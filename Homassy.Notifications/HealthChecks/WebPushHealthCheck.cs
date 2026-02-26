using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Homassy.Notifications.HealthChecks;

public sealed class WebPushHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public WebPushHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var subject = _configuration["WebPush:VapidSubject"];
        var publicKey = _configuration["WebPush:VapidPublicKey"];
        var privateKey = _configuration["WebPush:VapidPrivateKey"];

        var missing = new List<string>();
        if (string.IsNullOrEmpty(subject)) missing.Add("WebPush:VapidSubject");
        if (string.IsNullOrEmpty(publicKey)) missing.Add("WebPush:VapidPublicKey");
        if (string.IsNullOrEmpty(privateKey)) missing.Add("WebPush:VapidPrivateKey");

        if (missing.Count > 0)
        {
            var result = HealthCheckResult.Unhealthy(
                $"Missing VAPID configuration: {string.Join(", ", missing)}");
            return Task.FromResult(result);
        }

        return Task.FromResult(HealthCheckResult.Healthy("VAPID configuration is present."));
    }
}
