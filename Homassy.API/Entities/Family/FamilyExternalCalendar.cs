using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Family
{
    public class FamilyExternalCalendar : RecordChangeEntity
    {
        public int FamilyId { get; set; }
        public Family Family { get; set; } = null!;

        [Required]
        [StringLength(64, MinimumLength = 2)]
        public required string Name { get; set; }

        [Required]
        [StringLength(2048)]
        public required string ICalUrl { get; set; }

        [StringLength(7)]
        public string Color { get; set; } = "#3B82F6";

        public bool IsEnabled { get; set; } = true;

        public DateTime? LastSyncedAt { get; set; }

        [StringLength(512)]
        public string? LastSyncError { get; set; }

        public string? CachedEventsJson { get; set; }
    }
}
