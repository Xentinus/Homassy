using Homassy.API.Enums;

namespace Homassy.API.Models.Location
{
    /// <summary>
    /// A past purchase made at a given shopping location. The unit comes from the related inventory item;
    /// product name/brand are carried on the row (a shopping-location view aggregates across products).
    /// </summary>
    public class ShoppingLocationPurchaseInfo
    {
        public Guid PublicId { get; set; }
        public Guid ProductPublicId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductBrand { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public Unit? Unit { get; set; }
        public int? Price { get; set; }
        public Currency? Currency { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}
