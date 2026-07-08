using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    /// <summary>
    /// A single event in a product's global stock history, aggregated across all
    /// of its inventory items (including fully-consumed and soft-deleted ones).
    /// </summary>
    public class ProductHistoryEventInfo
    {
        public Guid EventId { get; set; }
        public ProductHistoryEventType Type { get; set; }
        public DateTime Date { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? RemainingQuantity { get; set; }
        public Unit? Unit { get; set; }
        public int? Price { get; set; }
        public Currency? Currency { get; set; }
        public string? UserName { get; set; }
        public LocationInfo? Location { get; set; }
        public Guid InventoryItemPublicId { get; set; }
    }
}
