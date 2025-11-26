using Homassy.API.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities
{
    public class Product : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public required string Name { get; set; }
        public required string Brand { get; set; }
        public string? Category { get; set; }
        public string? Barcode { get; set; }
        [Base64String]
        public string? ProductPictureBase64 { get; set; }
        public bool IsEatable { get; set; } = true;
        [EnumDataType(typeof(Unit))]
        public required Unit DefaultUnit { get; set; } = Unit.Gram;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
