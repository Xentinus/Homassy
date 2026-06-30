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
    }
}
