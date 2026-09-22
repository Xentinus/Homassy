using Homassy.Data.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Data.Entities.Family
{
    /// <summary>
    /// A note pinned to one day of the calendar (#60): the lightweight way to record something about
    /// a day ("school closed", "gas meter reading") without it having to be an inventory item, an
    /// automation or a shopping deadline.
    /// </summary>
    /// <remarks>
    /// <see cref="FamilyId"/> decides who it is for. Set, the note is the household's and every
    /// member sees and edits it; null, it is personal and only its author can. Writing a note is not
    /// a household feature — a user who has not created or joined a family still has days worth
    /// noting, and refusing them one because nobody else would read it is a rule about the schema,
    /// not about the day.
    /// <para>
    /// A personal note stays personal after its author joins a family: the note was written for
    /// them, and quietly publishing a backlog of private notes to a household the moment somebody
    /// accepts an invitation is not a thing this app should do. Notes written from then on are the
    /// family's, because that is the context they are written in.
    /// </para>
    /// <para>
    /// <see cref="Date"/> is a <see cref="DateOnly"/> (a <c>date</c> column), not a timestamp. The
    /// day a note is about has no time of day and must not be reinterpreted against a timezone -
    /// "the 3rd" stays the 3rd for every member, wherever they are. <see cref="ReminderAt"/> is the
    /// opposite: an instant, stored in UTC, because it is a moment a push goes out.
    /// </para>
    /// </remarks>
    public class CalendarNote : RecordChangeEntity
    {
        /// <summary>The household the note belongs to, or null for a note that is only its author's.</summary>
        public int? FamilyId { get; set; }

        /// <summary>The day the note is about. A calendar day, not an instant - see the remarks.</summary>
        public required DateOnly Date { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(2000)]
        public string? Content { get; set; }

        /// <summary>
        /// When to remind — the household for a family note, the author alone for a personal one —
        /// in UTC, or null for a note that only sits on the calendar. The reminder worker in
        /// <c>Homassy.Notifications</c> claims a due note by stamping <see cref="ReminderSentAt"/>
        /// before it dispatches, so a restart can only ever skip a reminder, never repeat one.
        /// </summary>
        public DateTime? ReminderAt { get; set; }

        /// <summary>When the reminder was claimed for dispatch. Cleared whenever <see cref="ReminderAt"/> moves.</summary>
        public DateTime? ReminderSentAt { get; set; }

        /// <summary>Who wrote the note. Kept alongside the audit stamp because the UI shows it.</summary>
        public required int CreatedByUserId { get; set; }

        /// <summary>Who last edited it, or null while it is still as written.</summary>
        public int? LastEditedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Family? Family { get; set; }
        public User.User? CreatedBy { get; set; }
        public User.User? LastEditedBy { get; set; }
    }
}
