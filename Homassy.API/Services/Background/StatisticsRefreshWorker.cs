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
