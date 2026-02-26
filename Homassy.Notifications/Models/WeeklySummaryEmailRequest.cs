namespace Homassy.Notifications.Models;

public sealed class WeeklySummaryEmailRequest
{
    public string To { get; init; } = string.Empty;
    public string Language { get; init; } = "hu";
    public string? Name { get; init; }
    public List<ExpiringProductDto> ExpiredItems { get; init; } = [];
    public List<ExpiringProductDto> ExpiringSoonItems { get; init; } = [];
}

public sealed record ExpiringProductDto(
    string Name,
    string Brand,
    DateTime ExpirationDate);
