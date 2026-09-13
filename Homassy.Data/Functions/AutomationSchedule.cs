using Homassy.Data.Enums;
using Homassy.Data.Extensions;

namespace Homassy.Data.Functions
{
    /// <summary>
    /// When an automation is next due, in UTC.
    /// </summary>
    /// <remarks>
    /// Shared because both sides of the boundary set the same column: the API when someone
    /// creates or edits an automation, and <c>Homassy.Notifications</c> when the worker runs one
    /// and schedules the next occurrence (#91). Two implementations of "next Tuesday at 08:00 in
    /// the user's timezone" would drift apart, and the drift would look like an automation
    /// firing at the wrong time rather than like a bug in either place.
    /// <para>
    /// Genuinely static and cache-free, which is what makes it callable from a worker process.
    /// </para>
    /// </remarks>
    public static class AutomationSchedule
    {
        /// <summary>
        /// Calculates the next execution time in UTC based on the automation schedule and user's timezone.
        /// </summary>
        public static DateTime? CalculateNextExecutionAt(
            ScheduleType scheduleType,
            TimeOnly scheduledTime,
            int? intervalDays,
            DaysOfWeek? scheduledDaysOfWeek,
            int? scheduledDayOfMonth,
            UserTimeZone userTimeZone,
            DateTime? lastExecutedAtUtc = null)
        {
            var tzId = userTimeZone.ToTimeZoneId();
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            DateTime nextLocal;

            if (scheduleType == ScheduleType.Interval)
            {
                if (!intervalDays.HasValue || intervalDays.Value < 1)
                    return null;

                if (lastExecutedAtUtc.HasValue)
                {
                    var lastLocal = TimeZoneInfo.ConvertTimeFromUtc(lastExecutedAtUtc.Value, tz);
                    var nextDate = lastLocal.Date.AddDays(intervalDays.Value);
                    nextLocal = nextDate.Add(scheduledTime.ToTimeSpan());

                    // If calculated time is in the past, advance forward
                    while (nextLocal <= nowLocal)
                    {
                        nextLocal = nextLocal.AddDays(intervalDays.Value);
                    }
                }
                else
                {
                    // First execution: schedule for today at the specified time, or tomorrow if past
                    nextLocal = nowLocal.Date.Add(scheduledTime.ToTimeSpan());
                    if (nextLocal <= nowLocal)
                    {
                        nextLocal = nextLocal.AddDays(intervalDays.Value);
                    }
                }
            }
            else // FixedDate
            {
                if (scheduledDaysOfWeek.HasValue && scheduledDaysOfWeek.Value != DaysOfWeek.None)
                {
                    // Weekly schedule: find next occurrence among selected days
                    var selectedDays = GetSelectedDays(scheduledDaysOfWeek.Value);
                    if (selectedDays.Count == 0)
                        return null;

                    // Find the nearest upcoming day from the set
                    DateTime? earliest = null;
                    foreach (var day in selectedDays)
                    {
                        var daysUntil = ((int)day - (int)nowLocal.DayOfWeek + 7) % 7;
                        var candidate = nowLocal.Date.AddDays(daysUntil).Add(scheduledTime.ToTimeSpan());

                        // If it's today but the time has passed, go to next week
                        if (candidate <= nowLocal)
                        {
                            candidate = candidate.AddDays(7);
                        }

                        if (!earliest.HasValue || candidate < earliest.Value)
                        {
                            earliest = candidate;
                        }
                    }

                    nextLocal = earliest!.Value;
                }
                else if (scheduledDayOfMonth.HasValue)
                {
                    // Monthly schedule: find next occurrence of the specified day of month
                    var day = Math.Min(scheduledDayOfMonth.Value, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month));
                    nextLocal = new DateTime(nowLocal.Year, nowLocal.Month, day).Add(scheduledTime.ToTimeSpan());

                    if (nextLocal <= nowLocal)
                    {
                        // Move to next month
                        var nextMonth = nowLocal.AddMonths(1);
                        day = Math.Min(scheduledDayOfMonth.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                        nextLocal = new DateTime(nextMonth.Year, nextMonth.Month, day).Add(scheduledTime.ToTimeSpan());
                    }
                }
                else
                {
                    return null;
                }
            }

            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), tz);
        }

        /// <summary>
        /// Converts DaysOfWeek flags to a list of DayOfWeek values.
        /// </summary>
        internal static List<DayOfWeek> GetSelectedDays(DaysOfWeek daysOfWeek)
        {
            var days = new List<DayOfWeek>();
            if (daysOfWeek.HasFlag(DaysOfWeek.Monday)) days.Add(DayOfWeek.Monday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Tuesday)) days.Add(DayOfWeek.Tuesday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Wednesday)) days.Add(DayOfWeek.Wednesday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Thursday)) days.Add(DayOfWeek.Thursday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Friday)) days.Add(DayOfWeek.Friday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Saturday)) days.Add(DayOfWeek.Saturday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Sunday)) days.Add(DayOfWeek.Sunday);
            return days;
        }
    }
}
