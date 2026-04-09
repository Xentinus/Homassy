using Homassy.API.Enums;

namespace Homassy.API.Models.Automation
{
    public class AutomationExecutionResponse
    {
        public Guid PublicId { get; set; }
        public DateTime ExecutedAt { get; set; }
        public AutomationExecutionStatus Status { get; set; }
        public decimal? ConsumedQuantity { get; set; }
        public string? Notes { get; set; }
        public int? TriggeredByUserId { get; set; }
    }
}
