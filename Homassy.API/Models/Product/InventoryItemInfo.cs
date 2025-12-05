using Homassy.API.Enums;

namespace Homassy.API.Models.Product
{
    public class InventoryItemInfo
    {
        public Guid PublicId { get; set; }
        public decimal CurrentQuantity { get; set; }
        public Unit Unit { get; set; }
        public DateTime? ExpirationAt { get; set; }
        public PurchaseInfo? PurchaseInfo { get; set; }
        public List<ConsumptionLogInfo> ConsumptionLogs { get; set; } = new();
    }
}
