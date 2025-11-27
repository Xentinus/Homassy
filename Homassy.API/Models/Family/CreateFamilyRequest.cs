using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Family
{
    public record CreateFamilyRequest
    {
        [Required]
        [StringLength(64, MinimumLength = 2)]
        public required string Name { get; init; }

        [StringLength(255)]
        public string? Description { get; init; }

        [Base64String]
        public string? FamilyPictureBase64 { get; init; }
    }
}
