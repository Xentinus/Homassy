using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Family
{
    public record UpdateFamilyRequest
    {
        [StringLength(64, MinimumLength = 2)]
        public string? Name { get; init; }

        [StringLength(255)]
        public string? Description { get; init; }
    }
}
