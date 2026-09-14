namespace Homassy.API.Models.Calendar
{
    public enum CalendarEventType
    {
        InventoryExpiration = 0,
        AutomationExecution = 1,
        ShoppingListDeadline = 2,
        ExternalCalendar = 3,

        /// <summary>A family day note (#60). Carries its own colour/marker in the calendar view.</summary>
        DayNote = 4
    }

    public class CalendarEventInfo
    {
        public Guid PublicId { get; set; }
        public string Title { get; set; } = string.Empty;
        public CalendarEventType EventType { get; set; }
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string? Detail { get; set; }
        public Guid? RelatedEntityPublicId { get; set; }
        public string? Color { get; set; }
        public bool IsAllDay { get; set; }
    }
}
