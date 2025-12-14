using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class ConsumeMultipleInventoryItemsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<ConsumeInventoryItemEntry> Items { get; set; } = [];
    }
}
