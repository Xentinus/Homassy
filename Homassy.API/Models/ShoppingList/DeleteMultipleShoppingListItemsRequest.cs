using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class DeleteMultipleShoppingListItemsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<Guid> ItemPublicIds { get; set; } = [];
    }
}
