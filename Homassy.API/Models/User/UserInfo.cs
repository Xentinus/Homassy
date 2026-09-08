namespace Homassy.API.Models.User
{
    public record UserInfo
    {
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// Path to this user's avatar thumbnail, versioned so it can be cached forever, or null
        /// when they have no picture. See <see cref="Constants.MediaUrls"/>.
        /// </summary>
        public string? ProfilePictureUrl { get; init; }

        public string TimeZone { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
    }
}
