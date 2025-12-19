using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class CreateProductRequest
    {
        [Required]
        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public required string Name { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public required string Brand { get; set; }

        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? Category { get; set; }

        [ValidBarcode]
        [StringLength(14, MinimumLength = 6)]
        public string? Barcode { get; set; }

        public bool IsEatable { get; set; } = true;

        // Customization properties
        [StringLength(128)]
        [SanitizedString]
        public string? Notes { get; set; }

        public bool IsFavorite { get; set; } = false;
    }
}
