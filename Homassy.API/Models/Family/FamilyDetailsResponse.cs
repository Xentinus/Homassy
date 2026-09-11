namespace Homassy.API.Models.Family
{
    public record FamilyDetailsResponse
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string ShareCode { get; init; } = string.Empty;

        /// <summary>
        /// Versioned path to the family picture, or null when it has none. A path, never bytes -
        /// see <c>MediaUrls</c> for why.
        /// </summary>
        public string? FamilyPictureUrl { get; init; }
    }
}
