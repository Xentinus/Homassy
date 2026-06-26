namespace Homassy.API.Models.Family
{
    /// <summary>
    /// An incoming request to join the current user's family, shown to existing members.
    /// </summary>
    public record FamilyJoinRequestResponse
    {
        public Guid PublicId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? ProfilePictureBase64 { get; init; }
        public DateTime RequestedAt { get; init; }
    }
}
