using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public record CreateProductRequest
    {
        [Required]
        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; init; }

        [Required]
        [StringLength(128, MinimumLength = 2)]
        public required string Brand { get; init; }

        [StringLength(128, MinimumLength = 2)]
        public string? Category { get; init; }

        [RegularExpression(@"^\d{4,14}$", ErrorMessage = "Barcode must be 4-14 digits")]
        [StringLength(14)]
        public string? Barcode { get; init; }

        public bool IsEatable { get; init; } = true;

        [Required]
        [EnumDataType(typeof(Unit))]
        public required Unit DefaultUnit { get; init; }

        public bool SaveOnlyPersonal { get; init; } = false;
    }
}
