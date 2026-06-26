namespace Homassy.API.Models.Family
{
    /// <summary>
    /// The current user's own pending request to join a family.
    /// </summary>
    public record MyJoinRequestResponse
    {
        public Guid PublicId { get; init; }
        public string FamilyName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime RequestedAt { get; init; }
    }
}
