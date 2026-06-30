using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Exceptions;
using Homassy.API.Models.Calendar;
using Homassy.API.Models.ExternalCalendar;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text;
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
                                End = ev.End,
                                Detail = ev.Description,
                                RelatedEntityPublicId = null,
                                Color = calendar.Color,
                                IsAllDay = ev.IsAllDay
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

                var events = LoadAndExpandEvents(icsContent, calendar.PublicId);

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
        /// Strips content that Ical.Net v4's content-line parser cannot handle, so a single bad
        /// line does not abort the whole feed. RFC 5545 folded continuation lines (leading space/tab)
        /// are unfolded first, then a logical line is kept only if its property-name token is a valid
        /// RFC 5545 name (alphanumerics + hyphen) and is not an Apple vendor property (<c>X-APPLE-*</c>).
        /// This drops Apple's <c>X-APPLE-STRUCTURED-LOCATION</c> as well as orphan fragments left when a
        /// vendor value is broken across a raw (non-folded) newline — e.g. an address tail like
        /// <c>...":geo:..."</c> whose "name" contains spaces/quotes. None of the stripped data is used
        /// (only Uid/Summary/DtStart/DtEnd/Description/IsAllDay are read).
        /// </summary>
        private static string SanitizeICalContent(string raw)
        {
            // Strip a leading UTF-8 BOM so the first BEGIN:VCALENDAR isn't misjudged.
            raw = raw.TrimStart('﻿');

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

                if (name.StartsWith("X-APPLE-", StringComparison.OrdinalIgnoreCase))
                    return false;

                // Valid iCal property name = alphanumerics + hyphen. Anything else (orphan
                // fragments from raw-newline-broken values, blank lines) is dropped.
                return name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
            });

            return string.Join("\r\n", kept);
        }

        /// <summary>
        /// Loads events from sanitized iCal text and expands recurrences into concrete occurrences
        /// without letting one bad event abort the whole feed. Fast path parses + expands the entire
        /// feed; if that throws, falls back to parsing each VEVENT block on its own (wrapped in a
        /// minimal VCALENDAR that retains the feed's VTIMEZONE blocks so TZID-based start/end times
        /// still resolve). Events that still fail to parse are skipped.
        /// </summary>
        private static List<CachedICalEvent> LoadAndExpandEvents(string ics, Guid calendarId)
        {
            // Recurrence rules can be unbounded, so expansion is limited to a rolling window.
            // Sync runs hourly (and on startup), so the window keeps sliding forward over time.
            var windowStart = DateTime.Now.AddMonths(-2);
            var windowEnd = DateTime.Now.AddMonths(12);

            try
            {
                return ExpandCalendar(Calendar.Load(ics), windowStart, windowEnd, calendarId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "External calendar {CalendarId}: whole-feed parse failed, falling back to per-event parsing",
                    calendarId);
            }

            var events = new List<CachedICalEvent>();
            var tzPrefix = string.Concat(ExtractComponentBlocks(ics, "VTIMEZONE"));

            foreach (var vevent in ExtractComponentBlocks(ics, "VEVENT"))
            {
                var single = $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Homassy//EN\r\n{tzPrefix}{vevent}END:VCALENDAR\r\n";
                try
                {
                    events.AddRange(ExpandCalendar(Calendar.Load(single), windowStart, windowEnd, calendarId));
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "External calendar {CalendarId}: skipped an unparseable event", calendarId);
                }
            }

            return events;
        }

        /// <summary>
        /// Expands a parsed calendar into <see cref="CachedICalEvent"/>s. Recurring events and any
        /// single events that fall inside the window are expanded via Ical.Net's occurrence engine
        /// (RRULE/RDATE expanded, EXDATE excluded, RECURRENCE-ID overrides applied). Non-recurring
        /// events whose only occurrence lies entirely outside the window are added directly so distant
        /// single events are not lost. If occurrence expansion itself throws on a malformed recurrence,
        /// every master event is stored once so we never regress to zero events.
        /// </summary>
        private static List<CachedICalEvent> ExpandCalendar(Calendar cal, DateTime windowStart, DateTime windowEnd, Guid calendarId)
        {
            var result = new List<CachedICalEvent>();

            try
            {
                foreach (var occ in cal.GetOccurrences(windowStart, windowEnd))
                {
                    if (occ.Source is not CalendarEvent ev) continue;
                    result.Add(MapEvent(ev, occ.Period.StartTime?.AsSystemLocal, occ.Period.EndTime?.AsSystemLocal));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "External calendar {CalendarId}: occurrence expansion failed, storing master events only",
                    calendarId);
                return cal.Events.Select(ev => MapEvent(ev, ev.DtStart?.AsSystemLocal, ev.DtEnd?.AsSystemLocal)).ToList();
            }

            // Safety net: a non-recurring event that does not overlap the window is dropped by
            // GetOccurrences. Add those directly (overlapping ones were already returned above).
            foreach (var ev in cal.Events)
            {
                if (ev.RecurrenceRules?.Count > 0 || ev.RecurrenceDates?.Count > 0) continue;

                var start = ev.DtStart?.AsSystemLocal;
                if (start == null) continue;

                var end = ev.DtEnd?.AsSystemLocal ?? start.Value;
                var overlapsWindow = start.Value <= windowEnd && end >= windowStart;
                if (overlapsWindow) continue;

                result.Add(MapEvent(ev, start, ev.DtEnd?.AsSystemLocal));
            }

            return result;
        }

        private static CachedICalEvent MapEvent(CalendarEvent ev, DateTime? start, DateTime? end) => new()
        {
            Uid = ev.Uid ?? Guid.NewGuid().ToString(),
            Title = ev.Summary ?? string.Empty,
            Start = start ?? DateTime.UtcNow,
            End = end,
            Description = ev.Description,
            IsAllDay = ev.IsAllDay
        };

        /// <summary>
        /// Extracts each <c>BEGIN:{component} … END:{component}</c> block (inclusive) as a CRLF-terminated string.
        /// </summary>
        private static List<string> ExtractComponentBlocks(string ics, string component)
        {
            var blocks = new List<string>();
            var begin = $"BEGIN:{component}";
            var end = $"END:{component}";

            StringBuilder? current = null;
            foreach (var line in ics.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Equals(begin, StringComparison.OrdinalIgnoreCase))
                {
                    current = new StringBuilder();
                    current.Append(line).Append("\r\n");
                }
                else if (current != null)
                {
                    current.Append(line).Append("\r\n");
                    if (line.Equals(end, StringComparison.OrdinalIgnoreCase))
                    {
                        blocks.Add(current.ToString());
                        current = null;
                    }
                }
            }

            return blocks;
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
