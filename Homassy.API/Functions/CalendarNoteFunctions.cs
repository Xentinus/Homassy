using Homassy.API.Context;
using Homassy.API.Models.Calendar;
using Homassy.Data.Context;
using Homassy.Data.Entities.Family;
using Homassy.Data.Enums;
using Homassy.Data.Exceptions;
using Homassy.Data.Functions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    /// <summary>
    /// Day notes on the family calendar (#60): the CRUD behind the calendar's note panel, and the
    /// read <see cref="CalendarFunctions"/> folds into the aggregated calendar response.
    /// </summary>
    /// <remarks>
    /// Uncached, unlike most of this layer. Notes are written by hand a few times a week and read in
    /// one date-range query when the calendar opens, so the indexed <c>(FamilyId, Date)</c> lookup is
    /// already the cheap path - a process-wide cache would only add an invalidation hop.
    /// <para>
    /// Everything here is family-scoped through <see cref="SessionInfo"/>. A note has no personal
    /// variant by design: a note only its author can see is a reminder, and this feature exists
    /// precisely so the rest of the household finds out about the day.
    /// </para>
    /// </remarks>
    public class CalendarNoteFunctions
    {
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly UserFunctions _userFunctions;

        /// <summary>
        /// How far in the past a reminder may still be set. A reminder whose moment is long gone
        /// cannot fire, and the worker drops it as stale rather than pushing "this was two weeks
        /// ago" - so it is refused at the edge instead of being silently accepted.
        /// </summary>
        private static readonly TimeSpan MaxReminderBacklog = TimeSpan.FromDays(1);

        public CalendarNoteFunctions(IDbContextFactory<HomassyDbContext> contextFactory, UserFunctions userFunctions)
        {
            _contextFactory = contextFactory;
            _userFunctions = userFunctions;
        }

        /// <summary>The family's notes in a date range, oldest day first.</summary>
        public async Task<List<CalendarNoteInfo>> GetNotesAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            var familyId = SessionInfo.GetFamilyId();
            if (!familyId.HasValue)
            {
                // Not an error: a user with no family simply has no notes, and the calendar still
                // renders its other event types.
                return [];
            }

            using var context = _contextFactory.CreateForReading();
            var notes = await context.CalendarNotes
                .Where(n => n.FamilyId == familyId.Value && n.Date >= startDate && n.Date <= endDate)
                .OrderBy(n => n.Date)
                .ThenBy(n => n.CreatedAt)
                .ToListAsync(cancellationToken);

            return notes.Select(BuildInfo).ToList();
        }

        public async Task<CalendarNoteInfo> CreateNoteAsync(
            CreateCalendarNoteRequest request,
            CancellationToken cancellationToken = default)
        {
            var userId = RequireUser();
            var familyId = RequireFamily();
            var reminderAt = NormalizeReminder(request.ReminderAt);

            using var context = _contextFactory.CreateDbContext();

            var note = new CalendarNote
            {
                FamilyId = familyId,
                Date = request.Date,
                Title = request.Title.Trim(),
                Content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim(),
                ReminderAt = reminderAt,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            context.CalendarNotes.Add(note);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information("User {UserId} created calendar note {NoteId} for {Date}", userId, note.PublicId, note.Date);

            await ActivityRecorder.RecordAsync(
                _contextFactory, userId, familyId,
                ActivityType.CalendarNoteCreate, note.Id, note.Title,
                cancellationToken: cancellationToken);

            return BuildInfo(note);
        }

        public async Task<CalendarNoteInfo> UpdateNoteAsync(
            Guid publicId,
            UpdateCalendarNoteRequest request,
            CancellationToken cancellationToken = default)
        {
            var userId = RequireUser();
            var familyId = RequireFamily();

            using var context = _contextFactory.CreateDbContext();
            var note = await LoadForWriteAsync(context, publicId, familyId, cancellationToken);

            // Normalised against what the note already holds. This is a full-state PUT, so editing
            // only the title of an old note resends the reminder that already fired - and refusing
            // that as "in the past" would make every note uneditable a day after its reminder.
            var reminderAt = NormalizeReminder(request.ReminderAt, note.ReminderAt);

            // A moved reminder is a new reminder: clearing the sent marker is what re-arms the
            // worker for it. Leaving it alone would silently swallow the rescheduled push.
            if (note.ReminderAt != reminderAt)
            {
                note.ReminderSentAt = null;
            }

            note.Date = request.Date;
            note.Title = request.Title.Trim();
            note.Content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim();
            note.ReminderAt = reminderAt;
            note.LastEditedByUserId = userId;

            await context.SaveChangesAsync(cancellationToken);

            Log.Information("User {UserId} updated calendar note {NoteId}", userId, note.PublicId);

            await ActivityRecorder.RecordAsync(
                _contextFactory, userId, familyId,
                ActivityType.CalendarNoteUpdate, note.Id, note.Title,
                cancellationToken: cancellationToken);

            return BuildInfo(note);
        }

        public async Task DeleteNoteAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = RequireUser();
            var familyId = RequireFamily();

            using var context = _contextFactory.CreateDbContext();
            var note = await LoadForWriteAsync(context, publicId, familyId, cancellationToken);

            note.DeleteRecord(userId);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information("User {UserId} deleted calendar note {NoteId}", userId, note.PublicId);

            await ActivityRecorder.RecordAsync(
                _contextFactory, userId, familyId,
                ActivityType.CalendarNoteDelete, note.Id, note.Title,
                cancellationToken: cancellationToken);
        }

        private static async Task<CalendarNote> LoadForWriteAsync(
            HomassyDbContext context,
            Guid publicId,
            int familyId,
            CancellationToken cancellationToken)
        {
            var note = await context.CalendarNotes
                .FirstOrDefaultAsync(n => n.PublicId == publicId, cancellationToken)
                ?? throw new CalendarNoteNotFoundException();

            // Every member may edit and delete every note: the notes are the family's, not their
            // author's, and the author is recorded rather than enforced.
            if (note.FamilyId != familyId)
            {
                throw new CalendarNoteAccessDeniedException();
            }

            return note;
        }

        private static int RequireUser()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            return userId.Value;
        }

        private static int RequireFamily()
        {
            var familyId = SessionInfo.GetFamilyId();
            if (!familyId.HasValue)
            {
                throw new CalendarNoteRequiresFamilyException();
            }

            return familyId.Value;
        }

        /// <summary>
        /// Pins the reminder to UTC and refuses a <em>newly set</em> one whose moment has already
        /// passed for good. The client sends an instant it computed in the user's own zone; an
        /// unspecified kind would be read as server-local and shift the push by the container's
        /// offset.
        /// </summary>
        /// <param name="reminderAt">The reminder the request carries, or null to disarm.</param>
        /// <param name="current">
        /// What the note already holds, on an update. A reminder that is unchanged is never refused
        /// however old it is: the endpoint is a full-state PUT, so an edit that only touches the
        /// title resends the reminder that fired last week, and rejecting that would make a note
        /// permanently uneditable a day after its own reminder. Null on create, where every value is
        /// a new one.
        /// </param>
        private static DateTime? NormalizeReminder(DateTime? reminderAt, DateTime? current = null)
        {
            if (!reminderAt.HasValue)
            {
                return null;
            }

            var utc = reminderAt.Value.Kind switch
            {
                DateTimeKind.Utc => reminderAt.Value,
                DateTimeKind.Local => reminderAt.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(reminderAt.Value, DateTimeKind.Utc)
            };

            // Compared at second precision, not tick-for-tick. PostgreSQL stores `timestamptz` to
            // the microsecond, so a value that has been through the database already differs from
            // the one that was sent - and "unchanged" has to mean unchanged to a reader, not to a
            // clock. The UI picks reminders by the minute, so a second is comfortably finer than
            // any edit a person can make.
            if (current.HasValue && Math.Abs((utc - current.Value).TotalSeconds) < 1)
            {
                return current.Value;
            }

            if (utc < DateTime.UtcNow - MaxReminderBacklog)
            {
                throw new CalendarNoteInvalidReminderException();
            }

            return utc;
        }

        private CalendarNoteInfo BuildInfo(CalendarNote note)
        {
            var author = _userFunctions.GetUserById(note.CreatedByUserId);
            var authorProfile = _userFunctions.GetUserProfileByUserId(note.CreatedByUserId);
            var editor = note.LastEditedByUserId.HasValue
                ? _userFunctions.GetUserById(note.LastEditedByUserId)
                : null;
            var editorProfile = note.LastEditedByUserId.HasValue
                ? _userFunctions.GetUserProfileByUserId(note.LastEditedByUserId)
                : null;

            return new CalendarNoteInfo
            {
                PublicId = note.PublicId,
                Date = note.Date,
                Title = note.Title,
                Content = note.Content,
                ReminderAt = note.ReminderAt,
                ReminderSent = note.ReminderSentAt.HasValue,
                CreatedByPublicId = author?.PublicId ?? Guid.Empty,
                CreatedByName = authorProfile?.DisplayName ?? author?.Name ?? string.Empty,
                LastEditedByPublicId = editor?.PublicId,
                LastEditedByName = editor == null ? null : editorProfile?.DisplayName ?? editor.Name,
                CreatedAt = note.CreatedAt
            };
        }
    }
}
