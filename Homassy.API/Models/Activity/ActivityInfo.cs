using Homassy.API.Enums;

namespace Homassy.API.Models.Activity
{
    public record ActivityInfo
    {
        public Guid PublicId { get; init; }
        public Guid UserPublicId { get; init; }
        public string UserName { get; init; } = string.Empty;

        /// <summary>The actor's chosen identity-colour key, or null for the deterministic pick. See <see cref="Entities.User.UserProfile.IdentityColor"/>.</summary>
        public string? IdentityColor { get; init; }

        public DateTime Timestamp { get; init; }
        public ActivityType ActivityType { get; init; }
        public string RecordName { get; init; } = string.Empty;
        public Unit? Unit { get; init; }
        public decimal? Quantity { get; init; }
    }
}
