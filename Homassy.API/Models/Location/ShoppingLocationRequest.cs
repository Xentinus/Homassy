using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Location
{
    public class ShoppingLocationRequest
    {
        [StringLength(128, MinimumLength = 2)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code")]
        public string? Color { get; set; }

        [StringLength(128)]
        public string? Address { get; set; }

        [StringLength(64)]
        public string? City { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(64)]
        public string? Country { get; set; }

        [Url]
        [StringLength(255)]
        public string? Website { get; set; }

        [Url]
        [StringLength(255)]
        public string? GoogleMaps { get; set; }

        public bool? IsSharedWithFamily { get; set; }
    }
}
