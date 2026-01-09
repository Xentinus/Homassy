namespace Homassy.API.Models.Family
{
    public record FamilyMemberResponse
    {
        public Guid PublicId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public DateTime LastLoginAt { get; init; }
        public string? ProfilePictureBase64 { get; init; }
        public bool IsCurrentUser { get; init; }
    }
}
