using Homassy.API.Entities.Product;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Location
{
    public class ShoppingLocation : LocationBase
    {
        [StringLength(128)]
        public string? Address { get; set; }

        [StringLength(64)]
        public string? City { get; set; }

        [DataType(DataType.PostalCode)]
        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(64)]
        public string? Country { get; set; }

        [Url]
        [StringLength(255)]
        public string? Website { get; set; }

        [Url]
        [StringLength(255)]
        public string? GoogleMaps { get; set; }

        // Geocoded coordinates (derived from the address on save) used for
        // proximity-based features such as the shopping-list "you are here" highlight.
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        // Store types this location belongs to (0..N). Stored as a PostgreSQL integer[]
        // (each enum element as its int value). Drives the "similar store here" highlight;
        // a store like OBI can be both HardwareStore and GardenCenter.
        public List<StoreType> StoreTypes { get; set; } = new();

        // Navigation properties
        public ICollection<ProductPurchaseInfo>? Purchases { get; set; }
    }
}
