using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class MoveInventoryItemsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one inventory item is required")]
        public required List<Guid> InventoryItemPublicIds { get; set; }

        [Required]
        public required Guid StorageLocationPublicId { get; set; }
    }
}
