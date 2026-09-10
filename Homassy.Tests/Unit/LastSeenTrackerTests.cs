using Homassy.API.Services;

namespace Homassy.Tests.Unit;

/// <summary>
/// <see cref="LastSeenTracker"/>'s three promises: the later stamp wins, a drain empties the
/// tracker exactly once, and a stamp arriving during a drain is never lost. All three are pure
/// in-memory facts - the tracker deliberately has no clock and no database, which is what makes
/// them testable at all.
/// </summary>
public class LastSeenTrackerTests
{
    private static readonly DateTime Noon = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Stamp_TwiceForOneUser_KeepsTheLaterTimestamp()
    {
        var tracker = new LastSeenTracker();

        tracker.Stamp(1, Noon);
        tracker.Stamp(1, Noon.AddMinutes(5));

        var pending = tracker.DrainPending();

        Assert.Equal(Noon.AddMinutes(5), Assert.Single(pending).Value);
    }

    /// <summary>
    /// The half that is easy to get wrong: requests do not arrive in timestamp order, so an
    /// out-of-order stamp must not be able to move last-seen backwards.
    /// </summary>
    [Fact]
    public void Stamp_OutOfOrder_DoesNotMoveTheTimestampBackwards()
    {
        var tracker = new LastSeenTracker();

        tracker.Stamp(1, Noon.AddMinutes(5));
        tracker.Stamp(1, Noon);

        Assert.Equal(Noon.AddMinutes(5), Assert.Single(tracker.DrainPending()).Value);
    }

    [Fact]
    public void DrainPending_ReturnsEveryStampAndLeavesTheTrackerEmpty()
    {
        var tracker = new LastSeenTracker();

        tracker.Stamp(1, Noon);
        tracker.Stamp(2, Noon.AddSeconds(1));
        tracker.Stamp(3, Noon.AddSeconds(2));

        var first = tracker.DrainPending();
        var second = tracker.DrainPending();

        Assert.Equal(3, first.Count);
        Assert.Empty(second);
    }

    /// <summary>
    /// The reason <c>DrainPending</c> swaps the dictionary instead of iterating and removing: a
    /// stamp that arrives while a drain is in flight has to end up in one drain or the next, never
    /// dropped between them. Simulated by holding the drained snapshot and stamping afterwards -
    /// which is exactly the interleaving a request hitting mid-flush produces.
    /// </summary>
    [Fact]
    public void Stamp_ArrivingAfterADrain_AppearsInTheNextDrain()
    {
        var tracker = new LastSeenTracker();
        tracker.Stamp(1, Noon);

        var firstDrain = tracker.DrainPending();
        tracker.Stamp(1, Noon.AddMinutes(1));

        var secondDrain = tracker.DrainPending();

        Assert.Equal(Noon, Assert.Single(firstDrain).Value);
        Assert.Equal(Noon.AddMinutes(1), Assert.Single(secondDrain).Value);
    }

    /// <summary>
    /// The concurrency case from the brief: 10 000 stamps across 100 users from 8 tasks must
    /// produce 100 entries and no exception - the tracker sits on the request path of a singleton
    /// service, so it is hit by arbitrary parallel requests.
    /// </summary>
    [Fact]
    public async Task Stamp_UnderConcurrency_KeepsOneEntryPerUserAndNeverThrows()
    {
        var tracker = new LastSeenTracker();
        const int users = 100;
        const int stampsPerTask = 10_000 / 8;

        var tasks = Enumerable.Range(0, 8).Select(taskIndex => Task.Run(() =>
        {
            for (var i = 0; i < stampsPerTask; i++)
            {
                tracker.Stamp(i % users, Noon.AddSeconds(taskIndex));
            }
        }));

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));

        Assert.Null(exception);

        var pending = tracker.DrainPending();
        Assert.Equal(users, pending.Count);
        // Every stored value is one of the stamps that was actually written, and the latest one
        // wins - so the max across the tasks is what every user ends up with.
        Assert.All(pending, entry => Assert.Equal(Noon.AddSeconds(7), entry.Value));
    }

    /// <summary>
    /// A drained dictionary is a snapshot the caller owns: further stamps must not mutate it behind
    /// the flush service's back while it is writing.
    /// </summary>
    [Fact]
    public void DrainPending_ReturnsASnapshotLaterStampsDoNotMutate()
    {
        var tracker = new LastSeenTracker();
        tracker.Stamp(1, Noon);

        var drained = tracker.DrainPending();
        tracker.Stamp(1, Noon.AddHours(1));
        tracker.Stamp(2, Noon.AddHours(1));

        Assert.Equal(Noon, Assert.Single(drained).Value);
    }

    [Fact]
    public void DrainPending_WithNothingPending_IsEmpty()
    {
        Assert.Empty(new LastSeenTracker().DrainPending());
    }

    /// <summary>
    /// The stamp is stored as UTC whatever kind it arrives as, so a value read back out and written
    /// to a <c>timestamp with time zone</c> column cannot be reinterpreted.
    /// </summary>
    [Fact]
    public void Stamp_UnspecifiedKind_IsStoredAsUtc()
    {
        var tracker = new LastSeenTracker();

        tracker.Stamp(1, new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(DateTimeKind.Utc, Assert.Single(tracker.DrainPending()).Value.Kind);
    }
}
