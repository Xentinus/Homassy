namespace Homassy.API.Models.Location
{
    public class ShoppingLocationInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Website { get; set; }
        public string? GoogleMaps { get; set; }
        public bool IsSharedWithFamily { get; set; } = false;
    }
}
