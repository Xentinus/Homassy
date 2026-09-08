using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    public class ProductInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public ProductCategory? Category { get; set; }
        public Unit Unit { get; set; }
        public string? Barcode { get; set; }
        /// <summary>
        /// Versioned path to the list-sized thumbnail, or null when the product has no picture.
        /// See <see cref="Constants.MediaUrls"/>.
        /// </summary>
        public string? ProductImageUrl { get; set; }

        /// <summary>
        /// Versioned path to the full-size picture, for the detail view and the lightbox.
        /// </summary>
        /// <remarks>
        /// Carried alongside the thumbnail even in list payloads because the lightbox opens from a
        /// card in a list — the alternative is the client assembling the URL itself, which would
        /// put the endpoint's shape in two places.
        /// </remarks>
        public string? ProductImageFullUrl { get; set; }
        public bool IsEatable { get; set; }
        public bool IsFavorite { get; set; } = false;
    }
}
