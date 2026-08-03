namespace Homassy.API.Models.ShoppingList
{
    public class ShoppingListInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public bool IsSharedWithFamily { get; set; }

        /// <summary>
        /// Items still to buy (PurchasedAt == null). Deliberately not a total: the item cache only holds
        /// items purchased within the last 7 days, so a total would differ between the cache and database
        /// read paths, while the unpurchased count is identical on both.
        /// </summary>
        public int PendingItemCount { get; set; }
    }
}
