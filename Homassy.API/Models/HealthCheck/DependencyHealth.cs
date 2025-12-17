namespace Homassy.API.Models.HealthCheck;

public class DependencyHealth
{
    public required string Status { get; init; }
    public required string Duration { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}
