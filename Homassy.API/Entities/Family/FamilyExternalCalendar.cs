using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Family
{
    public class FamilyExternalCalendar : RecordChangeEntity
    {
        public int FamilyId { get; set; }
        public Family Family { get; set; } = null!;

        [Required]
        [StringLength(64, MinimumLength = 2)]
        public required string Name { get; set; }

        [Required]
        [StringLength(2048)]
        public required string ICalUrl { get; set; }

        [StringLength(7)]
        public string Color { get; set; } = "#3B82F6";

        public bool IsEnabled { get; set; } = true;

        public DateTime? LastSyncedAt { get; set; }

        [StringLength(512)]
        public string? LastSyncError { get; set; }

        public string? CachedEventsJson { get; set; }

        /// <summary>
        /// Reminder lead times in minutes before an event starts, as a JSON int array
        /// (e.g. <c>[1440,15]</c> = one day and 15 minutes before). <c>0</c> means "at start".
        /// Null or an empty array disables reminders for this calendar.
        /// </summary>
        [StringLength(256)]
        public string? ReminderLeadTimesJson { get; set; }

        /// <summary>
        /// Time of day an all-day event's reminder is anchored to, instead of midnight. Interpreted in
        /// each recipient's own timezone, so members in different zones are notified at their local 08:00.
        /// </summary>
        public TimeOnly AllDayNotifyTime { get; set; } = new(8, 0);
    }
}
