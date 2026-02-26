namespace Homassy.Notifications.Models;

public sealed record ExpiringProductItem(
    string ProductName,
    string Brand,
    DateTime ExpirationDate,
    bool IsExpired);
