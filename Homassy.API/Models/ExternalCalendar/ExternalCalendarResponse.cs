namespace Homassy.API.Models.ExternalCalendar
{
    public class ExternalCalendarResponse
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ICalUrl { get; set; } = string.Empty;
        public string Color { get; set; } = "#3B82F6";
        public bool IsEnabled { get; set; }
        public DateTime? LastSyncedAt { get; set; }
        public string? LastSyncError { get; set; }
        public int EventCount { get; set; }

        /// <summary>Reminder lead times in minutes before the event starts; empty = reminders off.</summary>
        public List<int> ReminderLeadTimes { get; set; } = [];

        /// <summary>Time of day all-day reminders are anchored to, as <c>HH:mm</c>.</summary>
        public string AllDayNotifyTime { get; set; } = "08:00";
    }
}
