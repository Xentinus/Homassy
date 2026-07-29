using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Hubs;
using Homassy.API.Models.CalendarNote;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using CalendarNoteEntity = Homassy.API.Entities.Family.CalendarNote;

namespace Homassy.API.Functions
{
    /// <summary>
    /// Family-shared day notes. Deliberately cache-free (like external calendars): notes are range-queried by
    /// date, unbounded in count and mutated by every member, so an in-memory mirror would buy nothing.
    /// </summary>
    public class CalendarNoteFunctions
    {
        /// <summary>
        /// A reminder whose instant has already passed is still delivered inside this window. Shared with
        /// <c>CalendarNoteReminderService</c> in Homassy.Notifications so the write-time "already stale" check
        /// and the worker's delivery window can never diverge.
        /// </summary>
        public static readonly TimeSpan ReminderCatchUpWindow = TimeSpan.FromMinutes(15);

        /// <summary>Backstop on a single range read; a family with more notes than this in one range is not real.</summary>
        private const int MaxNotesPerRange = 1000;

        public async Task<List<CalendarNoteResponse>> GetCalendarNotesAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId();

            // A user with no family still has a working calendar (inventory, automations and shopping deadlines
            // are all user-scoped), so an empty list beats an exception that would break the whole page. Writes
            // do throw — see CreateCalendarNoteAsync.
            if (familyId == null)
                return [];

            using var context = new HomassyDbContext();

            var notes = await context.CalendarNotes
                .AsNoTracking()
                .Where(n => n.FamilyId == familyId.Value && n.Date >= startDate && n.Date <= endDate)
                .OrderBy(n => n.Date)
                .ThenBy(n => n.Id)
                .Take(MaxNotesPerRange)
                .ToListAsync(ct);

            var userFunctions = new UserFunctions();
            return notes.Select(n => MapToResponse(n, userFunctions)).ToList();
        }

        public async Task<CalendarNoteResponse> CreateCalendarNoteAsync(
            CreateCalendarNoteRequest request,
            CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new CalendarNoteRequiresFamilyException();
            var userId = SessionInfo.GetUserId()
                ?? throw new UnauthorizedAccessException("User not authenticated");

            // Backstop behind ModelState: a missing DateOnly binds to 0001-01-01 rather than failing.
            if (request.Date is not { } date || date == default)
                throw new CalendarNoteInvalidDateException();

            using var context = new HomassyDbContext();
            var note = new CalendarNoteEntity
            {
                FamilyId = familyId,
                Date = date,
                Title = request.Title,
                Content = NormalizeContent(request.Content),
                CreatedByUserId = userId,
                LastEditedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                RowVersion = Guid.NewGuid()
            };

            if (request.ReminderTime != null)
            {
                var timeOfDay = ParseReminderTime(request.ReminderTime);
                var zone = ResolveUserTimeZone(userId);

                note.ReminderTimeOfDay = timeOfDay;
                note.ReminderTimeZone = zone;
                note.ReminderAt = ComputeReminderAt(date, timeOfDay, zone);
                StampIfAlreadyStale(note);
            }

            context.CalendarNotes.Add(note);
            await context.SaveChangesAsync(ct);

            var response = MapToResponse(note, new UserFunctions());

            // Broadcast first (latency-sensitive), activity second (bookkeeping). The activity call has to come
            // after SaveChangesAsync because it takes the internal int Id.
            await MasterDataRealtime.CalendarNoteUpsertedAsync(familyId, response, ct);
            await RecordActivitySafelyAsync(userId, familyId, ActivityType.CalendarNoteCreate, note.Id, note.Title, ct);

            return response;
        }

