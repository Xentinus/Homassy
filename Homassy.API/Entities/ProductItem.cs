using Homassy.API.Enums;

namespace Homassy.API.Entities
{
    public class ProductItem : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public required int ProductId { get; set; }
        public required decimal Quantity { get; set; } = 1.0m;
        public required Unit Unit { get; set; } = Unit.Gram;
        public DateTime PurchaseAt { get; set; } = DateTime.UtcNow;
        public required decimal PurchasedQuantity { get; set; } = 1.0m;
        public int? Price { get; set; }
        public Currency? Currency { get; set; }
        public DateTime? ExpirationAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
    }
}
