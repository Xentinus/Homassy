using Homassy.API.Infrastructure;
using Homassy.API.Models.Automation;
using Homassy.API.Models.ExternalCalendar;
using Homassy.API.Models.Location;
using Homassy.API.Models.Product;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Broadcast helper for pushing "Törzsadatok" (master-data) changes to connected clients over SignalR.
    /// Writes still flow through the REST endpoints / Functions layer; after a successful commit the caller
    /// notifies everyone whose master-data lists the change is visible on. Resolves the hub context from the
    /// application <see cref="ServiceLocator"/> (the Functions layer is instantiated with <c>new</c>, so
    /// constructor injection isn't available), mirroring <see cref="InventoryRealtime"/>.
    ///
    /// Scope model differs per entity, so the group is resolved from the entity, not from a single rule:
    /// <list type="bullet">
    /// <item>Products have no owner on the entity — catalog changes go to the actor's family group (or user
    /// group when the actor has no family), matching <see cref="InventoryRealtime"/>'s product scope.</item>
    /// <item>Locations always have an owner <c>UserId</c> plus an optional <c>FamilyId</c> (shared): family
    /// group when shared, else the owner's user group.</item>
    /// <item>Automations have mutually-exclusive <c>UserId</c>/<c>FamilyId</c> (plus a always-set
    /// <c>CreatedByUserId</c> fallback): family group when family-shared, else the owner's user group.</item>
    /// <item>External calendars are always family-scoped: family group only.</item>
    /// </list>
    /// </summary>
    public static class MasterDataRealtime
    {
        public const string ProductUpsertedEvent = "ProductUpserted";
        public const string ProductDeletedEvent = "ProductDeleted";
        public const string StorageLocationUpsertedEvent = "StorageLocationUpserted";
        public const string StorageLocationDeletedEvent = "StorageLocationDeleted";
        public const string ShoppingLocationUpsertedEvent = "ShoppingLocationUpserted";
        public const string ShoppingLocationDeletedEvent = "ShoppingLocationDeleted";
        public const string AutomationUpsertedEvent = "AutomationUpserted";
        public const string AutomationDeletedEvent = "AutomationDeleted";
        public const string ExternalCalendarUpsertedEvent = "ExternalCalendarUpserted";
        public const string ExternalCalendarDeletedEvent = "ExternalCalendarDeleted";

        /// <summary>SignalR group for a family's shared master data. Shared with <see cref="MasterDataHub"/>.</summary>
        public static string FamilyGroup(int familyId) => $"masterdata:family:{familyId}";

        /// <summary>SignalR group for a single user's personal master data. Shared with <see cref="MasterDataHub"/>.</summary>
        public static string UserGroup(int userId) => $"masterdata:user:{userId}";

        private static IHubContext<MasterDataHub>? HubContext =>
            ServiceLocator.Provider?.GetService<IHubContext<MasterDataHub>>();

        /// <summary>
        /// Resolves the group an entity targets: the family group when it is family-scoped, otherwise the
        /// owner's user group. Every connection joins both its user group and (if any) its family group in
        /// <see cref="MasterDataHub.OnConnectedAsync"/>, and we send to the single most-inclusive group to
        /// avoid duplicate delivery to a connection that is in both.
        /// </summary>
        private static string Scope(int? familyId, int ownerUserId) =>
            familyId.HasValue ? FamilyGroup(familyId.Value) : UserGroup(ownerUserId);

        // --- Products (catalog) -------------------------------------------------
        public static Task ProductUpsertedAsync(int userId, int? familyId, ProductInfo product, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, userId), ProductUpsertedEvent, product, cancellationToken);

        public static Task ProductDeletedAsync(int userId, int? familyId, Guid productPublicId, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, userId), ProductDeletedEvent, new { publicId = productPublicId }, cancellationToken);

        // --- Storage locations --------------------------------------------------
        public static Task StorageLocationUpsertedAsync(int ownerUserId, int? familyId, StorageLocationInfo location, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, ownerUserId), StorageLocationUpsertedEvent, location, cancellationToken);

        public static Task StorageLocationDeletedAsync(int ownerUserId, int? familyId, Guid publicId, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, ownerUserId), StorageLocationDeletedEvent, new { publicId }, cancellationToken);

        // --- Shopping locations -------------------------------------------------
        public static Task ShoppingLocationUpsertedAsync(int ownerUserId, int? familyId, ShoppingLocationInfo location, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, ownerUserId), ShoppingLocationUpsertedEvent, location, cancellationToken);

        public static Task ShoppingLocationDeletedAsync(int ownerUserId, int? familyId, Guid publicId, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, ownerUserId), ShoppingLocationDeletedEvent, new { publicId }, cancellationToken);

        // --- Automation rules ---------------------------------------------------
        public static Task AutomationUpsertedAsync(int ownerUserId, int? familyId, AutomationResponse automation, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, ownerUserId), AutomationUpsertedEvent, automation, cancellationToken);

        public static Task AutomationDeletedAsync(int ownerUserId, int? familyId, Guid publicId, CancellationToken cancellationToken = default)
            => SendAsync(Scope(familyId, ownerUserId), AutomationDeletedEvent, new { publicId }, cancellationToken);

        // --- External calendars (always family-scoped) --------------------------
        public static Task ExternalCalendarUpsertedAsync(int familyId, ExternalCalendarResponse calendar, CancellationToken cancellationToken = default)
            => SendAsync(FamilyGroup(familyId), ExternalCalendarUpsertedEvent, calendar, cancellationToken);

        public static Task ExternalCalendarDeletedAsync(int familyId, Guid publicId, CancellationToken cancellationToken = default)
            => SendAsync(FamilyGroup(familyId), ExternalCalendarDeletedEvent, new { publicId }, cancellationToken);

        private static async Task SendAsync(string group, string eventName, object payload, CancellationToken cancellationToken)
        {
            var hub = HubContext;
            if (hub == null)
            {
                Log.Warning("MasterDataHub context unavailable; skipping {Event} broadcast to {Group}", eventName, group);
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
