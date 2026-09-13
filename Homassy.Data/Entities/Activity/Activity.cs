using Homassy.Data.Entities.Common;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Data.Entities.Activity
{
    public class Activity : RecordChangeEntity
    {
        public int UserId { get; set; }

        public int? FamilyId { get; set; }

        public DateTime Timestamp { get; set; }

        [EnumDataType(typeof(ActivityType))]
        public ActivityType ActivityType { get; set; }

        public int RecordId { get; set; }

        [StringLength(256)]
        public string RecordName { get; set; } = string.Empty;

        [EnumDataType(typeof(Unit))]
        public Unit? Unit { get; set; }

        public decimal? Quantity { get; set; }
    }
}
