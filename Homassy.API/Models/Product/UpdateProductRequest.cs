using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class UpdateProductRequest
    {
        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? Name { get; set; }

        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? Brand { get; set; }

        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? Category { get; set; }

        [RegularExpression(@"^\d{4,14}$", ErrorMessage = "Barcode must be 4-14 digits")]
        [StringLength(14, MinimumLength = 4)]
        public string? Barcode { get; set; }

        [Base64String]
        public string? ProductPictureBase64 { get; set; }

        public bool? IsEatable { get; set; }
    }
}
