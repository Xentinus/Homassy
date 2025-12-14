using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Location
{
    public class DeleteMultipleShoppingLocationsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one location is required")]
        public List<Guid> LocationPublicIds { get; set; } = [];
    }
}
