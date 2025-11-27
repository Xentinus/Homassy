using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Family
{
    public record JoinFamilyRequest
    {
        [Required]
        [StringLength(8, MinimumLength = 8)]
        public required string ShareCode { get; init; }
    }
}
