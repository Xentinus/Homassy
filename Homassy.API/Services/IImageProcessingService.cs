using Homassy.API.Models.ImageUpload;

namespace Homassy.API.Services
{
    public interface IImageProcessingService
    {
        ImageValidationResult ValidateImage(string base64Image, ImageProcessingOptions? options = null);
        ProcessedImage? ProcessImage(string base64Image, ImageProcessingOptions? options = null);
        Task<ProcessedImage?> ProcessImageAsync(string base64Image, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Centre-crops <paramref name="imageBytes"/> to a square and scales it to
        /// <paramref name="size"/> px, encoded as WebP.
        /// </summary>
        /// <remarks>
        /// Square because every consumer of a thumbnail is a fixed-size box (avatar, card
        /// thumbnail) — cropping here is what lets those boxes avoid <c>object-fit</c> surprises
        /// and lets the list ask for exactly the pixels it will show. Returns null if the bytes
        /// cannot be decoded.
        /// </remarks>
        ProcessedImage? CreateSquareThumbnail(byte[] imageBytes, int size, int quality = 75);

        /// <summary>
        /// Re-encodes <paramref name="imageBytes"/> as JPEG, for the rare client whose
        /// <c>Accept</c> header rules out the WebP a thumbnail is stored as.
        /// </summary>
        ProcessedImage? TranscodeToJpeg(byte[] imageBytes, int quality = 80);
    }
}
