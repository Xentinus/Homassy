using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    /// <summary>
    /// Request model for quickly adding multiple inventory items at once.
    /// </summary>
    public class QuickAddMultipleInventoryItemsRequest
    {
        /// <summary>
        /// List of items to add.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public required List<QuickAddMultipleInventoryItemEntry> Items { get; set; }

        /// <summary>
        /// Optional storage location for all items.
        /// </summary>
        public Guid? StorageLocationPublicId { get; set; }

        /// <summary>
        /// Whether items should be shared with family.
        /// </summary>
        public bool IsSharedWithFamily { get; set; } = false;
    }

    /// <summary>
    /// Single item entry for bulk inventory addition.
    /// </summary>
    public class QuickAddMultipleInventoryItemEntry
    {
        [Required]
        public required Guid ProductPublicId { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit Unit { get; set; } = Unit.Piece;
    }
}
