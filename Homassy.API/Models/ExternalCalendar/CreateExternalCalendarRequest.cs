using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ExternalCalendar
{
    public class CreateExternalCalendarRequest
    {
        [Required]
        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public required string Name { get; set; }

        [Required]
        [StringLength(2048)]
        [PublicFeedUrl]
        public required string ICalUrl { get; set; }

        [StringLength(7)]
        public string Color { get; set; } = "#3B82F6";

        /// <summary>Reminder lead times in minutes before the event starts; <c>0</c> = at start.</summary>
        public List<int>? ReminderLeadTimes { get; set; }

        /// <summary>Time of day all-day reminders are anchored to, as <c>HH:mm</c>. Defaults to 08:00.</summary>
        [StringLength(8)]
        public string? AllDayNotifyTime { get; set; }
    }
}
