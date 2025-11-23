namespace Homassy.API.Entities
{
    public class Family : BaseEntity
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? FamilyPictureBase64 { get; set; }
    }
}
