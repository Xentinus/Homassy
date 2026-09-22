namespace Homassy.API.Models.Family
{
    /// <summary>
    /// One household member who has the app open right now, collapsed across that member's
    /// simultaneous connections. Tracked by <see cref="Hubs.FamilyPresence"/> and pushed to the
    /// household's group by <see cref="Hubs.PresenceRealtime"/>, which is what the home screen's
    /// member strip renders.
    /// </summary>
    /// <remarks>
    /// Deliberately the same shape as <see cref="ShoppingList.PresenceMemberInfo"/> plus the two
    /// shopping fields: the web client renders both through the same avatar component, and a
    /// second, subtly different member DTO is how the two drift apart.
    /// </remarks>
    public record FamilyPresenceMemberInfo
    {
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Path to this member's avatar thumbnail, or null when they have none. See <see cref="Constants.MediaUrls"/>.</summary>
        public string? ProfilePictureUrl { get; init; }

        /// <summary>Chosen identity-colour key from the curated palette, or null for the deterministic pick.</summary>
        public string? IdentityColor { get; init; }

        /// <summary>How many of this member's connections currently have the app open.</summary>
        public int DeviceCount { get; init; }

        /// <summary>True while at least one of this member's connections is in in-store shopping mode.</summary>
        public bool IsShopping { get; init; }

        /// <summary>
        /// The name of the shopping list being shopped, when <see cref="IsShopping"/> is true and
        /// the list could be resolved. Server-resolved from the list's public id rather than taken
        /// from the client, so a connection cannot broadcast arbitrary text to the household.
        /// </summary>
        public string? ShoppingContext { get; init; }
    }
}
