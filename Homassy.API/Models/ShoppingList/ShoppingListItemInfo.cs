namespace Homassy.API.Models.ShoppingList
{
    public class ShoppingListItemInfo
    {
        public Guid PublicId { get; set; }
        public Guid ShoppingListPublicId { get; set; }
        public Guid? ProductPublicId { get; set; }
        public Guid? ShoppingLocationPublicId { get; set; }
        public string? CustomName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime? PurchasedAt { get; set; }
        public DateTime? DeadlineAt { get; set; }
        public DateTime? DueAt { get; set; }
    }
}
