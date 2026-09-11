using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Location
{
    /// <summary>
    /// The manual order of the caller's locations, as the ids in the order they should appear.
    /// Used by both the storage and the shopping location reorder endpoints.
    /// </summary>
    public class ReorderLocationsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one location is required")]
        public List<Guid> LocationPublicIds { get; set; } = [];
    }
}
