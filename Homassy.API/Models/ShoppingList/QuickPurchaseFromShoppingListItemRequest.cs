using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    /// <summary>
    /// Request model for quickly purchasing a shopping list item and converting it to an inventory item.
    /// Only works for non-custom items (items with a ProductPublicId).
    /// </summary>
    public class QuickPurchaseFromShoppingListItemRequest
    {
        /// <summary>
        /// The shopping list item to purchase.
        /// </summary>
        [Required]
        public required Guid ShoppingListItemPublicId { get; set; }

        /// <summary>
        /// When the item was purchased.
        /// </summary>
        [Required]
        public required DateTime PurchasedAt { get; set; }

        /// <summary>
        /// Quantity purchased.
        /// </summary>
        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }

        /// <summary>
        /// Purchase price.
        /// </summary>
        public int? Price { get; set; }

        /// <summary>
        /// Currency of the price.
        /// </summary>
        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }

        /// <summary>
        /// Target storage location for the new inventory item.
        /// </summary>
        public Guid? StorageLocationPublicId { get; set; }

        /// <summary>
        /// Expiration date for the inventory item.
        /// </summary>
        public DateTime? ExpirationAt { get; set; }

        /// <summary>
        /// Whether the inventory item should be shared with family.
        /// </summary>
        public bool IsSharedWithFamily { get; set; } = false;
    }
}
