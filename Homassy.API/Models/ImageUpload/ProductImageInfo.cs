using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record ProductImageInfo
    {
        public required Guid ProductPublicId { get; init; }
        public required string ImageBase64 { get; init; }
        public required ImageFormat Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required long FileSizeBytes { get; init; }
    }
}
