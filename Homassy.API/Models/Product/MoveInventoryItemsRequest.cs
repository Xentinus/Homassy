using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    /// <summary>
    /// Request model for moving multiple inventory items to a new storage location.
    /// </summary>
    public class MoveInventoryItemsRequest
    {
        /// <summary>
        /// List of inventory item public IDs to move.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one inventory item is required")]
        public required List<Guid> InventoryItemPublicIds { get; set; }

        /// <summary>
        /// Target storage location public ID.
        /// </summary>
        [Required]
        public required Guid StorageLocationPublicId { get; set; }
    }
}
