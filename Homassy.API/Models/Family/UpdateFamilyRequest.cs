using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Family
{
    public record UpdateFamilyRequest
    {
        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public string? Name { get; init; }

        [StringLength(255)]
        [SanitizedString]
        public string? Description { get; init; }
    }
}
