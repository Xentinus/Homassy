using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class QuickPurchaseMultipleShoppingListItemsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<QuickPurchaseFromShoppingListItemRequest> Items { get; set; } = [];
    }
}
