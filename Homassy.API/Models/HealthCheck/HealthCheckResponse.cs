namespace Homassy.API.Models.HealthCheck;

public class HealthCheckResponse
{
    public required string Status { get; init; }
    public required string Duration { get; init; }
    public required Dictionary<string, DependencyHealth> Dependencies { get; init; }
}
