using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Family
{
    public record UploadFamilyPictureRequest
    {
        [Required]
        [Base64String]
        public required string FamilyPictureBase64 { get; init; }
    }
}
