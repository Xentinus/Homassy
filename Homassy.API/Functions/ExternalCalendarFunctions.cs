using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Exceptions;
using Homassy.API.Models.Calendar;
using Homassy.API.Models.ExternalCalendar;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;

namespace Homassy.API.Functions
{
    public class ExternalCalendarFunctions
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task<List<ExternalCalendarResponse>> GetExternalCalendarsAsync(CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new ExternalCalendarRequiresFamilyException();

            using var context = new HomassyDbContext();
            var calendars = await context.FamilyExternalCalendars
                .Where(c => c.FamilyId == familyId)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            return calendars.Select(MapToResponse).ToList();
        }

        public async Task<ExternalCalendarResponse> CreateExternalCalendarAsync(
            CreateExternalCalendarRequest request,
            HttpClient httpClient,
            CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new ExternalCalendarRequiresFamilyException();

            var normalizedUrl = NormalizeICalUrl(request.ICalUrl);
            await ValidateICalUrlAsync(normalizedUrl, httpClient, ct);

            using var context = new HomassyDbContext();
            var calendar = new FamilyExternalCalendar
            {
                FamilyId = familyId,
                Name = request.Name,
                ICalUrl = normalizedUrl,
                Color = request.Color
            };

            context.FamilyExternalCalendars.Add(calendar);
            await context.SaveChangesAsync(ct);

            return MapToResponse(calendar);
        }

        public async Task<ExternalCalendarResponse> UpdateExternalCalendarAsync(
            Guid publicId,
            UpdateExternalCalendarRequest request,
            HttpClient httpClient,
            CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new ExternalCalendarRequiresFamilyException();

            using var context = new HomassyDbContext();
            var calendar = await context.FamilyExternalCalendars
                .FirstOrDefaultAsync(c => c.PublicId == publicId, ct)
                ?? throw new ExternalCalendarNotFoundException();

            if (calendar.FamilyId != familyId)
                throw new ExternalCalendarAccessDeniedException();

            if (request.Name != null) calendar.Name = request.Name;
            if (request.Color != null) calendar.Color = request.Color;
            if (request.IsEnabled.HasValue) calendar.IsEnabled = request.IsEnabled.Value;

            if (request.ICalUrl != null)
            {
                var normalizedUrl = NormalizeICalUrl(request.ICalUrl);
                await ValidateICalUrlAsync(normalizedUrl, httpClient, ct);
                calendar.ICalUrl = normalizedUrl;
                // Reset sync state when URL changes
                calendar.LastSyncedAt = null;
                calendar.LastSyncError = null;
                calendar.CachedEventsJson = null;
            }

            await context.SaveChangesAsync(ct);
            return MapToResponse(calendar);
        }

        public async Task DeleteExternalCalendarAsync(Guid publicId, CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new ExternalCalendarRequiresFamilyException();

            using var context = new HomassyDbContext();
            var calendar = await context.FamilyExternalCalendars
                .FirstOrDefaultAsync(c => c.PublicId == publicId, ct)
                ?? throw new ExternalCalendarNotFoundException();

            if (calendar.FamilyId != familyId)
                throw new ExternalCalendarAccessDeniedException();

            calendar.DeleteRecord(SessionInfo.GetUserId());
            await context.SaveChangesAsync(ct);
        }

        public async Task<ExternalCalendarResponse> TriggerSyncAsync(
            Guid publicId,
            HttpClient httpClient,
            CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new ExternalCalendarRequiresFamilyException();

            using var context = new HomassyDbContext();
            var calendar = await context.FamilyExternalCalendars
                .FirstOrDefaultAsync(c => c.PublicId == publicId, ct)
                ?? throw new ExternalCalendarNotFoundException();

            if (calendar.FamilyId != familyId)
                throw new ExternalCalendarAccessDeniedException();

            await SyncCalendarAsync(calendar, context, httpClient, ct);
            await context.SaveChangesAsync(ct);

            return MapToResponse(calendar);
        }

