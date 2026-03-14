using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    public class ItemAutomationExecution : RecordChangeEntity
    {
        [ForeignKey(nameof(ItemAutomation))]
        public required int ItemAutomationId { get; set; }

        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        [EnumDataType(typeof(AutomationExecutionStatus))]
        public required AutomationExecutionStatus Status { get; set; }

        public decimal? ConsumedQuantity { get; set; }

        [StringLength(512)]
        public string? Notes { get; set; }

        public int? TriggeredByUserId { get; set; }

        // Navigation properties
        public ItemAutomation ItemAutomation { get; set; } = null!;
    }
}
