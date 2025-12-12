using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace Homassy.API.Models.Common
{
    public record SelectValue
    {
        [Required]
        public Guid PublicId { get; init; }

        [Required]
        [MinLength(2)]
        public string Text { get; init; } = null!;
    }
}
