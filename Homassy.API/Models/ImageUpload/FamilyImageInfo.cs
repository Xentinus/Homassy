using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record FamilyImageInfo
    {
        /// <summary>
        /// Versioned path to the newly stored family picture. A different URL from the one the old
        /// picture had, so the caller binds it straight to an <c>&lt;img&gt;</c> with no
        /// cache-busting query of its own.
        /// </summary>
        public required string FamilyPictureUrl { get; init; }

        public required ImageFormat Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required long FileSizeBytes { get; init; }
    }
}
