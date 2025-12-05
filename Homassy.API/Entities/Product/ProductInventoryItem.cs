using Homassy.API.Entities.Common;
using Homassy.API.Entities.Location;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    public class ProductInventoryItem : RecordChangeEntity
    {
        [ForeignKey(nameof(Product))]
        public required int ProductId { get; set; }

        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public int? StorageLocationId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Current quantity cannot be negative")]
        public required decimal CurrentQuantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public required Unit Unit { get; set; } = Unit.Gram;

        public DateTime? ExpirationAt { get; set; }

        public bool IsFullyConsumed { get; set; } = false;

        public DateTime? FullyConsumedAt { get; set; }

        // Navigation properties
        public Product Product { get; set; } = null!;
        public StorageLocation? StorageLocation { get; set; }
        public ProductPurchaseInfo? PurchaseInfo { get; set; }
        public ICollection<ProductConsumptionLog>? ConsumptionLogs { get; set; }
    }
}
