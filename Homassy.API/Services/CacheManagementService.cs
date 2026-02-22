using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Entities.Common;
using Homassy.API.Functions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using System.Text.Json;

namespace Homassy.API.Services
{
    public class CacheManagementService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private static int _lastProcessedId;
        private NpgsqlConnection? _notificationConnection;
        private bool _isListenActive;
        private readonly SemaphoreSlim _processingLock = new(1, 1);

        public CacheManagementService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Cache management service starting...");

            try
            {
                await InitializeLastProcessedIdAsync(stoppingToken);
                await InitializeCachesAsync(stoppingToken);

                // Start LISTEN/NOTIFY in background
                _ = StartListenNotifyAsync(stoppingToken);

                Log.Information("Cache management service started successfully with LISTEN/NOTIFY + 5s fallback polling");

                // Fallback polling every 5 seconds
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                        // Only log if LISTEN is not active
                        if (!_isListenActive)
                        {
                            Log.Debug("Fallback polling active (LISTEN degraded)");
                        }

                        await ProcessTableChangesAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in fallback polling");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing cache management service");
            }
            finally
            {
                if (_notificationConnection != null)
                {
                    try
                    {
                        await _notificationConnection.CloseAsync();
                        await _notificationConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error disposing notification connection");
                    }
                }
                _processingLock.Dispose();
            }

            Log.Information("Cache management service stopped");
        }

        private async Task StartListenNotifyAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
                    var connectionString = context.Database.GetConnectionString();

                    if (string.IsNullOrEmpty(connectionString))
                    {
                        Log.Warning("Connection string is null, LISTEN/NOTIFY disabled");
                        return;
                    }

                    _notificationConnection = new NpgsqlConnection(connectionString);
                    await _notificationConnection.OpenAsync(stoppingToken);

                    _notificationConnection.Notification += async (sender, args) =>
                    {
                        try
                        {
                            Log.Debug($"Received NOTIFY: {args.Payload}");
                            await ProcessTableChangesAsync(stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error processing notification");
                        }
                    };

                    using var cmd = new NpgsqlCommand("LISTEN cache_changes", _notificationConnection);
                    await cmd.ExecuteNonQueryAsync(stoppingToken);

                    _isListenActive = true;
                    Log.Information("PostgreSQL LISTEN/NOTIFY active - real-time cache updates enabled");

                    // Keep connection alive and wait for notifications
                    while (!stoppingToken.IsCancellationRequested && _notificationConnection.State == System.Data.ConnectionState.Open)
                    {
                        await _notificationConnection.WaitAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _isListenActive = false;
                    Log.Error(ex, "LISTEN/NOTIFY connection failed, retrying in 30 seconds...");

                    if (_notificationConnection != null)
                    {
                        try
                        {
                            await _notificationConnection.CloseAsync();
                            await _notificationConnection.DisposeAsync();
                            _notificationConnection = null;
                        }
                        catch { /* Ignore cleanup errors */ }
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }

            _isListenActive = false;
        }

        private async Task InitializeCachesAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log.Information("Initializing caches...");

                await new UserFunctions().InitializeCacheAsync();
                await new FamilyFunctions().InitializeCacheAsync();
                await new ProductFunctions().InitializeCacheAsync();
                await new LocationFunctions().InitializeCacheAsync();
                await new ShoppingListFunctions().InitializeCacheAsync();
                await new ActivityFunctions().InitializeCacheAsync();

                Log.Information("All caches initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize caches");
                throw;
            }
        }

        private async Task InitializeLastProcessedIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

                var maxId = await context.TableRecordChanges
                    .AsNoTracking()
                    .MaxAsync(t => (int?)t.Id, cancellationToken);

                _lastProcessedId = maxId ?? 0;

                Log.Information($"Last processed ID initialized to: {_lastProcessedId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize last processed ID");
                _lastProcessedId = 0;
            }
        }

        private async Task ProcessTableChangesAsync(CancellationToken cancellationToken)
        {
            // Prevent concurrent processing from LISTEN and polling
            if (!await _processingLock.WaitAsync(0, cancellationToken))
            {
                return; // Already processing, skip this invocation
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

                var newChanges = await context.TableRecordChanges
                    .AsNoTracking()
                    .Where(t => t.Id > _lastProcessedId)
                    .OrderBy(t => t.Id)
                    .ToListAsync(cancellationToken);

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
            finally
            {
                _processingLock.Release();
            }
        }

        private static async Task ProcessSingleChangeAsync(TableRecordChange change)
        {
            switch (change.TableName)
            {
                case TableNames.Users:
                    await new UserFunctions().RefreshUserCacheAsync(change.RecordId);
                    break;

                case TableNames.UserNotificationPreferences:
                    await new UserFunctions().RefreshUserNotificationCacheAsync(change.RecordId);
                    break;

                case TableNames.UserProfiles:
                    await new UserFunctions().RefreshUserProfileCacheAsync(change.RecordId);
                    break;

                case TableNames.Families:
                    await new FamilyFunctions().RefreshCacheAsync(change.RecordId);
                    break;

                case TableNames.Products:
                    await new ProductFunctions().RefreshProductCacheAsync(change.RecordId);
                    break;

                case TableNames.ProductInventoryItems:
                    await new ProductFunctions().RefreshInventoryItemCacheAsync(change.RecordId);
                    break;

                case TableNames.ProductPurchaseInfos:
                    await new ProductFunctions().RefreshPurchaseInfoCacheAsync(change.RecordId);
                    break;

                case TableNames.ProductConsumptionLogs:
                    await new ProductFunctions().RefreshConsumptionLogsCacheAsync(change.RecordId);
                    break;

                case TableNames.ProductCustomizations:
                    await new ProductFunctions().RefreshProductCustomizationCacheAsync(change.RecordId);
                    break;

                case TableNames.StorageLocations:
                    await new LocationFunctions().RefreshStorageLocationCacheAsync(change.RecordId);
                    break;

                case TableNames.ShoppingLocations:
                    await new LocationFunctions().RefreshShoppingLocationCacheAsync(change.RecordId);
                    break;

                case TableNames.ShoppingLists:
                    await new ShoppingListFunctions().RefreshShoppingListCacheAsync(change.RecordId);
                    break;

                case TableNames.ShoppingListItems:
                    await new ShoppingListFunctions().RefreshShoppingListItemCacheAsync(change.RecordId);
                    break;

                case TableNames.Activities:
                    await new ActivityFunctions().RefreshActivityCacheAsync(change.RecordId);
                    break;

                default:
                    Log.Warning($"Unknown table name in TableRecordChanges: {change.TableName}");
                    break;
            }
        }
    }
}
