namespace Homassy.API.Models.ShoppingList
{
    public class ShoppingListInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public bool IsSharedWithFamily { get; set; }
    }
}
