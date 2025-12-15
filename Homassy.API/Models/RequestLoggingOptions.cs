namespace Homassy.API.Models;

public class RequestLoggingOptions
{
    public bool Enabled { get; set; } = true;

    public List<string> DetailedPaths { get; set; } = [];

    public List<string> ExcludedPaths { get; set; } = [];
}
