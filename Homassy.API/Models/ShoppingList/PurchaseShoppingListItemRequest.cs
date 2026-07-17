using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    /// <summary>
    /// Lightweight quick-purchase from a shopping list item. Supports buying only part of the
    /// quantity (optionally keeping the remainder on the list) and recording where it was bought.
    /// Unlike <see cref="QuickPurchaseFromShoppingListItemRequest"/> this does not create inventory.
    /// </summary>
    public class PurchaseShoppingListItemRequest
    {
        [Required]
        public required Guid ShoppingListItemPublicId { get; set; }

        [Required]
        public required DateTime PurchasedAt { get; set; }

        /// <summary>
        /// How much of the item's quantity was bought. Null means the whole quantity.
        /// </summary>
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal? PurchasedQuantity { get; set; }

        /// <summary>
        /// When only part of the quantity was bought, whether the remainder stays on the list.
        /// True (default): the item's quantity is reduced and it stays on the list.
        /// False: the whole item is marked purchased regardless of the bought amount.
        /// </summary>
        public bool KeepRemainder { get; set; } = true;

        /// <summary>
        /// Shopping location where the item was bought. Null means "no change".
        /// </summary>
        public Guid? ShoppingLocationPublicId { get; set; }

        /// <summary>
        /// Clear the item's shopping location (needed because a null id means "no change").
        /// </summary>
        public bool ClearShoppingLocation { get; set; } = false;
    }
}
