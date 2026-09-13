using Homassy.Notifications.Configuration;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// The polling loop every worker in this service runs (#95).
/// </summary>
/// <remarks>
/// There were eight copies of <c>while</c> + <c>try</c> + <c>Task.Delay(interval)</c> +
/// <c>catch</c> + <c>Task.Delay(backoff)</c>, which meant any fix to the loop had to be made eight
/// times - and the count was growing, since each new worker copied it again. Everything about the
/// cadence lives here now, and a worker supplies only <see cref="DoWorkAsync"/>.
/// <para>
/// Three things the copies did not do:
/// </para>
/// <list type="bullet">
/// <item>
/// <b><see cref="PeriodicTimer"/> rather than sleeping after the work.</b> A delay that starts when
/// the work finishes makes the effective period <c>work duration + interval</c>, so the workers
/// drifted against the wall clock. A timer schedules on the period instead.
/// </item>
/// <item>
/// <b>Startup jitter.</b> Eight workers starting together swept the database in the same instant on
/// every container start.
/// </item>
/// <item>
/// <b>Backoff that escalates.</b> A fixed error delay retries a broken dependency at the same rate
/// forever. This doubles up to a configured ceiling and resets on the first cycle that succeeds.
/// </item>
/// </list>
/// </remarks>
public abstract class PeriodicWorkerService : BackgroundService
{
    private readonly WorkerSchedule _schedule;
    private int _consecutiveFailures;

    protected PeriodicWorkerService(WorkerSchedule schedule)
    {
        _schedule = schedule;
    }

    /// <summary>Name used in the start, stop and failure log lines.</summary>
    protected abstract string WorkerName { get; }

    /// <summary>
    /// Whether the first cycle runs at startup rather than one interval later. Set per worker to
    /// whatever it did before this base class existed: the hourly and five-minute sweeps ran
    /// immediately, the short-interval monitors waited for their first tick because they report on
    /// what happened since the last one.
    /// </summary>
    protected virtual bool RunOnStartup => false;

    /// <summary>One cycle. Exceptions are caught by the loop and turned into a backoff.</summary>
    protected abstract Task DoWorkAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("{Worker} started (interval {Interval})", WorkerName, _schedule.Interval);

        try
        {
            await ApplyStartupJitterAsync(stoppingToken);

            // The timer is started before the startup cycle, not after it, so the period is
            // anchored to the start rather than to however long that cycle took.
            using var timer = new PeriodicTimer(_schedule.Interval);

            if (RunOnStartup)
            {
                await RunCycleAsync(stoppingToken);
            }

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping, not failing.
        }

        Log.Information("{Worker} stopped", WorkerName);
    }

    /// <summary>
    /// Runs one cycle, and on failure waits out the backoff before returning so the next tick is
    /// pushed back rather than arriving on schedule into a dependency that is still down.
    /// </summary>
    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await DoWorkAsync(stoppingToken);
            _consecutiveFailures = 0;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            var backoff = CurrentBackoff();

            Log.Error(ex, "Error in {Worker} (consecutive failure {Count}), backing off for {Backoff}",
                WorkerName, _consecutiveFailures, backoff);

            await Task.Delay(backoff, stoppingToken);
        }
    }

    /// <summary>
    /// The first failure waits the configured backoff, each further one doubles it, and the
    /// ceiling is the configured maximum. Computed from the failure count rather than accumulated,
    /// so a reset is one assignment and cannot drift.
    /// </summary>
    private TimeSpan CurrentBackoff()
    {
        var doublings = Math.Min(_consecutiveFailures - 1, 30);
        var seconds = _schedule.ErrorBackoff.TotalSeconds * Math.Pow(2, doublings);

        return seconds >= _schedule.MaxErrorBackoff.TotalSeconds
            ? _schedule.MaxErrorBackoff
            : TimeSpan.FromSeconds(seconds);
    }

    private async Task ApplyStartupJitterAsync(CancellationToken stoppingToken)
    {
        var window = _schedule.StartupJitter;
        if (window <= TimeSpan.Zero)
        {
            return;
        }

        var delay = TimeSpan.FromMilliseconds(Random.Shared.Next((int)window.TotalMilliseconds + 1));
        if (delay > TimeSpan.Zero)
        {
            Log.Debug("{Worker} waiting {Delay} of startup jitter", WorkerName, delay);
            await Task.Delay(delay, stoppingToken);
        }
    }
}
