using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class UpdateShoppingListRequest
    {
        [StringLength(128, MinimumLength = 2)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code")]
        public string? Color { get; set; }

        public bool? IsSharedWithFamily { get; set; }
    }
}
