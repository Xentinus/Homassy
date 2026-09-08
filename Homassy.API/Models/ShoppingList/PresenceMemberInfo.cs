namespace Homassy.API.Models.ShoppingList
{
    /// <summary>
    /// One member currently present on a shopping list — i.e. with it open in at least one
    /// connected browser tab or device — collapsed across that member's simultaneous connections.
    /// Built by <see cref="Hubs.ShoppingListHub"/> from the joining user's own data and tracked by
    /// <see cref="Hubs.ShoppingListPresence"/>, which is what produces <see cref="DeviceCount"/>.
    /// </summary>
    public record PresenceMemberInfo
    {
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Path to this member's avatar thumbnail, or null when they have none. See <see cref="Constants.MediaUrls"/>.</summary>
        public string? ProfilePictureUrl { get; init; }

        /// <summary>Chosen identity-colour key from the curated palette, or null for the deterministic pick.</summary>
        public string? IdentityColor { get; init; }

        /// <summary>How many of this member's connections currently have the list open.</summary>
        public int DeviceCount { get; init; }
    }
}