        public List<CalendarEventInfo> GetCachedEventsForDateRange(
            int familyId,
            DateTime startDate,
            DateTime endDate)
        {
            using var context = new HomassyDbContext();
            var calendars = context.FamilyExternalCalendars
                .Where(c => c.FamilyId == familyId && c.IsEnabled && c.CachedEventsJson != null)
                .ToList();

            var result = new List<CalendarEventInfo>();

            foreach (var calendar in calendars)
            {
                try
                {
                    var events = JsonSerializer.Deserialize<List<CachedICalEvent>>(
                        calendar.CachedEventsJson!, JsonOptions);

                    if (events == null) continue;

                    foreach (var ev in events)
                    {
                        var eventStart = ev.Start;
                        var eventEnd = ev.End ?? ev.Start;

                        // Include event if it overlaps with the requested range
                        if (eventStart <= endDate && eventEnd >= startDate)
                        {
                            result.Add(new CalendarEventInfo
                            {
                                PublicId = calendar.PublicId,
                                Title = ev.Title,
                                EventType = CalendarEventType.ExternalCalendar,
                                Start = eventStart,
                                Detail = ev.Description,
                                RelatedEntityPublicId = null,
                                Color = calendar.Color
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to deserialize cached events for calendar {CalendarId}", calendar.PublicId);
                }
            }

            return result;
        }

        public static async Task SyncCalendarAsync(
            FamilyExternalCalendar calendar,
            HomassyDbContext context,
            HttpClient httpClient,
            CancellationToken ct)
        {
            try
            {
                Log.Information("Syncing external calendar {CalendarId} ({Name})", calendar.PublicId, calendar.Name);

                var icsContent = await httpClient.GetStringAsync(calendar.ICalUrl, ct);
                icsContent = SanitizeICalContent(icsContent);
                var parsed = Calendar.Load(icsContent);
                var events = new List<CachedICalEvent>();

                foreach (var ev in parsed.Events)
                {
                    var uid = ev.Uid ?? Guid.NewGuid().ToString();
                    var title = ev.Summary ?? string.Empty;
                    var isAllDay = ev.IsAllDay;
                    var start = ev.DtStart?.AsSystemLocal ?? DateTime.UtcNow;
                    var end = ev.DtEnd?.AsSystemLocal;
                    var description = ev.Description;

                    events.Add(new CachedICalEvent
                    {
                        Uid = uid,
                        Title = title,
                        Start = start,
                        End = end,
                        Description = description,
                        IsAllDay = isAllDay
                    });
                }

                calendar.CachedEventsJson = JsonSerializer.Serialize(events, JsonOptions);
                calendar.LastSyncedAt = DateTime.UtcNow;
                calendar.LastSyncError = null;

                Log.Information("Synced {Count} events for calendar {CalendarId}", events.Count, calendar.PublicId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message.Length > 512 ? ex.Message[..512] : ex.Message;
                calendar.LastSyncError = errorMessage;
                Log.Error(ex, "Failed to sync external calendar {CalendarId}", calendar.PublicId);
            }
        }

        private static async Task ValidateICalUrlAsync(string url, HttpClient httpClient, CancellationToken ct)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                var content = await httpClient.GetStringAsync(url, timeoutCts.Token);

                if (!content.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
                    throw new ExternalCalendarInvalidUrlException("URL does not point to a valid iCal feed.");
            }
            catch (ExternalCalendarInvalidUrlException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new ExternalCalendarFetchFailedException("iCal feed fetch timed out.");
            }
            catch (Exception ex)
            {
                throw new ExternalCalendarFetchFailedException($"Failed to fetch iCal feed: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes vendor properties that Ical.Net v4's content-line parser cannot handle
        /// (notably Apple's <c>X-APPLE-STRUCTURED-LOCATION</c>, which carries quoted parameters
        /// with embedded commas/colons and a long base64 <c>X-APPLE-MAPKIT-HANDLE</c> value).
        /// A single bad line aborts the whole feed parse, so these lines are stripped before load.
        /// RFC 5545 folded continuation lines are unfolded first so a folded property is dropped
        /// in its entirety rather than leaving orphan fragments. None of the stripped data is used
        /// (only Uid/Summary/DtStart/DtEnd/Description/IsAllDay are read).
        /// </summary>
        private static string SanitizeICalContent(string raw)
        {
            var physicalLines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            // Unfold: a line starting with space or tab continues the previous logical line.
            var logicalLines = new List<string>();
            foreach (var line in physicalLines)
            {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && logicalLines.Count > 0)
                    logicalLines[^1] += line[1..];
                else
                    logicalLines.Add(line);
            }

            var kept = logicalLines.Where(line =>
            {
                var nameEnd = line.IndexOfAny([';', ':']);
                var name = nameEnd >= 0 ? line[..nameEnd] : line;
                return !name.StartsWith("X-APPLE-", StringComparison.OrdinalIgnoreCase);
            });

            return string.Join("\r\n", kept);
        }

        private static string NormalizeICalUrl(string url)
        {
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
                return "https://" + url[9..];

            return url;
        }

        private static ExternalCalendarResponse MapToResponse(FamilyExternalCalendar calendar)
        {
            int eventCount = 0;
            if (calendar.CachedEventsJson != null)
            {
                try
                {
                    var events = JsonSerializer.Deserialize<List<CachedICalEvent>>(
                        calendar.CachedEventsJson, JsonOptions);
                    eventCount = events?.Count ?? 0;
                }
                catch { /* ignore */ }
            }

            return new ExternalCalendarResponse
            {
                PublicId = calendar.PublicId,
                Name = calendar.Name,
                ICalUrl = calendar.ICalUrl,
                Color = calendar.Color,
                IsEnabled = calendar.IsEnabled,
                LastSyncedAt = calendar.LastSyncedAt,
                LastSyncError = calendar.LastSyncError,
                EventCount = eventCount
            };
        }
    }
}
