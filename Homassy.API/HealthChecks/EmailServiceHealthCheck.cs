// DEPRECATED: This file is kept for backward compatibility only.
// Email health checks are now handled via Kratos health endpoint.
// This file should be deleted in future cleanup.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Homassy.API.HealthChecks;

[Obsolete("Email is now handled by Ory Kratos Courier. This class will be removed.")]
public class EmailServiceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Email is now handled by Kratos Courier - always return healthy
        return Task.FromResult(HealthCheckResult.Healthy("Email handled by Kratos Courier"));
    }
}
