using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class CreateMultipleProductsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one product is required")]
        public List<CreateProductRequest> Products { get; set; } = [];
    }
}
