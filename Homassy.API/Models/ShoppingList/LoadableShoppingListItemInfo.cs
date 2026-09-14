using Homassy.Data.Enums;

namespace Homassy.API.Models.ShoppingList
{
    /// <summary>
    /// One shopping list item offered by the "load inventory from shopping list" picker (#63).
    /// </summary>
    /// <remarks>
    /// Flat on purpose. The picker spans every list the caller can see, so a row has to carry
    /// enough of its own context - which list it came from, when it was bought and where - for two
    /// rows of the same product from two different trips to stay distinguishable without the client
    /// resolving anything.
    /// </remarks>
    public class LoadableShoppingListItemInfo
    {
        public Guid PublicId { get; set; }

        public Guid ShoppingListPublicId { get; set; }

        public string ShoppingListName { get; set; } = string.Empty;

        /// <summary>True when the source list is the family's rather than the caller's own.</summary>
        public bool IsSharedWithFamily { get; set; }

        public Guid ProductPublicId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string? ProductBrand { get; set; }

        public decimal Quantity { get; set; }

        public Unit Unit { get; set; }

        public string? Note { get; set; }

        /// <summary>When it was bought, or null for an item that is still outstanding.</summary>
        public DateTime? PurchasedAt { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        public string? ShoppingLocationName { get; set; }
    }
}
