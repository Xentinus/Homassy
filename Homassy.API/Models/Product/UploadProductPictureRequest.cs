using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public record UploadProductPictureRequest
    {
        [Required]
        [Base64String]
        public required string ProductPictureBase64 { get; init; }
    }
}
