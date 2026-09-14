namespace Homassy.API.Models.Calendar
{
    public class GetCalendarNotesRequest
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}
