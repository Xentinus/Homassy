using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ImageUpload
{
    public record UploadUserProfileImageRequest
    {
        [Required]
        [Base64String]
        public required string ImageBase64 { get; init; }
    }
}
