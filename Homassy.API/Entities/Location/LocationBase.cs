using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Location
{
    public abstract class LocationBase : RecordChangeEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }

        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code")]
        public string? Color { get; set; }
    }
}
