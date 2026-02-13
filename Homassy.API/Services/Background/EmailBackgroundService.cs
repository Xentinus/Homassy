// DEPRECATED: This file is kept for backward compatibility only.
// Email functionality is now handled by Ory Kratos Courier.
// This file should be deleted in future cleanup.

namespace Homassy.API.Services.Background;

[Obsolete("Email is now handled by Ory Kratos Courier. This class will be removed.")]
public sealed class EmailBackgroundService : BackgroundService
{
    public EmailBackgroundService(EmailQueueService emailQueue)
    {
        // No-op: Email is now handled by Kratos Courier
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // No-op: Email is now handled by Kratos Courier
        return Task.CompletedTask;
    }
}
