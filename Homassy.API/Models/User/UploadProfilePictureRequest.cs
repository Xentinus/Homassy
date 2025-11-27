using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.User
{
    public record UploadProfilePictureRequest
    {
        [Required]
        [Base64String]
        public required string ProfilePictureBase64 { get; init; }
    }
}
