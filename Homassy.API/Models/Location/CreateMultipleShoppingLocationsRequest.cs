using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Location
{
    public class CreateMultipleShoppingLocationsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one shopping location is required")]
        public List<ShoppingLocationRequest> Locations { get; set; } = [];
    }
}
