using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    public class ProductInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public ProductCategory? Category { get; set; }
        public string? Barcode { get; set; }
        public string? ProductPictureBase64 { get; set; }
        public bool IsEatable { get; set; }
        public bool IsFavorite { get; set; } = false;
    }
}
