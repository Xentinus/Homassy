using Homassy.API.Constants;
using Homassy.API.Functions;
using Serilog;

namespace Homassy.API.Services
{
    /// <summary>
    /// Maps one changed row onto the in-memory cache that holds it.
    /// </summary>
    /// <remarks>
    /// Two things drive this, and they are not alternatives:
    /// <list type="bullet">
    /// <item><see cref="CacheManagementService"/>, off the database's own
    /// <c>TableRecordChanges</c> trigger, is the <em>cross-instance</em> path - it is how a second
    /// API instance (or the notifications worker) learns that this one wrote something.</item>
    /// <item><see cref="Infrastructure.Caching.CacheWriteThrough"/> is the <em>in-process</em> path -
    /// it refreshes this instance's own cache on the commit that changed the row, so a request
    /// cannot read back its own stale write.</item>
    /// </list>
    /// Both funnel through here so there is one description of which cache owns which table.
    /// Refreshing twice is harmless: every <c>Refresh*CacheAsync</c> re-reads the row and upserts
    /// or evicts it, so the second call is the same work with the same result.
    /// </remarks>
    public static class EntityCacheRefresher
    {
        /// <summary>
        /// The tables whose rows something actually keeps in memory, and therefore the only ones
        /// worth a refresh. Anything else read straight from the database cannot go stale.
        /// </summary>
        public static readonly IReadOnlySet<string> CachedTables = new HashSet<string>(StringComparer.Ordinal)
        {
            TableNames.Users,
            TableNames.UserNotificationPreferences,
            TableNames.UserProfiles,
            TableNames.Families,
            TableNames.Products,
            TableNames.ProductInventoryItems,
            TableNames.ProductPurchaseInfos,
            TableNames.ProductConsumptionLogs,
            TableNames.ProductCustomizations,
            TableNames.StorageLocations,
            TableNames.ShoppingLocations,
            TableNames.ShoppingLists,
            TableNames.ShoppingListItems,
            TableNames.Activities
        };

        /// <summary>
        /// Re-reads row <paramref name="recordId"/> of <paramref name="tableName"/> and applies it
        /// to the cache that holds that table.
        /// </summary>
        /// <param name="tableName">The database table the row belongs to.</param>
        /// <param name="recordId">The row's <c>Id</c>.</param>
        /// <param name="services">
        /// A scope to resolve the Functions layer from. The caches are static, but the classes that
        /// own them are scoped services.
        /// </param>
        public static async Task RefreshAsync(string tableName, int recordId, IServiceProvider services)
        {
            switch (tableName)
            {
                case TableNames.Users:
                    await services.GetRequiredService<UserFunctions>().RefreshUserCacheAsync(recordId);
                    break;

                case TableNames.UserNotificationPreferences:
                    await services.GetRequiredService<UserFunctions>().RefreshUserNotificationCacheAsync(recordId);
                    break;

                case TableNames.UserProfiles:
                    await services.GetRequiredService<UserFunctions>().RefreshUserProfileCacheAsync(recordId);
                    break;

                case TableNames.Families:
                    await services.GetRequiredService<FamilyFunctions>().RefreshCacheAsync(recordId);
                    break;

                case TableNames.Products:
                    await services.GetRequiredService<ProductFunctions>().RefreshProductCacheAsync(recordId);
                    break;

                case TableNames.ProductInventoryItems:
                    await services.GetRequiredService<ProductFunctions>().RefreshInventoryItemCacheAsync(recordId);
                    break;

                case TableNames.ProductPurchaseInfos:
                    await services.GetRequiredService<ProductFunctions>().RefreshPurchaseInfoCacheAsync(recordId);
                    break;

                case TableNames.ProductConsumptionLogs:
                    await services.GetRequiredService<ProductFunctions>().RefreshConsumptionLogsCacheAsync(recordId);
                    break;

                case TableNames.ProductCustomizations:
                    await services.GetRequiredService<ProductFunctions>().RefreshProductCustomizationCacheAsync(recordId);
                    break;

                case TableNames.StorageLocations:
                    await services.GetRequiredService<LocationFunctions>().RefreshStorageLocationCacheAsync(recordId);
                    break;

                case TableNames.ShoppingLocations:
                    await services.GetRequiredService<LocationFunctions>().RefreshShoppingLocationCacheAsync(recordId);
                    break;

                case TableNames.ShoppingLists:
                    await services.GetRequiredService<ShoppingListFunctions>().RefreshShoppingListCacheAsync(recordId);
                    break;

                case TableNames.ShoppingListItems:
                    await services.GetRequiredService<ShoppingListFunctions>().RefreshShoppingListItemCacheAsync(recordId);
                    break;

                case TableNames.Activities:
                    await services.GetRequiredService<ActivityFunctions>().RefreshActivityCacheAsync(recordId);
                    break;

                case TableNames.FamilyExternalCalendars:
                    // No in-memory cache for external calendars; data read directly from DB.
                    Log.Debug("FamilyExternalCalendars change recorded (id: {RecordId})", recordId);
                    break;

                default:
                    Log.Warning($"Unknown table name in TableRecordChanges: {tableName}");
                    break;
            }
        }
    }
}
