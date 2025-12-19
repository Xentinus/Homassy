using Homassy.API.Enums;

namespace Homassy.API.Models.ImageUpload
{
    public record ImageValidationResult
    {
        public bool IsValid { get; init; }
        public ImageValidationError Error { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public ImageFormat DetectedFormat { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public long FileSizeBytes { get; init; }

        public static ImageValidationResult Success(ImageFormat format, int width, int height, long fileSizeBytes)
        {
            return new ImageValidationResult
            {
                IsValid = true,
                Error = ImageValidationError.None,
                DetectedFormat = format,
                Width = width,
                Height = height,
                FileSizeBytes = fileSizeBytes
            };
        }

        public static ImageValidationResult Failure(ImageValidationError error, string errorMessage)
        {
            return new ImageValidationResult
            {
                IsValid = false,
                Error = error,
                ErrorMessage = errorMessage,
                DetectedFormat = ImageFormat.Unknown
            };
        }
    }
}
