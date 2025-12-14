using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Location
{
    public class CreateMultipleStorageLocationsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one storage location is required")]
        public List<StorageLocationRequest> Locations { get; set; } = [];
    }
}
