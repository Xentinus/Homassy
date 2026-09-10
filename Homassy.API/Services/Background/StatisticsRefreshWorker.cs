using Homassy.API.Context;
using Homassy.API.Models.Statistics;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Services.Background;

/// <summary>
/// Hosted service that refreshes the global statistics cache once on startup,
/// then nightly at 02:00 UTC.
/// </summary>
public sealed class StatisticsRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StatisticsService _statisticsService;

    // Target hour (UTC) for the nightly refresh.
    private const int RefreshHourUtc = 2;

    public StatisticsRefreshWorker(
        IServiceScopeFactory scopeFactory,
        StatisticsService statisticsService)
    {
        _scopeFactory = scopeFactory;
        _statisticsService = statisticsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Statistics refresh worker started");

        // Warm the cache immediately so the first request is never served a zero response.
        await RefreshAsync(stoppingToken);
        await PreWarmFamilyInsightsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = ComputeDelayUntilNextRefresh();
                Log.Information(
                    "Statistics refresh worker: next refresh in {Hours:F1} h (at ~02:00 UTC)",
                    delay.TotalHours);

                await Task.Delay(delay, stoppingToken);
                await RefreshAsync(stoppingToken);
                await PreWarmFamilyInsightsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Statistics refresh worker encountered an error; retrying in 5 minutes");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Statistics refresh worker stopped");
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Log.Information("Statistics refresh worker: calculating global statistics...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

            var products = await context.Products.LongCountAsync(cancellationToken);
            var inventoryItems = await context.ProductInventoryItems.LongCountAsync(cancellationToken);
            var shoppingLists = await context.ShoppingLists.LongCountAsync(cancellationToken);
            var purchasedItems = await context.ShoppingListItems
                .Where(i => i.PurchasedAt != null)
                .LongCountAsync(cancellationToken);
            var shoppingLocations = await context.ShoppingLocations.LongCountAsync(cancellationToken);
            var storageLocations = await context.StorageLocations.LongCountAsync(cancellationToken);

            var statistics = new GlobalStatisticsResponse
            {
                TotalProducts = products,
                TotalInventoryItems = inventoryItems,
                TotalShoppingLists = shoppingLists,
                TotalPurchasedItems = purchasedItems,
                TotalShoppingLocations = shoppingLocations,
                TotalStorageLocations = storageLocations,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _statisticsService.UpdateStatistics(statistics);

            Log.Information(
                "Statistics cache updated — products: {Products}, inventory: {Inventory}, " +
                "lists: {Lists}, purchased: {Purchased}, shopping locations: {SL}, storage locations: {StL}",
                products, inventoryItems, shoppingLists, purchasedItems, shoppingLocations, storageLocations);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Statistics refresh worker: failed to calculate global statistics");
        }
    }

    /// <summary>
    /// How recently a family must have done something to be worth pre-warming. The point of the
    /// cutoff is that the cost of this pass scales with <em>active</em> families rather than with
    /// every family row ever created - a household that stopped using the app a year ago gains
    /// nothing from a warm cache, and paying for it every night is how a nightly job quietly turns
    /// into a problem.
    /// </summary>
    private const int ActiveFamilyWindowDays = 90;

    /// <summary>
    /// Which scoreboard window gets pre-warmed. One, not all three: the entry is keyed by window
    /// (see <c>InsightFunctions</c>'s scoreboard key), so warming every option would multiply the
    /// work and the memory for windows most families never open. 30 days is the endpoint's default
    /// and by far the most requested.
    /// </summary>
    private const int PreWarmedScoreboardDays = 30;

    /// <summary>
    /// Pre-computes the family scoreboard for every recently-active family, so the first member to
    /// open the insight page after the nightly refresh is not the one who pays for the
    /// aggregation.
    ///
    /// <para>
    /// <b>Wrapped in its own try/catch, and awaited after the global refresh rather than beside
    /// it.</b> The global statistics pass has worked for releases and serves a public endpoint; a
    /// failure in this newer, heavier pass must not be able to take it down with it. The same
    /// reasoning applies per family inside the loop: one family whose data trips an aggregation is
    /// logged and skipped, not allowed to abandon the rest of the sweep.
    /// </para>
    ///
    /// <para>
    /// It calls the ordinary <c>GetFamilyScoreboardAsync</c> rather than computing anything of its
    /// own, so a warmed entry is byte-identical to what a request would have produced and there is
    /// exactly one implementation of the aggregation to keep correct. That method writes through
    /// <c>FamilyInsightsCache</c> itself, which is what makes this a pre-warm rather than a
    /// throwaway computation.
    /// </para>
    /// </summary>
    public async Task PreWarmFamilyInsightsAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HomassyDbContext>>();
            var insightFunctions = scope.ServiceProvider.GetRequiredService<Functions.InsightFunctions>();

            List<ActiveFamily> activeFamilies;
            using (var context = contextFactory.CreateForReading())
            {
                var activeSince = DateTime.UtcNow.AddDays(-ActiveFamilyWindowDays);

                // One query for the whole sweep: the families with recent activity, each paired
                // with one of their members. The member is needed because the aggregation resolves
                // the timezone (and therefore the day bucketing of both streaks) from a user's own
                // profile - so pre-warming has to warm the entry a real member would ask for, not a
                // UTC-shaped one no request would ever hit.
                activeFamilies = await context.Activities
                    .Where(a => a.FamilyId != null && a.Timestamp >= activeSince)
                    .GroupBy(a => a.FamilyId!.Value)
                    .Select(g => new ActiveFamily(g.Key, g.Min(a => a.UserId)))
                    .ToListAsync(cancellationToken);
            }

            if (activeFamilies.Count == 0)
            {
                Log.Information("Statistics refresh worker: no families active in the last {Days} days, nothing to pre-warm", ActiveFamilyWindowDays);
                return;
            }

            var warmed = 0;
            foreach (var family in activeFamilies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await insightFunctions.GetFamilyScoreboardAsync(family.UserId, family.FamilyId, PreWarmedScoreboardDays, cancellationToken);
                    warmed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Statistics refresh worker: failed to pre-warm the scoreboard for family {FamilyId}", family.FamilyId);
                }
            }

            Log.Information(
                "Statistics refresh worker: pre-warmed {Warmed}/{Total} active family scoreboards in {Elapsed:F1}s",
                warmed, activeFamilies.Count, (DateTime.UtcNow - startedAt).TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Statistics refresh worker: family insight pre-warm failed");
        }
    }

    /// <summary>
    /// One family worth pre-warming, plus a member of it whose profile supplies the timezone the
    /// aggregation is computed in - see <see cref="PreWarmFamilyInsightsAsync"/>.
    /// </summary>
    private sealed record ActiveFamily(int FamilyId, int UserId);

    /// <summary>
    /// Computes a <see cref="TimeSpan"/> until the next occurrence of 02:00 UTC.
    /// Always returns a positive delay of at most 24 hours.
    /// </summary>
    private static TimeSpan ComputeDelayUntilNextRefresh()
    {
        var utcNow = DateTime.UtcNow;
        var nextRun = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, RefreshHourUtc, 0, 0, DateTimeKind.Utc);

        if (nextRun <= utcNow)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - utcNow;
    }
}
