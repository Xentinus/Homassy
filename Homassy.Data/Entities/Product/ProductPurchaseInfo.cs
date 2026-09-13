using Homassy.Data.Entities.Common;
using Homassy.Data.Entities.Location;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.Data.Entities.Product
{
    public class ProductPurchaseInfo : RecordChangeEntity
    {
        [ForeignKey(nameof(ProductInventoryItem))]
        public required int ProductInventoryItemId { get; set; }

        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        [Range(0.001, double.MaxValue, ErrorMessage = "Original quantity must be greater than 0")]
        public required decimal OriginalQuantity { get; set; }

        public decimal? Price { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }

        public int? ShoppingLocationId { get; set; }
        public string? ReceiptNumber { get; set; }

        // Navigation properties
        public ShoppingLocation? ShoppingLocation { get; set; }
        public ProductInventoryItem ProductInventoryItem { get; set; } = null!;
    }
}