        public async Task<CalendarNoteResponse> UpdateCalendarNoteAsync(
            Guid publicId,
            UpdateCalendarNoteRequest request,
            CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new CalendarNoteRequiresFamilyException();
            var userId = SessionInfo.GetUserId()
                ?? throw new UnauthorizedAccessException("User not authenticated");

            // Reject the contradictory combination before mutating anything.
            if (request.ClearReminder && request.ReminderTime != null)
                throw new CalendarNoteInvalidReminderException("Cannot set and clear the reminder in the same request.");

            using var context = new HomassyDbContext();

            // Fetch by PublicId only, then check the family — that keeps "does not exist" (404) distinguishable
            // from "belongs to another family" (403).
            var note = await context.CalendarNotes
                .FirstOrDefaultAsync(n => n.PublicId == publicId, ct)
                ?? throw new CalendarNoteNotFoundException();

            if (note.FamilyId != familyId)
                throw new CalendarNoteAccessDeniedException();

            ApplyClientVersion(context, note, request.Version);

            if (request.Title != null) note.Title = request.Title;
            if (request.Content != null) note.Content = NormalizeContent(request.Content);

            ApplyReminderPatch(note, request);

            // Read this before stamping the editor fields — setting them would itself mark the entry Modified.
            var hasChanges = context.ChangeTracker.HasChanges();

            if (hasChanges)
            {
                note.LastEditedByUserId = userId;
                note.LastEditedAt = DateTime.UtcNow;
                note.RowVersion = Guid.NewGuid();
            }

            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new CalendarNoteConcurrencyException();
            }

            var response = MapToResponse(note, new UserFunctions());

            // A no-op PUT must not spam the activity feed or the realtime channel.
            if (hasChanges)
            {
                await MasterDataRealtime.CalendarNoteUpsertedAsync(familyId, response, ct);
                await RecordActivitySafelyAsync(userId, familyId, ActivityType.CalendarNoteUpdate, note.Id, note.Title, ct);
            }

