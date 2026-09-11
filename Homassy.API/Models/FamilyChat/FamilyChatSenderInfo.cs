namespace Homassy.API.Models.FamilyChat
{
    /// <summary>
    /// Who sent a message, as much as the stream needs to draw them (#144).
    /// </summary>
    /// <remarks>
    /// The public id and a display name, plus the avatar <b>path</b> and the identity colour the
    /// rest of the app already uses for "who". Never the avatar bytes: a chat page is hundreds of
    /// rows and a base64 avatar repeated on each of them is the mistake #112 and #121 removed
    /// everywhere else. Never the internal user id either - nothing outside the API needs it, and
    /// a sequential id in a payload is an enumeration handle.
    /// </remarks>
    public record FamilyChatSenderInfo
    {
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Path to this member's avatar thumbnail, or null when they have none. See <see cref="Constants.MediaUrls"/>.</summary>
        public string? ProfilePictureUrl { get; init; }

        /// <summary>Chosen identity-colour key from the curated palette, or null for the deterministic pick.</summary>
        public string? IdentityColor { get; init; }
    }
}
