using Homassy.Data.Entities.Common;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Data.Entities.ShoppingList
{
    public class ShoppingListItem : RecordChangeEntity
    {
        // Shopping list reference
        public required int ShoppingListId { get; set; }
        public int? ProductId { get; set; }
        public int? ShoppingLocationId { get; set; }

        [StringLength(255, MinimumLength = 4)]
        public string? CustomName { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; } = 1.0m;

        [EnumDataType(typeof(Unit))]  
        public required Unit Unit { get; set; } = Unit.Gram;

        [StringLength(255)]
        public string? Note { get; set; }

        /// <summary>
        /// Manual (aisle) position within the list. Sparse — see <see cref="Functions.SparseOrdering"/>:
        /// values are gapped so moving one row rewrites one row. Zero on every pre-existing row, which
        /// leaves the list in the urgency-then-name order it had before anyone dragged anything.
        /// </summary>
        public int SortOrder { get; set; }

        public DateTime? PurchasedAt { get; set; }
        public DateTime? DeadlineAt { get; set; }
        public DateTime? DueAt { get; set; }

        /// <summary>
        /// When this item was turned into an inventory item by the "load inventory from shopping list"
        /// flow (#63). It is the marker that keeps an item out of that picker once it has been loaded,
        /// which <see cref="PurchasedAt"/> cannot do: an item can be marked purchased on the list
        /// without ever reaching the stock, and an item bought on an earlier trip is exactly what the
        /// picker is for.
        /// </summary>
        public DateTime? InventoryLoadedAt { get; set; }

        // Navigation properties
        public ShoppingList ShoppingList { get; set; } = null!;
    }
}
