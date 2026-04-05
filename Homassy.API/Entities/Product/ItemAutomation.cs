using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    public class ItemAutomation : RecordChangeEntity
    {
        [ForeignKey(nameof(ProductInventoryItem))]
        public int? ProductInventoryItemId { get; set; }

        [ForeignKey(nameof(Product))]
        public int? ProductId { get; set; }

        [ForeignKey(nameof(ShoppingList))]
        public int? ShoppingListId { get; set; }

        public int? FamilyId { get; set; }
        public int? UserId { get; set; }

        /// <summary>
        /// The user who created this automation. Always set, even for family-shared automations.
        /// </summary>
        public int CreatedByUserId { get; set; }

        [EnumDataType(typeof(ScheduleType))]
        public required ScheduleType ScheduleType { get; set; }

        public int? IntervalDays { get; set; }

        [EnumDataType(typeof(DaysOfWeek))]
        public DaysOfWeek? ScheduledDaysOfWeek { get; set; }

        public int? ScheduledDayOfMonth { get; set; }

        public required TimeOnly ScheduledTime { get; set; }

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

        public bool IsEnabled { get; set; } = true;

        public DateTime? NextExecutionAt { get; set; }

        public DateTime? LastExecutedAt { get; set; }

        // Navigation properties
        public ProductInventoryItem? ProductInventoryItem { get; set; }
        public Product? Product { get; set; }
        public ShoppingList.ShoppingList? ShoppingList { get; set; }
        public ICollection<ItemAutomationExecution>? Executions { get; set; }
    }
}
