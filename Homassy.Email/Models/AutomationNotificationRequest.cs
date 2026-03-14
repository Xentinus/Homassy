namespace Homassy.Email.Models;

public sealed record AutomationNotificationRequest(
    string To,
    string Language,        // "hu" | "de" | "en"
    string? Name,
    string ProductName,
    string ActionType,      // "auto_consume" | "notify_only" | "insufficient_quantity"
    decimal? ConsumedQuantity,
    string? Unit
);
