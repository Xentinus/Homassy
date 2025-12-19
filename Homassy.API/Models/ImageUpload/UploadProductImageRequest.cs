using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ImageUpload
{
    public record UploadProductImageRequest
    {
        [Required]
        public required Guid ProductPublicId { get; init; }

        [Required]
        [Base64String]
        public required string ImageBase64 { get; init; }
    }
}
