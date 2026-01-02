namespace Homassy.API.Enums
{
    public enum ActivityType
    {
        // Product Activities (1-6)
        ProductCreate = 1,
        ProductUpdate = 2,
        ProductDelete = 3,
        ProductPhotoUpload = 4,
        ProductPhotoDelete = 5,
        ProductPhotoDownloadFromOpenFoodFacts = 6,

        // ProductInventory Activities (7-10)
        ProductInventoryCreate = 7,
        ProductInventoryUpdate = 8,
        ProductInventoryDecrease = 9,
        ProductInventoryDelete = 10,

        // ShoppingList Activities (11-13)
        ShoppingListCreate = 11,
        ShoppingListUpdate = 12,
        ShoppingListDelete = 13,

        // ShoppingListItem Activities (14-17, 21-22)
        ShoppingListItemAdd = 14,
        ShoppingListItemUpdate = 15,
        ShoppingListItemPurchase = 16,
        ShoppingListItemDelete = 17,

        // Family Activities (18-20)
        FamilyCreate = 18,
        FamilyJoin = 19,
        FamilyLeave = 20,

        // ShoppingListItem Activities (continued)
        ShoppingListItemQuickPurchase = 21,
        ShoppingListItemRestorePurchase = 22
    }
}
