namespace Homassy.API.Models.ApplicationSettings;

public class GracefulShutdownSettings
{
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
}
