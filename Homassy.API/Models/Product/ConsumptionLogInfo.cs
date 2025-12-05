namespace Homassy.API.Models.Product
{
    public class ConsumptionLogInfo
    {
        public Guid PublicId { get; set; }
        public string? UserName { get; set; }
        public decimal ConsumedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public DateTime ConsumedAt { get; set; }
    }
}
