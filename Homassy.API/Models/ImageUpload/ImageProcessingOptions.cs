using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record ImageProcessingOptions
    {
        public int MaxWidth { get; init; } = 800;
        public int MaxHeight { get; init; } = 800;
        public int MinWidth { get; init; } = 50;
        public int MinHeight { get; init; } = 50;
        public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;
        public int JpegQuality { get; init; } = 80;
        public ImageFormat OutputFormat { get; init; } = ImageFormat.Jpeg;
        public HashSet<ImageFormat> AllowedFormats { get; init; } = [ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.WebP];
    }
}
