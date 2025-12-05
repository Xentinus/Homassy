using Homassy.API.Entities.Product;
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

        // Navigation properties
        public ICollection<ProductPurchaseInfo>? Purchases { get; set; }
    }
}
