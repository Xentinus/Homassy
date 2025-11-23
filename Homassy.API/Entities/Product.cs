using Homassy.API.Enums;

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
        public string? ProductPictureBase64 { get; set; }
        public bool IsEatable { get; set; } = true;
        public required Unit DefaultUnit { get; set; } = Unit.Gram;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
