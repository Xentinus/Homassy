namespace Homassy.Notifications.Models;

public sealed record LowStockPushRequest(
    int UserId,
    string ProductName,
    decimal TotalStock,
    decimal ThresholdQuantity,
    decimal AddQuantity,
    string Unit,
    string ShoppingListName);
