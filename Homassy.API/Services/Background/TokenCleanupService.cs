using Serilog;

namespace Homassy.API.Services.Background;

/// <summary>
/// Token cleanup is now handled by Kratos. This service is kept as a no-op for backwards compatibility.
/// </summary>
public sealed class TokenCleanupService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Token cleanup service: Using Kratos for session management, no cleanup needed");
        return Task.CompletedTask;
    }
}
