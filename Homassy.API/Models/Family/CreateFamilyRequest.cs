using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Family
{
    public record CreateFamilyRequest
    {
        [Required]
        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public required string Name { get; init; }

        [StringLength(255)]
        [SanitizedString]
        public string? Description { get; init; }

        [Base64String]
        public string? FamilyPictureBase64 { get; init; }
    }
}
