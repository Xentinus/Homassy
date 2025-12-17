namespace Homassy.API.Models.ApplicationSettings;

public class EndpointTimeoutSettings
{
    public string PathPattern { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
}
