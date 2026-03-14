using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Automation
{
    public class CreateAutomationRequest
    {
        [Required]
        public required Guid InventoryItemPublicId { get; set; }

        [Required]
        [EnumDataType(typeof(ScheduleType))]
        public required ScheduleType ScheduleType { get; set; }

        [Range(1, 365, ErrorMessage = "Interval must be between 1 and 365 days")]
        public int? IntervalDays { get; set; }

        public DayOfWeek? ScheduledDayOfWeek { get; set; }

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

        public bool IsSharedWithFamily { get; set; } = false;
    }
}
