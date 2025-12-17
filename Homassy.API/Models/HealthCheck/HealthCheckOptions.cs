namespace Homassy.API.Models.HealthCheck;

public class HealthCheckOptions
{
    public string OpenFoodFactsTestBarcode { get; set; } = "3017620422003";
    public int TimeoutSeconds { get; set; } = 10;
}
