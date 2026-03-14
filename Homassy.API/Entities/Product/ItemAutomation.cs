using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    public class ItemAutomation : RecordChangeEntity
    {
        [ForeignKey(nameof(ProductInventoryItem))]
        public required int ProductInventoryItemId { get; set; }

        public int? FamilyId { get; set; }
        public int? UserId { get; set; }

        [EnumDataType(typeof(ScheduleType))]
        public required ScheduleType ScheduleType { get; set; }

        public int? IntervalDays { get; set; }

        public DayOfWeek? ScheduledDayOfWeek { get; set; }

        public int? ScheduledDayOfMonth { get; set; }

        public required TimeOnly ScheduledTime { get; set; }

        [EnumDataType(typeof(AutomationActionType))]
        public required AutomationActionType ActionType { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Consume quantity must be greater than 0")]
        public decimal? ConsumeQuantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit? ConsumeUnit { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime? NextExecutionAt { get; set; }

        public DateTime? LastExecutedAt { get; set; }

        // Navigation properties
        public ProductInventoryItem ProductInventoryItem { get; set; } = null!;
        public ICollection<ItemAutomationExecution>? Executions { get; set; }
    }
}
