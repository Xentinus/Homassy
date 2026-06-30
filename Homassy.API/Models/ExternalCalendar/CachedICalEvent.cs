namespace Homassy.API.Models.ExternalCalendar
{
    public class CachedICalEvent
    {
        public string Uid { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string? Description { get; set; }
        public bool IsAllDay { get; set; }
    }
}
