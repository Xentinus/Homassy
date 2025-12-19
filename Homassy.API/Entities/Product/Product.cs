using Homassy.API.Attributes.Validation;
using Homassy.API.Entities.Common;
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

        [ValidBarcode]
        [StringLength(14, MinimumLength = 6)]
        public string? Barcode { get; set; }

        [Base64String]
        public string? ProductPictureBase64 { get; set; }

        public bool IsEatable { get; set; } = true;

        // Navigation properties
        public ICollection<ProductCustomization>? Customizations { get; set; }
        public ICollection<ProductInventoryItem>? InventoryItems { get; set; }
    }
}
