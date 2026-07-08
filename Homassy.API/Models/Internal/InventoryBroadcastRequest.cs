using Homassy.API.Models.Product;

namespace Homassy.API.Models.Internal
{
    /// <summary>
    /// Payload for the internal inventory-broadcast endpoint used by out-of-process services
    /// (e.g. Homassy.Notifications automation workers) that mutate inventory directly and cannot
    /// reach the SignalR hub in-process. The API relays it over <see cref="Hubs.InventoryRealtime"/>.
    /// </summary>
    public class InventoryBroadcastRequest
    {
        /// <summary>"upserted" or "deleted".</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Owning user of the affected item (drives the user group for personal items).</summary>
        public int UserId { get; set; }

        /// <summary>Owning family, if the item is family-shared.</summary>
        public int? FamilyId { get; set; }

        /// <summary>Whether the item is family-shared (drives family vs user group routing).</summary>
        public bool SharedWithFamily { get; set; }

        // ---- upserted ----
        public InventoryGridProductInfo? Product { get; set; }
        public InventoryGridItemInfo? Item { get; set; }

        // ---- deleted ----
        public Guid? ProductPublicId { get; set; }
        public Guid? ItemPublicId { get; set; }
    }
}
