using Homassy.API.Enums;

namespace Homassy.API.Models.Location
{
    /// <summary>
    /// A product currently in stock at a given storage location. Product name/brand are carried on the
    /// row itself, since a storage-location view aggregates inventory across many products.
    /// </summary>
    public class StorageLocationInventoryItemInfo
    {
        public Guid PublicId { get; set; }
        public Guid ProductPublicId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductBrand { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public Unit Unit { get; set; }
        public DateTime? ExpirationAt { get; set; }
        public bool IsSharedWithFamily { get; set; }
    }
}
