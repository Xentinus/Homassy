using Homassy.API.Enums;

namespace Homassy.API.Models.Automation
{
    public class AutomationResponse
    {
        public Guid PublicId { get; set; }
        public Guid? InventoryItemPublicId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductBrand { get; set; } = string.Empty;
        public Guid? ProductPublicId { get; set; }
        public Guid? ShoppingListPublicId { get; set; }
        public string? ShoppingListName { get; set; }
        public ScheduleType ScheduleType { get; set; }
        public int? IntervalDays { get; set; }
        public DaysOfWeek? ScheduledDaysOfWeek { get; set; }
        public int? ScheduledDayOfMonth { get; set; }
        public TimeOnly ScheduledTime { get; set; }
        public AutomationActionType ActionType { get; set; }
        public decimal? ConsumeQuantity { get; set; }
        public Unit? ConsumeUnit { get; set; }
        public decimal? AddQuantity { get; set; }
        public Unit? AddUnit { get; set; }
        public decimal? ThresholdQuantity { get; set; }
        public bool IsTriggered { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime? NextExecutionAt { get; set; }
        public DateTime? LastExecutedAt { get; set; }
    }
}
