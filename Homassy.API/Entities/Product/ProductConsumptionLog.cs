using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    public class ProductConsumptionLog : RecordChangeEntity
    {
        [ForeignKey(nameof(ProductInventoryItem))]
        public required int ProductInventoryItemId { get; set; }

        public int? UserId { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Consumed quantity must be greater than 0")]
        public required decimal ConsumedQuantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Remaining quantity cannot be negative")]
        public required decimal RemainingQuantity { get; set; }

        public DateTime ConsumedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProductInventoryItem ProductInventoryItem { get; set; } = null!;
    }
}
