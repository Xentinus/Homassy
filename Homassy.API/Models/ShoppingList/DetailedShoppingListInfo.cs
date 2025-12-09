namespace Homassy.API.Models.ShoppingList
{
    public class DetailedShoppingListInfo : ShoppingListInfo
    {
        public List<ShoppingListItemInfo> Items { get; set; } = new();
    }
}
