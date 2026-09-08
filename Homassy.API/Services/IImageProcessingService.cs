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
        /// What avatars use: they are always drawn in a circle, so a square crop is what the UI
        /// would do anyway, and doing it here means the bytes are not paying for pixels that get
        /// clipped. Product pictures want <see cref="CreateBoundedThumbnail"/> instead. Returns
        /// null if the bytes cannot be decoded.
        /// </remarks>
        ProcessedImage? CreateSquareThumbnail(byte[] imageBytes, int size, int quality = 75);

        /// <summary>
        /// Scales <paramref name="imageBytes"/> down so neither side exceeds
        /// <paramref name="maxSize"/> px, keeping the aspect ratio and cropping nothing, encoded
        /// as WebP.
        /// </summary>
        /// <remarks>
        /// What product pictures use, as opposed to the square crop avatars get: a product card
        /// renders its image <c>object-contain</c>, so a centre crop would cut the top off a tall
        /// bottle or the ends off a wide label. Never scales up — an image already inside the box
        /// is only re-encoded. Returns null if the bytes cannot be decoded.
        /// </remarks>
        ProcessedImage? CreateBoundedThumbnail(byte[] imageBytes, int maxSize, int quality = 75);

        /// <summary>
        /// Re-encodes <paramref name="imageBytes"/> as JPEG, for the rare client whose
        /// <c>Accept</c> header rules out the WebP a thumbnail is stored as.
        /// </summary>
        ProcessedImage? TranscodeToJpeg(byte[] imageBytes, int quality = 80);
    }
}
