using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    /// <summary>
    /// Lightweight inventory item projection carrying only the fields the Készletek grid cards
    /// need. Used for the realtime snapshot and broadcast payloads (see <see cref="Hubs.InventoryRealtime"/>).
    /// Heavier detail (storage location, purchase info, consumption logs) is intentionally omitted;
    /// the detail page fetches those via REST — apart from <see cref="OriginalQuantity"/>, which is
    /// one number and is what lets a card draw a stock ring at all.
    /// </summary>
    public class InventoryGridItemInfo
    {
        public Guid PublicId { get; set; }
        public Guid ProductPublicId { get; set; }
        public decimal CurrentQuantity { get; set; }

        /// <summary>
        /// What was originally bought, when the item has purchase info. The denominator the grid
        /// card's stock ring needs — null means there is nothing to measure against, and the card
        /// shows a flat badge rather than inventing a scale.
        /// </summary>
        public decimal? OriginalQuantity { get; set; }

        public Unit Unit { get; set; }
        public DateTime? ExpirationAt { get; set; }
        public bool IsSharedWithFamily { get; set; }
    }
}
