using Homassy.Data.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Data.Entities.Family
{
    /// <summary>
    /// A note pinned to one day of the family calendar (#60). Notes are family-wide, so they double
    /// as the lightweight way to tell the household something about that day ("school closed", "gas
    /// meter reading") without it having to be an inventory item, an automation or a shopping
    /// deadline.
    /// </summary>
    /// <remarks>
    /// The note belongs to a <see cref="Family"/> and never to a single user: there is no personal
    /// variant, because a note nobody else can see is a reminder, and reminders are what
    /// <see cref="ReminderAt"/> is for.
    /// <para>
    /// <see cref="Date"/> is a <see cref="DateOnly"/> (a <c>date</c> column), not a timestamp. The
    /// day a note is about has no time of day and must not be reinterpreted against a timezone -
    /// "the 3rd" stays the 3rd for every member, wherever they are. <see cref="ReminderAt"/> is the
    /// opposite: an instant, stored in UTC, because it is a moment a push goes out.
    /// </para>
    /// </remarks>
    public class CalendarNote : RecordChangeEntity
    {
        public required int FamilyId { get; set; }

        /// <summary>The day the note is about. A calendar day, not an instant - see the remarks.</summary>
        public required DateOnly Date { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(2000)]
        public string? Content { get; set; }

        /// <summary>
        /// When the family should be reminded, UTC, or null for a note that only sits on the
        /// calendar. The reminder worker in <c>Homassy.Notifications</c> claims a due note by
        /// stamping <see cref="ReminderSentAt"/> before it dispatches, so a restart can only ever
        /// skip a reminder, never repeat one.
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
