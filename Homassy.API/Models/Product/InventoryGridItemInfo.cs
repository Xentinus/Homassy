using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    /// <summary>
    /// Lightweight inventory item projection carrying only the fields the Készletek grid cards
    /// need. Used for the realtime snapshot and broadcast payloads (see <see cref="Hubs.InventoryRealtime"/>).
    /// Heavier detail (storage location, purchase info, consumption logs) is intentionally omitted;
    /// the detail page fetches those via REST.
    /// </summary>
    public class InventoryGridItemInfo
    {
        public Guid PublicId { get; set; }
        public Guid ProductPublicId { get; set; }
        public decimal CurrentQuantity { get; set; }
        public Unit Unit { get; set; }
        public DateTime? ExpirationAt { get; set; }
        public bool IsSharedWithFamily { get; set; }
    }
}
