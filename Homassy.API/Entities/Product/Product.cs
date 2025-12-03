using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Product
{
    public class Product : RecordChangeEntity
    {
        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; set; }

        [StringLength(128, MinimumLength = 2)]
        public required string Brand { get; set; }

        [StringLength(128, MinimumLength = 2)]
        public string? Category { get; set; }

        [RegularExpression(@"^\d{4,14}$", ErrorMessage = "Barcode must be 4-14 digits")]
        [StringLength(14, MinimumLength = 4)]
        public string? Barcode { get; set; }

        [Base64String]
        public string? ProductPictureBase64 { get; set; }

        public bool IsEatable { get; set; } = true;

        // Navigation properties
        public ICollection<ProductCustomization>? Customizations { get; set; }
        public ICollection<ProductInventoryItem>? InventoryItems { get; set; }
    }
}
