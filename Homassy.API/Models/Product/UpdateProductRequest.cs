using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public record UpdateProductRequest
    {
        [StringLength(128, MinimumLength = 2)]
        public string? Name { get; init; }

        [StringLength(128, MinimumLength = 2)]
        public string? Brand { get; init; }

        [StringLength(128, MinimumLength = 2)]
        public string? Category { get; init; }

        [RegularExpression(@"^\d{4,14}$", ErrorMessage = "Barcode must be 4-14 digits")]
        [StringLength(14)]
        public string? Barcode { get; init; }

        public bool? IsEatable { get; init; }

        [EnumDataType(typeof(Unit))]
        public Unit? DefaultUnit { get; init; }
    }
}
