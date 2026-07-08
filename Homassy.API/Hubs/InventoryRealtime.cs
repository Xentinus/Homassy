using Homassy.API.Infrastructure;
using Homassy.API.Models.Product;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Broadcast helper for pushing Készletek (inventory) changes to connected clients over SignalR.
    /// Writes still flow through the REST endpoints / <see cref="Functions.ProductFunctions"/> (and the
    /// automation workers); after a successful commit the caller notifies everyone whose grid the change
    /// is visible on. Resolves the hub context from the application <see cref="ServiceLocator"/> (the
    /// Functions layer is instantiated with <c>new</c>, so constructor injection isn't available),
    /// mirroring <see cref="ShoppingListRealtime"/>.
    ///
    /// Scope model: family-shared items (<c>FamilyId</c> set) go to the family group; personal items
    /// go to the owner's user group. Every connection joins both its user group and (if any) its family
    /// group in <see cref="InventoryHub.OnConnectedAsync"/>, matching the grid's visibility filter
    /// (<c>item.UserId == me || item.FamilyId == myFamily</c>).
    /// </summary>
    public static class InventoryRealtime
    {
        public const string InventoryUpsertedEvent = "InventoryUpserted";
        public const string InventoryDeletedEvent = "InventoryDeleted";
        public const string ProductUpdatedEvent = "ProductUpdated";
        public const string ProductFavoriteChangedEvent = "ProductFavoriteChanged";
        public const string ProductDeletedEvent = "ProductDeleted";

        /// <summary>SignalR group for a family's shared inventory. Shared with <see cref="InventoryHub"/>.</summary>
        public static string FamilyGroup(int familyId) => $"inventory:family:{familyId}";

        /// <summary>SignalR group for a single user's personal inventory. Shared with <see cref="InventoryHub"/>.</summary>
        public static string UserGroup(int userId) => $"inventory:user:{userId}";

        private static IHubContext<InventoryHub>? HubContext =>
            ServiceLocator.Provider?.GetService<IHubContext<InventoryHub>>();

        /// <summary>
        /// Resolves the group an inventory item belongs to: the family group when it is shared with a
        /// family, otherwise the owner's user group.
        /// </summary>
        private static string ItemGroup(int userId, int? familyId, bool sharedWithFamily) =>
            sharedWithFamily && familyId.HasValue ? FamilyGroup(familyId.Value) : UserGroup(userId);

        /// <summary>
        /// Resolves the group product-level catalog changes target: the family group when the actor has
        /// a family (every member joins it), otherwise the actor's user group. Sending to the single
        /// most-inclusive group avoids duplicate delivery to a connection that is in both.
        /// </summary>
        private static string ProductScopeGroup(int userId, int? familyId) =>
            familyId.HasValue ? FamilyGroup(familyId.Value) : UserGroup(userId);

        /// <summary>Pushes an item create/update to the item's scope group (product carrier lets the grid insert a new card).</summary>
        public static Task InventoryUpsertedAsync(int userId, int? familyId, InventoryGridProductInfo product, InventoryGridItemInfo item, CancellationToken cancellationToken = default)
            => SendAsync(ItemGroup(userId, familyId, item.IsSharedWithFamily), InventoryUpsertedEvent, new { product, item }, cancellationToken);

        /// <summary>Notifies the item's scope group that an item was removed / fully consumed.</summary>
        public static Task InventoryDeletedAsync(int userId, int? familyId, bool sharedWithFamily, Guid productPublicId, Guid itemPublicId, CancellationToken cancellationToken = default)
            => SendAsync(ItemGroup(userId, familyId, sharedWithFamily), InventoryDeletedEvent, new { productPublicId, itemPublicId }, cancellationToken);

        /// <summary>Notifies the product's scope group that catalog fields (name/brand/barcode/eatable) changed. Never carries per-user favorite state.</summary>
        public static Task ProductUpdatedAsync(int userId, int? familyId, InventoryGridProductInfo product, CancellationToken cancellationToken = default)
            => SendAsync(ProductScopeGroup(userId, familyId), ProductUpdatedEvent, product, cancellationToken);

        /// <summary>Notifies the acting user (favorite is per-user) that a product's favorite flag changed.</summary>
        public static Task ProductFavoriteChangedAsync(int userId, Guid productPublicId, bool isFavorite, CancellationToken cancellationToken = default)
            => SendAsync(UserGroup(userId), ProductFavoriteChangedEvent, new { publicId = productPublicId, isFavorite }, cancellationToken);

        /// <summary>Notifies the product's scope group that the product itself was deleted.</summary>
        public static Task ProductDeletedAsync(int userId, int? familyId, Guid productPublicId, CancellationToken cancellationToken = default)
            => SendAsync(ProductScopeGroup(userId, familyId), ProductDeletedEvent, new { publicId = productPublicId }, cancellationToken);

        private static async Task SendAsync(string group, string eventName, object payload, CancellationToken cancellationToken)
        {
            var hub = HubContext;
            if (hub == null)
            {
                Log.Warning("InventoryHub context unavailable; skipping {Event} broadcast to {Group}", eventName, group);
                return;
            }

            try
            {
                await hub.Clients.Group(group).SendAsync(eventName, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                // A broadcast failure must never break the write that triggered it.
                Log.Error(ex, "Failed to broadcast {Event} to {Group}", eventName, group);
            }
        }
    }
}
