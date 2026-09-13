using Homassy.API.Services;

namespace Homassy.Tests.Unit;

/// <summary>
/// The counter behind the shutdown log lines: how many requests a stop is waiting for, and when
/// they finished (#85).
/// </summary>
public class InFlightRequestTrackerTests
{
    [Fact]
    public void Count_TracksEnterAndExit()
    {
        var tracker = new InFlightRequestTracker();

        Assert.Equal(0, tracker.Count);

        tracker.Enter();
        tracker.Enter();
        Assert.Equal(2, tracker.Count);

        tracker.Exit();
        Assert.Equal(1, tracker.Count);

        tracker.Exit();
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void BeginDraining_OnAnIdleTracker_ReportsNothingToWaitFor()
    {
        var tracker = new InFlightRequestTracker();
        var drains = 0;
        tracker.Drained += _ => drains++;

        var inFlight = tracker.BeginDraining();

        Assert.Equal(0, inFlight);
        Assert.Equal(1, drains);
    }

    [Fact]
    public void Drained_IsRaisedOnceWhenTheLastRequestLeaves()
    {
        var tracker = new InFlightRequestTracker();
        var drains = 0;
        tracker.Drained += _ => drains++;

        tracker.Enter();
        tracker.Enter();

        Assert.Equal(2, tracker.BeginDraining());
        Assert.Equal(0, drains);

        tracker.Exit();
        Assert.Equal(0, drains);

        tracker.Exit();
        Assert.Equal(1, drains);

        // A request that arrives after the drain finished must not raise it a second time.
        tracker.Enter();
        tracker.Exit();
        Assert.Equal(1, drains);
    }

    [Fact]
    public void Count_IsExactUnderParallelEnterAndExit()
    {
        var tracker = new InFlightRequestTracker();

        Parallel.For(0, 1000, _ =>
        {
            tracker.Enter();
            tracker.Exit();
        });

        Assert.Equal(0, tracker.Count);

        Parallel.For(0, 1000, _ => tracker.Enter());

        Assert.Equal(1000, tracker.Count);
    }
}
