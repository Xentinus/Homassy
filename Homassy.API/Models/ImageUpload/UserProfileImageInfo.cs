using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record UserProfileImageInfo
    {
        /// <summary>
        /// Versioned path to the newly stored avatar. The caller can bind it straight to an
        /// <c>&lt;img&gt;</c> — it is a different URL from the one the old picture had, so no
        /// cache-busting query of the client's own is needed.
        /// </summary>
        public required string ProfilePictureUrl { get; init; }

        public required ImageFormat Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required long FileSizeBytes { get; init; }
    }
}
