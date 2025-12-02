using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Location
{
    public class ShoppingLocation : RecordChangeEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        [StringLength(128, MinimumLength = 4)]
        public required string Name { get; set; }
        [StringLength(255)]
        public string? Description { get; set; }
        [StringLength(64)]
        public string? Address { get; set; }
        [StringLength(64)]
        public string? City { get; set; }
        [DataType(DataType.PostalCode)]
        [StringLength(20)]
        public string? PostalCode { get; set; }
        [StringLength(64)]
        public string? Country { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
