using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    public class PurchaseInfo
    {
        public Guid PublicId { get; set; }
        public DateTime PurchasedAt { get; set; }
        public decimal OriginalQuantity { get; set; }
        public int? Price { get; set; }
        public Currency? Currency { get; set; }
        public LocationInfo? ShoppingLocation { get; set; }
    }
}
