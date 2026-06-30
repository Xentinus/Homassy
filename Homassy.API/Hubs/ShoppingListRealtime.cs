using Homassy.API.Infrastructure;
using Homassy.API.Models.ShoppingList;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Broadcast helper for pushing shopping list changes to connected clients over SignalR.
    /// Writes still flow through the REST endpoints / <see cref="Functions.ShoppingListFunctions"/>;
    /// after a successful commit the Functions layer calls into here to notify everyone viewing
    /// the affected list. Resolves the hub context from the application <see cref="ServiceLocator"/>
    /// (the Functions layer is instantiated with <c>new</c>, so constructor injection isn't available),
    /// mirroring the existing static cross-cutting pattern used elsewhere in the codebase.
    /// </summary>
    public static class ShoppingListRealtime
    {
        public const string ItemUpsertedEvent = "ItemUpserted";
        public const string ItemDeletedEvent = "ItemDeleted";
        public const string ListUpdatedEvent = "ListUpdated";
        public const string ListDeletedEvent = "ListDeleted";

        /// <summary>
        /// SignalR group name for a single shopping list. Shared with <see cref="ShoppingListHub"/>.
        /// </summary>
        public static string GroupName(Guid listPublicId) => $"shopping-list:{listPublicId}";

        private static IHubContext<ShoppingListHub>? HubContext =>
            ServiceLocator.Provider?.GetService<IHubContext<ShoppingListHub>>();

        /// <summary>Pushes the current (hydrated) state of an item to the list's group (covers create/update/purchase/restore).</summary>
        public static Task ItemUpsertedAsync(Guid listPublicId, ShoppingListItemInfo item, CancellationToken cancellationToken = default)
            => SendAsync(listPublicId, ItemUpsertedEvent, item, cancellationToken);

        /// <summary>Notifies the list's group that an item was removed.</summary>
        public static Task ItemDeletedAsync(Guid listPublicId, Guid itemPublicId, CancellationToken cancellationToken = default)
            => SendAsync(listPublicId, ItemDeletedEvent, new { publicId = itemPublicId, shoppingListPublicId = listPublicId }, cancellationToken);

        /// <summary>Notifies the list's group that list metadata (name, color, sharing) changed.</summary>
        public static Task ListUpdatedAsync(ShoppingListInfo list, CancellationToken cancellationToken = default)
            => SendAsync(list.PublicId, ListUpdatedEvent, list, cancellationToken);

        /// <summary>Notifies the list's group that the list itself was deleted.</summary>
        public static Task ListDeletedAsync(Guid listPublicId, CancellationToken cancellationToken = default)
            => SendAsync(listPublicId, ListDeletedEvent, new { publicId = listPublicId }, cancellationToken);

        private static async Task SendAsync(Guid listPublicId, string eventName, object payload, CancellationToken cancellationToken)
        {
            var hub = HubContext;
            if (hub == null)
            {
                Log.Warning("ShoppingListHub context unavailable; skipping {Event} broadcast for list {ListPublicId}", eventName, listPublicId);
                return;
            }

            try
            {
                await hub.Clients.Group(GroupName(listPublicId)).SendAsync(eventName, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                // A broadcast failure must never break the HTTP write that triggered it.
                Log.Error(ex, "Failed to broadcast {Event} for shopping list {ListPublicId}", eventName, listPublicId);
            }
        }
    }
}
