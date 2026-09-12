namespace Homassy.API.Enums
{
    /// <summary>
    /// Which entity type a global-search hit belongs to.
    /// </summary>
    /// <remarks>
    /// Serialized as its number: the API registers no string enum converter, so the client
    /// mirrors these values in its own <c>SearchResultKind</c> enum. Append only — the numbers
    /// are part of the wire format.
    /// </remarks>
    public enum SearchResultKind
    {
        Product = 0,
        InventoryItem = 1,
        ShoppingList = 2,
        ShoppingLocation = 3,
        StorageLocation = 4,
        Automation = 5,
    }
}
