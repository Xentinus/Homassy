namespace Homassy.Email.Models;

public sealed record WeeklySummaryRequest(
    string To,
    string Language,        // "hu" | "de" | "en"
    string? Name,
    ExpiringProductDto[] ExpiredItems,
    ExpiringProductDto[] ExpiringSoonItems
);

public sealed record ExpiringProductDto(
    string Name,
    string? Brand,
    DateTimeOffset ExpirationDate
);
