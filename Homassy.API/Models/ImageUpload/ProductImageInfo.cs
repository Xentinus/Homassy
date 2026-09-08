using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record ProductImageInfo
    {
        public required Guid ProductPublicId { get; init; }

        /// <summary>Versioned path to the list-sized thumbnail of the newly stored picture.</summary>
        public required string ProductImageUrl { get; init; }

        /// <summary>Versioned path to the full-size rendition.</summary>
        public required string ProductImageFullUrl { get; init; }
        public required ImageFormat Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required long FileSizeBytes { get; init; }
    }
}
