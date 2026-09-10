using Homassy.API.Context;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Services.Background;

/// <summary>
/// Writes <see cref="LastSeenTracker"/>'s in-memory stamps to <c>UserProfiles.LastSeenAt</c> in
/// batches (#127), so the request path never does it.
/// </summary>
/// <remarks>
/// Follows <see cref="StatisticsRefreshWorker"/>'s structure for the loop, the cancellation
/// handling and the error backoff, so all the background services in this project fail the same
/// way. The one behaviour worth stating: a flush that fails <b>drops that batch</b> rather than
/// retrying it. The tracker has already handed the stamps over and moved on, and re-queueing them
/// would mean either holding a failed batch indefinitely or writing a stamp older than one that
/// arrived since. Losing a few minutes of last-seen precision is exactly the kind of loss this
/// value is allowed to take - see <c>UserProfile.LastSeenAt</c>.
/// </remarks>
public sealed class LastSeenFlushService : BackgroundService
{
    /// <summary>
    /// How often pending stamps are written. Long enough that the write volume is negligible, short
    /// enough that a user's last-seen is never far behind - and, in the case that actually matters
    /// (a first launch on a new device), the value being minutes stale only widens the delta window
    /// slightly.
    /// </summary>
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How many users one <c>UPDATE</c> covers. Chunked so a burst of activity cannot build a
    /// single statement with thousands of parameters in its <c>IN</c> list - the same reason every
    /// other query in this milestone is bounded.
    /// </summary>
    private const int FlushChunkSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LastSeenTracker _tracker;

    public LastSeenFlushService(IServiceScopeFactory scopeFactory, LastSeenTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Last-seen flush service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(FlushInterval, stoppingToken);
                await FlushAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Last-seen flush service encountered an error; retrying in 1 minute");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        // One last flush on the way down, so a graceful shutdown does not throw away the stamps
        // collected since the last tick. Best-effort: the drain period is short and the host is
        // already stopping, so a failure here is logged and ignored rather than delaying exit.
        try
        {
            await FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Last-seen flush service: the final flush on shutdown failed");
        }

        Log.Information("Last-seen flush service stopped");
    }

    /// <summary>
    /// Drains the tracker and writes what it got, one batched <c>ExecuteUpdateAsync</c> per chunk -
    /// never a round trip per user.
    /// </summary>
    /// <remarks>
    /// <c>ExecuteUpdateAsync</c> also means no entities are loaded, tracked or saved: the flush
    /// never materialises a <c>UserProfile</c>, so it cannot overwrite a field somebody else
    /// changed in the meantime, and it does not disturb <c>RecordChange</c> the way a tracked save
    /// would.
    /// <para>
    /// The stamps are grouped by <em>timestamp</em> first and chunked within each group, so every
    /// statement writes one exact instant to the users it belongs to. Grouping the other way round
    /// (a chunk of users, one representative time) would round a whole batch to one moment and make
    /// every last-seen in it slightly wrong; grouping this way costs one statement per distinct
    /// instant, which is bounded by how many users were seen in the same tick.
    /// </para>
    /// </remarks>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var pending = _tracker.DrainPending();
        if (pending.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HomassyDbContext>>();
        using var context = contextFactory.CreateDbContext();

        var written = 0;

        // Grouped by the stamp itself: users seen at the same instant share one statement, and each
        // user's stored value stays exactly what was recorded for them.
        foreach (var group in pending.GroupBy(entry => entry.Value))
        {
            var seenAt = group.Key;
            var userIds = group.Select(entry => entry.Key).ToList();

            foreach (var chunk in userIds.Chunk(FlushChunkSize))
            {
                written += await context.UserProfiles
                    .Where(profile => chunk.Contains(profile.UserId))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.LastSeenAt, seenAt), cancellationToken);
            }
        }

        Log.Debug("Last-seen flush service: wrote {Written} last-seen stamps for {Users} users", written, pending.Count);
    }
}
