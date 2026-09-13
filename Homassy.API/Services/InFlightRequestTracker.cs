namespace Homassy.API.Services;

/// <summary>
/// Counts the requests currently in the pipeline, so shutdown can say what it is waiting for.
/// </summary>
/// <remarks>
/// This is diagnostics, not the drain mechanism. The drain itself is
/// <see cref="Microsoft.Extensions.Hosting.HostOptions.ShutdownTimeout"/>: Kestrel stops
/// accepting connections on the stop signal and waits for the requests already in flight, which
/// is precisely the behaviour a hand-rolled wait would be trying to reproduce — and a hand-rolled
/// wait cannot work anyway, because every callback it could run from fires while the server is
/// still accepting. What the counter adds is the answer to "did the drain finish, or did the
/// timeout cut it off", which is otherwise invisible from the outside.
///
/// Registered as a singleton; every member is safe to call from any thread.
/// </remarks>
public sealed class InFlightRequestTracker
{
    private int _count;
    private long _drainStartedTicks = -1;

    /// <summary>Requests currently between entering and leaving the pipeline.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>True once <see cref="BeginDraining"/> has been called.</summary>
    public bool IsDraining => Volatile.Read(ref _drainStartedTicks) >= 0;

    /// <summary>
    /// Raised once per drain, the moment the count reaches zero after <see cref="BeginDraining"/>,
    /// with how long that took. An idle process raises it from <see cref="BeginDraining"/> itself,
    /// with a duration of about nothing.
    /// </summary>
    public event Action<TimeSpan>? Drained;

    /// <summary>Marks a request as entering the pipeline. Pair with <see cref="Exit"/>.</summary>
    public void Enter() => Interlocked.Increment(ref _count);

    /// <summary>Marks a request as having left the pipeline, whether it succeeded or threw.</summary>
    public void Exit()
    {
        if (Interlocked.Decrement(ref _count) == 0)
        {
            RaiseDrainedIfDraining();
        }
    }

    /// <summary>
    /// Starts the drain window and returns the number of requests still in flight. Zero means
    /// there is nothing to wait for and the process can stop immediately.
    /// </summary>
    public int BeginDraining()
    {
        Interlocked.CompareExchange(ref _drainStartedTicks, DateTime.UtcNow.Ticks, -1);

        var inFlight = Count;

        if (inFlight == 0)
        {
            RaiseDrainedIfDraining();
        }

        return inFlight;
    }

    private void RaiseDrainedIfDraining()
    {
        // -1 means "not draining"; anything else is the start instant, and swapping it back to
        // -1 is what makes the event fire exactly once per drain.
        var startedTicks = Interlocked.Exchange(ref _drainStartedTicks, -1);

        if (startedTicks < 0)
        {
            return;
        }

        Drained?.Invoke(TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startedTicks));
    }
}
