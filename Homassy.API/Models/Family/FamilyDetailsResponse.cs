namespace Homassy.API.Models.Family
{
    public record FamilyDetailsResponse
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string ShareCode { get; init; } = string.Empty;
        public string? FamilyPictureBase64 { get; init; }
    }
}
