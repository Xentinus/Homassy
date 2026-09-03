using Homassy.API.Models.ShoppingList;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Broadcast helper for pushing shopping list changes to connected clients over SignalR.
    /// Writes still flow through the REST endpoints / <see cref="Functions.ShoppingListFunctions"/>;
    /// after a successful commit the Functions layer calls into here to notify everyone viewing
    /// the affected list. Takes its hub context through the constructor and is registered as a singleton; the
    /// Functions layer reaches it through <see cref="Functions.FunctionsRuntime"/>.
    /// </summary>
    public sealed class ShoppingListRealtime
    {
        public const string ItemUpsertedEvent = "ItemUpserted";
        public const string ItemDeletedEvent = "ItemDeleted";
        public const string ListUpdatedEvent = "ListUpdated";
        public const string ListDeletedEvent = "ListDeleted";

        /// <summary>
        /// SignalR group name for a single shopping list. Shared with <see cref="ShoppingListHub"/>.
        /// </summary>
        public static string GroupName(Guid listPublicId) => $"shopping-list:{listPublicId}";

        private readonly IHubContext<ShoppingListHub>? _hubContext;

        /// <remarks>
        /// The hub context is optional so a host that maps no hubs resolves the helper and skips
        /// the broadcast, exactly as the previous service-locator lookup did when it returned null.
        /// </remarks>
        public ShoppingListRealtime(IHubContext<ShoppingListHub>? hubContext = null)
        {
            _hubContext = hubContext;
        }

        /// <summary>Pushes the current (hydrated) state of an item to the list's group (covers create/update/purchase/restore).</summary>
        public Task ItemUpsertedAsync(Guid listPublicId, ShoppingListItemInfo item, CancellationToken cancellationToken = default)
            => SendAsync(listPublicId, ItemUpsertedEvent, item, cancellationToken);

        /// <summary>Notifies the list's group that an item was removed.</summary>
        public Task ItemDeletedAsync(Guid listPublicId, Guid itemPublicId, CancellationToken cancellationToken = default)
            => SendAsync(listPublicId, ItemDeletedEvent, new { publicId = itemPublicId, shoppingListPublicId = listPublicId }, cancellationToken);

        /// <summary>Notifies the list's group that list metadata (name, color, sharing) changed.</summary>
        public Task ListUpdatedAsync(ShoppingListInfo list, CancellationToken cancellationToken = default)
            => SendAsync(list.PublicId, ListUpdatedEvent, list, cancellationToken);

        /// <summary>Notifies the list's group that the list itself was deleted.</summary>
        public Task ListDeletedAsync(Guid listPublicId, CancellationToken cancellationToken = default)
            => SendAsync(listPublicId, ListDeletedEvent, new { publicId = listPublicId }, cancellationToken);

        private async Task SendAsync(Guid listPublicId, string eventName, object payload, CancellationToken cancellationToken)
        {
            var hub = _hubContext;
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
