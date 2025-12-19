using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record ProcessedImage
    {
        public required byte[] Data { get; init; }
        public required string Base64 { get; init; }
        public required ImageFormat Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required long FileSizeBytes { get; init; }
        public required string ContentType { get; init; }
    }
}
