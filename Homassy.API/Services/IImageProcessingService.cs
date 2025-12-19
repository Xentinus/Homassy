using Homassy.API.Models.ImageUpload;

namespace Homassy.API.Services
{
    public interface IImageProcessingService
    {
        ImageValidationResult ValidateImage(string base64Image, ImageProcessingOptions? options = null);
        ProcessedImage? ProcessImage(string base64Image, ImageProcessingOptions? options = null);
        Task<ProcessedImage?> ProcessImageAsync(string base64Image, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default);
    }
}
