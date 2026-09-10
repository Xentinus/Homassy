namespace Homassy.API.Functions
{
    /// <summary>
    /// The result of scanning a set of qualifying days - see <see cref="StreakCalculator.Compute"/>.
    /// </summary>
    /// <param name="Current">
    /// The run the household is still on: the length of the latest unbroken run, but only when that
    /// run reaches today or yesterday. <c>0</c> once a day has actually been missed.
    /// </param>
    /// <param name="Longest">
    /// The longest unbroken run anywhere in the input, whether or not it is the current one. Never
    /// less than <paramref name="Current"/>.
    /// </param>
    /// <param name="LastQualifyingDay">
    /// The most recent qualifying day, or <see langword="null"/> when there are none. Lets a client
    /// say "last cleared on ..." without being handed the whole day set.
    /// </param>
    public readonly record struct StreakResult(int Current, int Longest, DateOnly? LastQualifyingDay);

    /// <summary>
    /// Turns a set of days on which something counted into a current-and-longest streak (#109).
    ///
    /// <para>
    /// <b>Pure and static</b>, like <see cref="SeriesZeroFill"/> and <see cref="UnitNormalization"/>:
    /// no <c>DbContext</c>, and specifically <b>no clock</b> - <c>today</c> is a parameter. That is
    /// what makes every rule below a plain input/output fact a unit test can state, and it is also
    /// what keeps a streak on the viewer's own calendar instead of UTC's: the caller resolves the
    /// household's local today (see <c>TimeZoneFunctions</c>) and passes it in, so a member in
    /// Budapest does not lose their streak at 01:00 local because UTC has not ticked over yet.
    /// </para>
    /// </summary>
    public static class StreakCalculator
    {
        /// <summary>
        /// Scans <paramref name="qualifyingDays"/> for consecutive-day runs.
        ///
        /// <para>
        /// <b>A run ending yesterday still counts as current.</b> This reads like an off-by-one
        /// until you know why: a streak is broken by a day that was actually <em>missed</em>, and
        /// today is not missed until it is over. Ending the streak at midnight would punish someone
        /// for not having opened the app yet this morning - the one case where the honest answer is
        /// "you are still on 5" rather than "you were on 5". A run ending the day before yesterday
        /// is genuinely broken: yesterday came and went with nothing.
        /// </para>
        ///
        /// <para>
        /// The input is normalized before anything is counted: duplicates collapse (the same day
        /// twice is one day), order does not matter, and days after <paramref name="today"/> are
        /// dropped - a qualifying day cannot be in the future on the viewer's own calendar, and a
        /// stray one (a clock skew, a seeded row) must not be able to inflate a streak or a
        /// "longest ever".
        /// </para>
        /// </summary>
        /// <param name="qualifyingDays">The days that count toward the streak, in any order.</param>
        /// <param name="today">The viewer's local today, so a streak does not break at UTC midnight.</param>
        public static StreakResult Compute(IEnumerable<DateOnly> qualifyingDays, DateOnly today)
        {
            var days = qualifyingDays
                .Where(day => day <= today)
                .Distinct()
                .OrderBy(day => day)
                .ToList();

            if (days.Count == 0)
            {
                return new StreakResult(0, 0, null);
            }

            var longest = 1;
            var currentRun = 1;

            // One pass over sorted, distinct days: each day either extends the run it follows or
            // starts a new one. Only the run that ends at days[^1] can be the current streak, so
            // there is nothing to track beyond the run in progress and the best seen so far.
            for (var i = 1; i < days.Count; i++)
            {
                var isConsecutive = days[i] == days[i - 1].AddDays(1);
                currentRun = isConsecutive ? currentRun + 1 : 1;

                if (currentRun > longest)
                {
                    longest = currentRun;
                }
            }

            var lastDay = days[^1];
            var yesterday = today.AddDays(-1);
            var current = lastDay == today || lastDay == yesterday ? currentRun : 0;

            return new StreakResult(current, longest, lastDay);
        }
    }
}
