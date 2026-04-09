using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Automation
{
    public class CreateAutomationRequest
    {
        public Guid? InventoryItemPublicId { get; set; }

        public Guid? ProductPublicId { get; set; }

        public Guid? ShoppingListPublicId { get; set; }

        [Required]
        [EnumDataType(typeof(ScheduleType))]
        public required ScheduleType ScheduleType { get; set; }

        [Range(1, 365, ErrorMessage = "Interval must be between 1 and 365 days")]
        public int? IntervalDays { get; set; }

        [EnumDataType(typeof(DaysOfWeek))]
        public DaysOfWeek? ScheduledDaysOfWeek { get; set; }

        [Range(1, 31, ErrorMessage = "Day of month must be between 1 and 31")]
        public int? ScheduledDayOfMonth { get; set; }

        [Required]
        public required TimeOnly ScheduledTime { get; set; }

        [Required]
        [EnumDataType(typeof(AutomationActionType))]
        public required AutomationActionType ActionType { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Consume quantity must be greater than 0")]
        public decimal? ConsumeQuantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit? ConsumeUnit { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Add quantity must be greater than 0")]
        public decimal? AddQuantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit? AddUnit { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Threshold quantity must be greater than 0")]
        public decimal? ThresholdQuantity { get; set; }

        public bool IsSharedWithFamily { get; set; } = false;
    }
}
