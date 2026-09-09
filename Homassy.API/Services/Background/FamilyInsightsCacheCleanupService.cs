using Serilog;

namespace Homassy.API.Services.Background;

/// <summary>
/// Periodically sweeps expired entries out of <see cref="FamilyInsightsCache"/>. Nothing else
/// ever removes an entry from that cache just because its TTL elapsed - see
/// <see cref="FamilyInsightsCache.CleanupExpiredEntries"/> - so without this service a key that
/// is never queried again (a product nobody re-opens, a date range nobody re-requests) would sit
/// in that dictionary, unreclaimed, for the rest of the process's life. Modelled on
/// <see cref="RateLimitCleanupService"/>'s own loop shape: delay, sweep, log; on failure, log and
/// let the same delay before the next iteration act as the backoff.
/// </summary>
public sealed class FamilyInsightsCacheCleanupService : BackgroundService
{
    private readonly FamilyInsightsCache _cache;

    // FamilyInsightsCache's callers use 5- and 15-minute TTLs. Sweeping every 5 minutes - the
    // shorter of the two - bounds how long a 5-minute entry can sit expired-but-unswept to at
    // most one interval, and a 15-minute entry to at most a third of its own TTL, without waking
    // the process any more often than the shortest-lived entries actually need. Same
    // interval-relative-to-lifetime relationship RateLimitCleanupService uses between its own
    // 1-hour sweep and the 2-hour bucket age it cleans up.
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    public FamilyInsightsCacheCleanupService(FamilyInsightsCache cache)
    {
        _cache = cache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Family insights cache cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
                var removed = _cache.CleanupExpiredEntries();
                Log.Information("Family insights cache cleanup completed: removed {Removed} expired entr{Suffix}", removed, removed == 1 ? "y" : "ies");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in family insights cache cleanup");
            }
        }
    }
}
