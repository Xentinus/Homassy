using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class SplitInventoryItemRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }
    }
}
