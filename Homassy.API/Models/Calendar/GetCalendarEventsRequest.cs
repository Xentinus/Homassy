namespace Homassy.API.Models.Calendar
{
    public class GetCalendarEventsRequest
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}
