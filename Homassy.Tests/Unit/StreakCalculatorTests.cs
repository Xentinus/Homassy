using Homassy.API.Functions;

namespace Homassy.Tests.Unit;

/// <summary>
/// Every rule <see cref="StreakCalculator.Compute"/> promises, as a plain input/output fact. There
/// is no clock and no database here on purpose - that is exactly what taking <c>today</c> as a
/// parameter buys, and it is why the "ends yesterday" and "ends the day before yesterday" cases can
/// be stated at all rather than being untestable timing.
/// </summary>
public class StreakCalculatorTests
{
    /// <summary>An arbitrary fixed "today" - nothing here depends on the real date.</summary>
    private static readonly DateOnly Today = new(2026, 9, 10);

    private static IEnumerable<DateOnly> ConsecutiveDaysEndingOn(DateOnly last, int count) =>
        Enumerable.Range(0, count).Select(offset => last.AddDays(-offset));

    [Fact]
    public void Compute_FiveConsecutiveDaysEndingToday_IsACurrentStreakOfFive()
    {
        var result = StreakCalculator.Compute(ConsecutiveDaysEndingOn(Today, 5), Today);

        Assert.Equal(5, result.Current);
        Assert.Equal(5, result.Longest);
        Assert.Equal(Today, result.LastQualifyingDay);
    }

    /// <summary>
    /// The rule that looks like an off-by-one and is not: today is not a missed day until it is
    /// over, so a run ending yesterday is still the run the household is on. Breaking it at
    /// midnight would punish someone for not having opened the app yet this morning.
    /// </summary>
    [Fact]
    public void Compute_FiveConsecutiveDaysEndingYesterday_IsStillACurrentStreakOfFive()
    {
        var yesterday = Today.AddDays(-1);

        var result = StreakCalculator.Compute(ConsecutiveDaysEndingOn(yesterday, 5), Today);

        Assert.Equal(5, result.Current);
        Assert.Equal(5, result.Longest);
        Assert.Equal(yesterday, result.LastQualifyingDay);
    }

    /// <summary>And the day after that, it really is broken - yesterday came and went with nothing.</summary>
    [Fact]
    public void Compute_FiveConsecutiveDaysEndingTheDayBeforeYesterday_IsBrokenButRemembered()
    {
        var dayBeforeYesterday = Today.AddDays(-2);

        var result = StreakCalculator.Compute(ConsecutiveDaysEndingOn(dayBeforeYesterday, 5), Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(5, result.Longest);
        Assert.Equal(dayBeforeYesterday, result.LastQualifyingDay);
    }

    /// <summary>
    /// A gap splits the input into runs: <c>Current</c> counts only the run that reaches today,
    /// while <c>Longest</c> remembers the bigger earlier one.
    /// </summary>
    [Fact]
    public void Compute_GapInTheMiddle_CountsOnlyTheRunTouchingTodayAsCurrent()
    {
        var days = ConsecutiveDaysEndingOn(Today, 2)                       // today and yesterday
            .Concat(ConsecutiveDaysEndingOn(Today.AddDays(-10), 6))        // a longer, older run
            .ToList();

        var result = StreakCalculator.Compute(days, Today);

        Assert.Equal(2, result.Current);
        Assert.Equal(6, result.Longest);
        Assert.Equal(Today, result.LastQualifyingDay);
    }

    [Fact]
    public void Compute_DuplicateDays_CollapseToOne()
    {
        var days = new[]
        {
            Today, Today, Today,
            Today.AddDays(-1), Today.AddDays(-1)
        };

        var result = StreakCalculator.Compute(days, Today);

        Assert.Equal(2, result.Current);
        Assert.Equal(2, result.Longest);
    }

    [Fact]
    public void Compute_UnsortedInput_GivesTheSameAnswerAsSorted()
    {
        var sorted = ConsecutiveDaysEndingOn(Today, 4).OrderBy(day => day).ToList();
        var shuffled = new[] { sorted[2], sorted[0], sorted[3], sorted[1] };

        var fromSorted = StreakCalculator.Compute(sorted, Today);
        var fromShuffled = StreakCalculator.Compute(shuffled, Today);

        Assert.Equal(fromSorted, fromShuffled);
    }

    [Fact]
    public void Compute_EmptyInput_IsZeroWithNoLastDay()
    {
        var result = StreakCalculator.Compute([], Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
        Assert.Null(result.LastQualifyingDay);
    }

    [Fact]
    public void Compute_SingleDayToday_IsACurrentStreakOfOne()
    {
        var result = StreakCalculator.Compute([Today], Today);

        Assert.Equal(1, result.Current);
        Assert.Equal(1, result.Longest);
        Assert.Equal(Today, result.LastQualifyingDay);
    }

    /// <summary>
    /// A day after <c>today</c> cannot be a qualifying day on the viewer's own calendar, so it is
    /// dropped rather than allowed to extend a streak or a "longest ever" - the defensive half of
    /// the normalization, for a clock skew or a seeded row.
    /// </summary>
    [Fact]
    public void Compute_FutureDays_AreIgnored()
    {
        var days = new[] { Today.AddDays(2), Today.AddDays(1), Today, Today.AddDays(-1) };

        var result = StreakCalculator.Compute(days, Today);

        Assert.Equal(2, result.Current);
        Assert.Equal(2, result.Longest);
        Assert.Equal(Today, result.LastQualifyingDay);
    }

    /// <summary>
    /// A run that ends in the future's direction is not the only thing skew could do: a set made up
    /// entirely of future days has nothing to count at all, and must not report a streak.
    /// </summary>
    [Fact]
    public void Compute_OnlyFutureDays_IsEmpty()
    {
        var result = StreakCalculator.Compute([Today.AddDays(1), Today.AddDays(2)], Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
        Assert.Null(result.LastQualifyingDay);
    }

    /// <summary>
    /// A month boundary is where naive day arithmetic (comparing day-of-month numbers, say) breaks;
    /// <c>DateOnly.AddDays</c> handles it, and this pins that the scan uses it.
    /// </summary>
    [Fact]
    public void Compute_RunAcrossAMonthBoundary_IsUnbroken()
    {
        var today = new DateOnly(2026, 3, 2);
        var days = new[]
        {
            new DateOnly(2026, 2, 27),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 1),
            today
        };

        var result = StreakCalculator.Compute(days, today);

        Assert.Equal(4, result.Current);
        Assert.Equal(4, result.Longest);
    }
}
