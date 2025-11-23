namespace Homassy.API.Entities
{
    public class ShoppingLocation : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
