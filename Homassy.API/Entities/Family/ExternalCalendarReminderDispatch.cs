using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// A record that one reminder has already been pushed, so a re-sync or a worker restart cannot send
    /// it twice. The occurrence is identified by <see cref="EventUid"/> + <see cref="OccurrenceKey"/>,
    /// which also gives moved events their re-arming for free: a moved occurrence produces a different
    /// key, so it has no marker yet and is treated as a fresh reminder. Deleted events simply stop
    /// appearing in the synced cache and never trigger.
    /// </summary>
    /// <remarks>
    /// Deliberately holds no navigation properties. Nothing reads the parent rows through this table, and a
    /// required navigation to a soft-delete-filtered principal makes EF log a query-filter interaction
    /// warning on every model build. The foreign keys (and their cascades) are configured in
    /// <c>HomassyDbContext</c> without them.
    /// </remarks>
    public class ExternalCalendarReminderDispatch : BaseEntity
    {
        public int ExternalCalendarId { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(512)]
        public required string EventUid { get; set; }

        /// <summary>
        /// Identifies the occurrence within the event series. For a timed event this is the occurrence's
        /// UTC start; for an all-day event it is the event date at 00:00. Stored as
        /// <c>timestamp without time zone</c> because it is a key, not an instant.
        /// </summary>
        public DateTime OccurrenceKey { get; set; }

        /// <summary>Which of the calendar's configured lead times this dispatch covers.</summary>
        public int LeadTimeMinutes { get; set; }

        public DateTime SentAt { get; set; }
    }
}
