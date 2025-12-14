using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class ConsumeInventoryItemEntry
    {
        [Required]
        public required Guid InventoryItemPublicId { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }
    }
}
