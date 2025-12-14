using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class CreateMultipleShoppingListItemsRequest
    {
        [Required]
        public required Guid ShoppingListPublicId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<CreateShoppingListItemEntry> Items { get; set; } = [];
    }
}
