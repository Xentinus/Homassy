namespace Homassy.API.Models.Product
{
    public class DetailedProductInfo : ProductInfo
    {
        public List<InventoryItemInfo> InventoryItems { get; set; } = new();
    }
}
