namespace Homassy.API.Models.Product
{
    public class SplitInventoryItemResponse
    {
        public required InventoryItemInfo OriginalItem { get; set; }
        public required InventoryItemInfo NewItem { get; set; }
    }
}
