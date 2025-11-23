namespace Homassy.API.Entities
{
    public class StorageLocation : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
