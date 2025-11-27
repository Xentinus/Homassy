using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities
{
    public class StorageLocation : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        [StringLength(128, MinimumLength = 4)]
        public required string Name { get; set; }
        [StringLength(255)]
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
