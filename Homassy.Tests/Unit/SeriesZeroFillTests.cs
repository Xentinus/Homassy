using Homassy.API.Functions;

namespace Homassy.Tests.Unit;

public class SeriesZeroFillTests
{
    /// <summary>
    /// A real Monday, derived from an arbitrary anchor date rather than hardcoded, so the test
    /// does not depend on anyone mentally verifying a specific year's calendar. Every week-bucket
    /// test below builds its own window from this so "the first bucket is a Monday" is a fact the
    /// test establishes itself, not an assumption baked into a magic date literal.
    /// </summary>
    private static DateOnly AMonday()
    {
        var anchor = new DateOnly(2026, 3, 4);
        var daysSinceMonday = ((int)anchor.DayOfWeek + 6) % 7;
        var monday = anchor.AddDays(-daysSinceMonday);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek); // sanity-check the helper itself
        return monday;
    }

    [Fact]
    public void Densify_SparseInputWithThreeDayHole_FillsTheHoleWithThreeZeroPoints()
    {
        var from = new DateOnly(2026, 3, 1);
        var to = new DateOnly(2026, 3, 10);
        var sparse = new[]
        {
            new SeriesPoint { Bucket = new DateOnly(2026, 3, 2), Value = 10m },
            new SeriesPoint { Bucket = new DateOnly(2026, 3, 6), Value = 20m } // 03-03, 04, 05 are the hole
        };

        var result = SeriesZeroFill.Densify(sparse, from, to, SeriesBucket.Day);

        Assert.Equal(10, result.Count); // one point per day, 03-01..03-10 inclusive
        Assert.Equal(10m, result.Single(p => p.Bucket == new DateOnly(2026, 3, 2)).Value);
        Assert.Equal(20m, result.Single(p => p.Bucket == new DateOnly(2026, 3, 6)).Value);

        var hole = new[] { new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5) };
        foreach (var day in hole)
        {
            Assert.Equal(0m, result.Single(p => p.Bucket == day).Value);
        }
    }

    [Fact]
    public void Densify_EmptyInputOverThirtyDayWindow_YieldsExactlyThirtyZeroPoints()
    {
        var to = new DateOnly(2026, 6, 15);
        var from = to.AddDays(-29); // 30 calendar days inclusive of both ends

        var result = SeriesZeroFill.Densify([], from, to, SeriesBucket.Day);

        // Not an empty list: a chart has to draw a flat line at zero, not nothing at all.
        Assert.Equal(30, result.Count);
        Assert.All(result, p => Assert.Equal(0m, p.Value));
        Assert.Equal(from, result[0].Bucket);
        Assert.Equal(to, result[^1].Bucket);
    }

    [Fact]
    public void Densify_WeekBucketing_SnapsToMondayAndYieldsCeilingOfDaysOverSeven()
    {
        var from = AMonday();
        var to = from.AddDays(29); // a 30-day window -> ceil(30/7) = 5 ISO weeks

        // A point on a Thursday inside the first week must be attributed to that week's Monday,
        // not tracked as its own (non-Monday) bucket - this is the "snaps to Monday" behaviour.
        var sparse = new[] { new SeriesPoint { Bucket = from.AddDays(3), Value = 42m } };

        var result = SeriesZeroFill.Densify(sparse, from, to, SeriesBucket.Week);

        Assert.Equal(5, result.Count);
        Assert.All(result, p => Assert.Equal(DayOfWeek.Monday, p.Bucket.DayOfWeek));
        Assert.Equal(from, result[0].Bucket);
        Assert.Equal(42m, result[0].Value); // the Thursday value landed on its week's Monday
    }

    [Fact]
    public void Densify_PointOutsideFromTo_IsDroppedRatherThanWideningTheWindow()
    {
        var from = new DateOnly(2026, 4, 10);
        var to = new DateOnly(2026, 4, 15);
        var sparse = new[]
        {
            new SeriesPoint { Bucket = from.AddDays(-1), Value = 999m }, // just before the window
            new SeriesPoint { Bucket = to.AddDays(1), Value = 888m },    // just after the window
            new SeriesPoint { Bucket = new DateOnly(2026, 4, 12), Value = 5m } // legitimately inside
        };

        var result = SeriesZeroFill.Densify(sparse, from, to, SeriesBucket.Day);

        // Still exactly the requested window - neither stray point widened it.
        Assert.Equal(6, result.Count);
        Assert.Equal(from, result[0].Bucket);
        Assert.Equal(to, result[^1].Bucket);
        Assert.DoesNotContain(result, p => p.Value == 999m);
        Assert.DoesNotContain(result, p => p.Value == 888m);
        Assert.Equal(5m, result.Single(p => p.Bucket == new DateOnly(2026, 4, 12)).Value);
    }

    [Fact]
    public void Densify_OutputIsOrderedAscendingWithNoDuplicateBuckets()
    {
        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 5, 8);

        // Deliberately unordered, to prove the output's own ordering doesn't just mirror input order.
        var sparse = new[]
        {
            new SeriesPoint { Bucket = new DateOnly(2026, 5, 8), Value = 3m },
            new SeriesPoint { Bucket = new DateOnly(2026, 5, 1), Value = 1m },
            new SeriesPoint { Bucket = new DateOnly(2026, 5, 4), Value = 2m }
        };

        var result = SeriesZeroFill.Densify(sparse, from, to, SeriesBucket.Day);

        Assert.Equal(result.OrderBy(p => p.Bucket).Select(p => p.Bucket), result.Select(p => p.Bucket));
        Assert.Equal(result.Count, result.Select(p => p.Bucket).Distinct().Count());
    }

    /// <summary>
    /// Fix round 1, Critical defect 1 - first consequence: a window that does not open on a Monday
    /// must not lose its own first day's consumption. Before this fix, <c>Densify</c> snapped a
    /// sparse point down to its ISO week's Monday <em>before</em> comparing it against
    /// <c>[from, to]</c> - so an activity on the window's own first day (here, a Wednesday) would
    /// snap to the Monday two days earlier, which falls before <c>from</c> itself, and get dropped
    /// entirely: the whole first partial week's genuine consumption silently zeroed. Every week-
    /// bucket test above this one uses a Monday as <c>from</c>, which is exactly why none of them
    /// caught this - a Monday's own ISO-week Monday is itself, so the pre-fix snap-then-compare was
    /// always a no-op there. This test deliberately starts off-Monday to exercise the case those
    /// cannot.
    /// </summary>
    [Fact]
    public void Densify_WeekBucketWindowNotStartingOnMonday_FirstDayActivityAppearsInFirstWeeksValue()
    {
        var monday = AMonday();
        var from = monday.AddDays(2); // a Wednesday - the window's own first day, deliberately off-Monday.
        var to = from.AddDays(29);
        var sparse = new[] { new SeriesPoint { Bucket = from, Value = 42m } }; // activity on day 1 of the window

        var result = SeriesZeroFill.Densify(sparse, from, to, SeriesBucket.Week);

        // The first week bucket is still labelled with the real ISO Monday (a partial first week
        // keeps that label rather than shifting to line up with `from` - see BuildBucketStarts's
        // remarks) - and now correctly carries the Wednesday activity's value instead of losing it.
        Assert.Equal(monday, result[0].Bucket);
        Assert.Equal(42m, result[0].Value);
    }

    /// <summary>
    /// Fix round 1, Critical defect 1 - second consequence: an activity recorded one day after the
    /// window ends must never appear anywhere in the response, even though the UTC padding upstream
    /// (in <c>InsightFunctions.ComputeConsumptionSeriesAsync</c>) deliberately admits it past the
    /// raw window filter. Before this fix, that point's ISO week Monday could still fall inside
    /// <c>[from, to]</c> whenever <c>to</c> itself is not a Sunday (as here) even though the point
    /// itself is past <c>to</c> - so it passed straight through into the last week's bucket. This
    /// test starts off-Monday too, for the same reason as the test above.
    /// </summary>
    [Fact]
    public void Densify_WeekBucketActivityOneDayAfterWindowEnds_DoesNotAppearAnywhereInResult()
    {
        var monday = AMonday();
        var from = monday.AddDays(3); // a Thursday - deliberately off-Monday too (see the test above).
        var to = from.AddDays(29); // a Friday - not the last day of its own ISO week, which is what
                                    // lets the leak below actually manifest against the pre-fix code.
        var sparse = new[] { new SeriesPoint { Bucket = to.AddDays(1), Value = 999m } }; // one day past the window

        var result = SeriesZeroFill.Densify(sparse, from, to, SeriesBucket.Week);

        Assert.DoesNotContain(result, p => p.Value == 999m);
        Assert.Equal(0m, result.Sum(p => p.Value)); // must not appear under any bucket, not just its own.
    }
}
