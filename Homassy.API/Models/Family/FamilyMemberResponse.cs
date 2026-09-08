namespace Homassy.API.Models.Family
{
    public record FamilyMemberResponse
    {
        public Guid PublicId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public DateTime LastLoginAt { get; init; }
        public string? ProfilePictureUrl { get; init; }
        public bool IsCurrentUser { get; init; }

        /// <summary>Chosen identity-colour key from the curated palette, or null for the deterministic pick.</summary>
        public string? IdentityColor { get; init; }
    }
}
