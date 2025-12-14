using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product;

public class QuickAddMultipleInventoryItemsRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required")]
    public required List<QuickAddMultipleInventoryItemEntry> Items { get; set; }

    public Guid? StorageLocationPublicId { get; set; }

    public bool IsSharedWithFamily { get; set; } = false;
}
