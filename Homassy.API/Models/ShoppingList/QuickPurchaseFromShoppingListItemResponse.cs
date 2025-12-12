using Homassy.API.Models.Product;

namespace Homassy.API.Models.ShoppingList
{
    /// <summary>
    /// Response model for the quick purchase from shopping list item operation.
    /// </summary>
    public class QuickPurchaseFromShoppingListItemResponse
    {
        /// <summary>
        /// The updated shopping list item with purchase date set.
        /// </summary>
        public required ShoppingListItemInfo ShoppingListItem { get; set; }

        /// <summary>
        /// The newly created inventory item.
        /// </summary>
        public required InventoryItemInfo InventoryItem { get; set; }
    }
}
