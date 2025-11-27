using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Functions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Services
{
    public class CacheManagementService : BackgroundService
    {
        private static int _lastProcessedId;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Cache management service starting...");

            try
            {
                await InitializeLastProcessedIdAsync();
                await InitializeCachesAsync();

                Log.Information("Cache management service started successfully");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        await ProcessTableChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing table changes");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing cache management service");
            }

            Log.Information("Cache management service stopped");
        }

        private static async Task InitializeCachesAsync()
        {
            try
            {
                Log.Information("Initializing caches...");

                await new UserFunctions().InitializeCacheAsync();
                await new FamilyFunctions().InitializeCacheAsync();
                await new ProductFunctions().InitializeCacheAsync();
                await new LocationFunctions().InitializeCacheAsync();

                Log.Information("All caches initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize caches");
                throw;
            }
        }

        private static async Task InitializeLastProcessedIdAsync()
        {
            try
            {
                var context = new HomassyDbContext();
                var maxId = await context.TableRecordChanges
                    .AsNoTracking()
                    .MaxAsync(t => (int?)t.Id);

                _lastProcessedId = maxId ?? 0;

                Log.Information($"Last processed ID initialized to: {_lastProcessedId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize last processed ID");
                _lastProcessedId = 0;
            }
        }

        private static async Task ProcessTableChangesAsync()
        {
            try
            {
                var context = new HomassyDbContext();
                var newChanges = await context.TableRecordChanges
                    .AsNoTracking()
                    .Where(t => t.Id > _lastProcessedId)
                    .OrderBy(t => t.Id)
                    .ToListAsync();

                if (newChanges.Count == 0)
                {
                    return;
                }

                Log.Information($"Processing {newChanges.Count} table change(s)");

                foreach (var change in newChanges)
                {
                    try
                    {
                        await ProcessSingleChangeAsync(change);
                        _lastProcessedId = change.Id;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to process change ID {change.Id} for table {change.TableName}, record {change.RecordId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving table changes");
            }
        }

        private static async Task ProcessSingleChangeAsync(Entities.TableRecordChange change)
        {
            switch (change.TableName)
            {
                case TableNames.Users:
                    await new UserFunctions().RefreshCacheAsync(change.RecordId);
                    break;

                case TableNames.Families:
                    await new FamilyFunctions().RefreshCacheAsync(change.RecordId);
                    break;

                case TableNames.Products:
                    await new ProductFunctions().RefreshProductCacheAsync(change.RecordId);
                    break;

                case TableNames.ProductItems:
                    await new ProductFunctions().RefreshProductItemCacheAsync(change.RecordId);
                    break;

                case TableNames.StorageLocations:
                    await new LocationFunctions().RefreshStorageLocationCacheAsync(change.RecordId);
                    break;

                case TableNames.ShoppingLocations:
                    await new LocationFunctions().RefreshShoppingLocationCacheAsync(change.RecordId);
                    break;

                default:
                    Log.Warning($"Unknown table name in TableRecordChanges: {change.TableName}");
                    break;
            }
        }
    }
}
