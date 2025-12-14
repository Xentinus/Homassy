namespace Homassy.API.Models.Common;

public record VersionInfo
{
    public required string Version { get; init; }
    public required string ShortVersion { get; init; }
    public required string BuildType { get; init; }
    public required string BuildDate { get; init; }
}