            return response;
        }

        public async Task DeleteCalendarNoteAsync(Guid publicId, CancellationToken ct = default)
        {
            var familyId = SessionInfo.GetFamilyId()
                ?? throw new CalendarNoteRequiresFamilyException();
            var userId = SessionInfo.GetUserId()
                ?? throw new UnauthorizedAccessException("User not authenticated");

            using var context = new HomassyDbContext();
            var note = await context.CalendarNotes
                .FirstOrDefaultAsync(n => n.PublicId == publicId, ct)
                ?? throw new CalendarNoteNotFoundException();

            if (note.FamilyId != familyId)
                throw new CalendarNoteAccessDeniedException();

            // The reminder fields are left intact: the global soft-delete filter already keeps the row out of
            // the worker's scan, and leaving them coherent keeps a hypothetical undelete sane. LastEditedByUserId
            // is likewise untouched — DeleteRecord overwrites RecordChange.LastModifiedBy with the deleter, which
            // is precisely why the explicit author/editor columns exist.
            note.DeleteRecord(userId);
            await context.SaveChangesAsync(ct);

            await MasterDataRealtime.CalendarNoteDeletedAsync(familyId, note.PublicId, ct);
            await RecordActivitySafelyAsync(userId, familyId, ActivityType.CalendarNoteDelete, note.Id, note.Title, ct);
        }

        /// <summary>
        /// Recomputes the reminder from the patch. The reminder is defined by the triple
        /// (Date, ReminderTimeOfDay, ReminderTimeZone); <c>ReminderAt</c> is a derived projection, which is what
        /// lets a date change be recomputed deterministically without guessing which zone to convert back through.
        /// </summary>
        private static void ApplyReminderPatch(CalendarNoteEntity note, UpdateCalendarNoteRequest request)
        {
            var newDate = request.Date ?? note.Date;

            TimeOnly? newTimeOfDay;
            if (request.ClearReminder)
                newTimeOfDay = null;
            else if (request.ReminderTime != null)
                newTimeOfDay = ParseReminderTime(request.ReminderTime);
            else
                newTimeOfDay = note.ReminderTimeOfDay;

            if (newTimeOfDay is null)
            {
                note.ReminderAt = null;
                note.ReminderTimeOfDay = null;
                note.ReminderTimeZone = null;
                note.ReminderSentAt = null;
            }
            else
            {
                // The zone stays as first snapshotted, so neither an editor in another zone nor the author
                // changing their profile timezone can silently move an existing reminder.
                var zone = note.ReminderTimeZone ?? ResolveUserTimeZone(note.CreatedByUserId);
                var newReminderAt = ComputeReminderAt(newDate, newTimeOfDay.Value, zone);

                // Re-arm only when the instant actually moved AND moved into the future. Without the
                // future-only half, nudging the time back and forth across "now" would clear ReminderSentAt
                // repeatedly and re-push the same note.
                if (newReminderAt != note.ReminderAt && newReminderAt > DateTime.UtcNow)
                    note.ReminderSentAt = null;

                note.ReminderAt = newReminderAt;
                note.ReminderTimeOfDay = newTimeOfDay;
                note.ReminderTimeZone = zone;
                StampIfAlreadyStale(note);
            }

            note.Date = newDate;
        }

        /// <summary>
        /// Seeds the row version the client read, so EF's concurrency check fires against that rather than
        /// against the value this request just fetched. Without it the check would only cover the milliseconds
        /// between fetch and save, which is not where a lost update happens.
        /// </summary>
        private static void ApplyClientVersion(HomassyDbContext context, CalendarNoteEntity note, string version)
        {
            if (!Guid.TryParse(version, out var parsed))
                throw new CalendarNoteConcurrencyException("Malformed concurrency token");

            context.Entry(note).Property(n => n.RowVersion).OriginalValue = parsed;
        }

        /// <summary>
        /// Retires a reminder that can never be delivered — one whose instant is already older than the worker's
        /// catch-up window (a note for today at 21:00 entered at 22:00, or a backfilled past note). Rejecting the
        /// save instead would be user-hostile, and leaving it unclaimed would keep a row in the pending-reminder
        /// index that the worker re-examines every minute forever.
        /// </summary>
        private static void StampIfAlreadyStale(CalendarNoteEntity note)
        {
            if (note.ReminderAt is { } at && at <= DateTime.UtcNow - ReminderCatchUpWindow)
                note.ReminderSentAt = DateTime.UtcNow;
        }

        private static DateTime ComputeReminderAt(DateOnly date, TimeOnly timeOfDay, UserTimeZone zone) =>
            zone.ToTimeZoneInfo().LocalToUtc(date.ToDateTime(timeOfDay));

        private static TimeOnly ParseReminderTime(string value)
        {
            if (!TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
                throw new CalendarNoteInvalidReminderException("The reminder time must be given as HH:mm.");

            return parsed;
        }

        private static UserTimeZone ResolveUserTimeZone(int userId) =>
            new UserFunctions().GetUserProfileByUserId(userId)?.DefaultTimeZone
                ?? UserTimeZone.CentralEuropeStandardTime;

        /// <summary>Collapses an empty or whitespace body to null, so "no content" has one representation.</summary>
        private static string? NormalizeContent(string? content) =>
            string.IsNullOrWhiteSpace(content) ? null : content;

        private static async Task RecordActivitySafelyAsync(
            int userId, int familyId, ActivityType activityType, int recordId, string recordName, CancellationToken ct)
        {
            try
            {
                await new ActivityFunctions().RecordActivityAsync(
                    userId, familyId, activityType, recordId, recordName, null, null, ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record {activityType} activity for calendar note {recordId}");
            }
        }

        private static CalendarNoteResponse MapToResponse(
            CalendarNoteEntity note,
            UserFunctions userFunctions)
        {
            var author = userFunctions.GetUserById(note.CreatedByUserId);
            var lastEditor = note.LastEditedByUserId == note.CreatedByUserId
                ? author
                : userFunctions.GetUserById(note.LastEditedByUserId);

            return new CalendarNoteResponse
            {
                PublicId = note.PublicId,
                Date = note.Date,
                Title = note.Title,
                Content = note.Content,
                ReminderAt = note.ReminderAt,
                ReminderTime = note.ReminderTimeOfDay?.ToString(@"HH\:mm", CultureInfo.InvariantCulture),
                ReminderTimeZone = note.ReminderTimeZone,
                ReminderSentAt = note.ReminderSentAt,
                AuthorPublicId = author?.PublicId ?? Guid.Empty,
                AuthorName = ResolveDisplayName(note.CreatedByUserId, author, userFunctions),
                LastEditedByPublicId = lastEditor?.PublicId ?? Guid.Empty,
                LastEditedByName = ResolveDisplayName(note.LastEditedByUserId, lastEditor, userFunctions),
                CreatedAt = note.CreatedAt,
                LastEditedAt = note.LastEditedAt,
                Version = note.RowVersion.ToString()
            };
        }

        /// <summary>
        /// Profile display name first, account name second — both come out of <see cref="UserFunctions"/>'
        /// caches, so resolving a whole range costs no extra queries. A member who has since left the family
        /// still resolves, which is correct: the note is history.
        /// </summary>
        private static string ResolveDisplayName(int userId, Entities.User.User? user, UserFunctions userFunctions) =>
            userFunctions.GetUserProfileByUserId(userId)?.DisplayName
                ?? user?.Name
                ?? "Unknown User";
    }
}
