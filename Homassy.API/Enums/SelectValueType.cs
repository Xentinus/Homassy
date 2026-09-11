namespace Homassy.API.Enums
{
    public enum SelectValueType
    {
        ShoppingLocation = 0,
        StorageLocation = 1,
        Product = 2,
        ProductInventoryItem = 3,
        ShoppingList = 4,
        Languages = 5,
        Currencies = 6,
        TimeZones = 7,
        ProductCategory = 8,

        /// <summary>
        /// Every product in the catalogue, not only the ones this user has stock of.
        /// </summary>
        /// <remarks>
        /// <see cref="Product"/> answers "what do I have at home", which is what a stock picker
        /// wants and what most of the app asks for. This one answers "what exists", which is what
        /// you need to point at something you are out of - the family chat's attachment picker
        /// (#147/R9 follow-up), where "buy this" is the commonest thing to say about a product you
        /// do not currently own.
        /// </remarks>
        ProductCatalog = 9,
    }
}
