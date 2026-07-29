namespace Homassy.API.Constants
{
    public static class CalendarConstants
    {
        /// <summary>
        /// Longest date range a single calendar read may cover. Shared by every calendar-surface controller so
        /// they cannot drift: if only one were widened, the calendar screen would start getting 400s from that
        /// endpoint while the others kept working — a confusing partial failure.
        /// </summary>
        public const int MaxDateRangeDays = 93;
    }
}
