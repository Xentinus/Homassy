using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class CreateShoppingListRequest
    {
        [Required]
        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public required string Name { get; set; }

        [StringLength(500)]
        [SanitizedString]
        public string? Description { get; set; }

        [StringLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code")]
        public string? Color { get; set; }

        public bool? IsSharedWithFamily { get; set; }
    }
}
