using Homassy.API.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities
{
    public class ProductItem : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public int? StorageLocationId { get; set; }
        public required int ProductId { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        public required decimal Quantity { get; set; } = 1.0m;
        [EnumDataType(typeof(Unit))]
        public required Unit Unit { get; set; } = Unit.Gram;
        public DateTime PurchaseAt { get; set; } = DateTime.UtcNow;
        [Range(0.001, double.MaxValue, ErrorMessage = "Purchased quantity must be greater than 0")]
        public required decimal PurchasedQuantity { get; set; } = 1.0m;
        public int? Price { get; set; }
        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }
        public DateTime? ExpirationAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
    }
}
