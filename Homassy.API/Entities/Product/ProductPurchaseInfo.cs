using Homassy.API.Entities.Common;
using Homassy.API.Entities.Location;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    public class ProductPurchaseInfo : RecordChangeEntity
    {
        [ForeignKey(nameof(ProductInventoryItem))]
        public required int ProductInventoryItemId { get; set; }

        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        [Range(0.001, double.MaxValue, ErrorMessage = "Original quantity must be greater than 0")]
        public required decimal OriginalQuantity { get; set; }

        public int? Price { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }

        public int? ShoppingLocationId { get; set; }
        public string? ReceiptNumber { get; set; }

        // Navigation properties
        public ShoppingLocation? ShoppingLocation { get; set; }
        public ProductInventoryItem ProductInventoryItem { get; set; } = null!;
    }
}
