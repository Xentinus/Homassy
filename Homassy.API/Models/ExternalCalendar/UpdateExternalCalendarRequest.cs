using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ExternalCalendar
{
    public class UpdateExternalCalendarRequest
    {
        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public string? Name { get; set; }

        [StringLength(2048)]
        [PublicFeedUrl]
        public string? ICalUrl { get; set; }

        [StringLength(7)]
        public string? Color { get; set; }

        public bool? IsEnabled { get; set; }

        /// <summary>
        /// Reminder lead times in minutes before the event starts. Null leaves them unchanged;
        /// an empty array turns reminders off for this calendar.
        /// </summary>
        public List<int>? ReminderLeadTimes { get; set; }

        /// <summary>Time of day all-day reminders are anchored to, as <c>HH:mm</c>.</summary>
        [StringLength(8)]
        public string? AllDayNotifyTime { get; set; }
    }
}
