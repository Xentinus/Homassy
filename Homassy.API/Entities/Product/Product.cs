using Homassy.API.Attributes.Validation;
using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Product
{
    public class Product : RecordChangeEntity
    {
        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; set; }

        [StringLength(128, MinimumLength = 2)]
        public required string Brand { get; set; }


        public ProductCategory? Category { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit Unit { get; set; } = Unit.Piece;

        [ValidBarcode]
        [StringLength(14, MinimumLength = 6)]
        public string? Barcode { get; set; }

        /// <summary>
        /// Content hash of the picture in <see cref="ProductImage"/>, or null when the product has
        /// none. Kept here so building an image URL costs nothing beyond the product row the cache
        /// already holds — a list of 200 products loads no image bytes at all.
        /// </summary>
        [StringLength(32)]
        public string? ProductPictureVersion { get; set; }

        public bool IsEatable { get; set; } = true;

        // Navigation properties
        public ICollection<ProductCustomization>? Customizations { get; set; }
        public ICollection<ProductInventoryItem>? InventoryItems { get; set; }
    }
}
