using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Location
{
    public class ShoppingLocationRequest
    {
        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? Name { get; set; }

        [StringLength(500)]
        [SanitizedString]
        public string? Description { get; set; }

        [StringLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code")]
        public string? Color { get; set; }

        [StringLength(128)]
        [SanitizedString]
        public string? Address { get; set; }

        [StringLength(64)]
        [SanitizedString]
        public string? City { get; set; }

        [StringLength(20)]
        [SanitizedString]
        public string? PostalCode { get; set; }

        [StringLength(64)]
        [SanitizedString]
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
