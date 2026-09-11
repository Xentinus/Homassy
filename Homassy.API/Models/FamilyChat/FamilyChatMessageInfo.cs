using Homassy.API.Enums;

namespace Homassy.API.Models.FamilyChat
{
    /// <summary>One message as the stream renders it (#144).</summary>
    public record FamilyChatMessageInfo
    {
        public Guid PublicId { get; init; }

        public FamilyChatMessageKind Kind { get; init; }

        /// <summary>The text, or an image message's caption. Null for a picture sent without one.</summary>
        public string? Body { get; init; }

        public DateTime SentAt { get; init; }

        public DateTime? EditedAt { get; init; }

        public FamilyChatSenderInfo Sender { get; init; } = new();

        /// <summary>
        /// Path to this message's picture, thumbnail rendition, or null for a text message (#147).
        /// </summary>
        /// <remarks>
        /// A path, not bytes, for the same reason <see cref="FamilyChatSenderInfo"/> carries one:
        /// the stream loads hundreds of rows, and an image endpoint answer is cacheable by both
        /// the browser and the service worker while a base64 field in a JSON payload is neither.
        /// </remarks>
        public string? ImageUrl { get; init; }

        /// <summary>Path to the full-size rendition, for the tap-to-open viewer (#147).</summary>
        public string? ImageFullUrl { get; init; }

        /// <summary>Aspect ratio of the picture, so the stream can reserve its box before the bytes arrive (#147).</summary>
        public int? ImageWidth { get; init; }

        public int? ImageHeight { get; init; }
    }
}
