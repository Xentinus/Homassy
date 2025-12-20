using Homassy.API.Enums;
using Serilog;

namespace Homassy.API.Services.Background;

public sealed class EmailBackgroundService : BackgroundService
{
    private readonly EmailQueueService _emailQueue;
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15)
    ];

    public EmailBackgroundService(EmailQueueService emailQueue)
    {
        _emailQueue = emailQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Email background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _emailQueue.DequeueAsync(stoppingToken);
                await ProcessEmailWithRetryAsync(task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in email background service");
            }
        }

        Log.Information("Email background service stopped");
    }

    private static async Task ProcessEmailWithRetryAsync(Models.Background.EmailTask task, CancellationToken stoppingToken)
    {
        for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                switch (task.Type)
                {
                    case EmailType.Verification:
                        await EmailService.SendVerificationCodeAsync(task.Email, task.Code, task.TimeZone, task.Language);
                        break;
                    case EmailType.Registration:
                        await EmailService.SendRegistrationEmailAsync(task.Email, task.Email, task.Code, task.TimeZone, task.Language);
                        break;
                    default:
                        Log.Warning($"Unknown email type: {task.Type}");
                        return;
                }

                Log.Debug($"Email sent successfully to {task.Email}, type: {task.Type}, language: {task.Language}");
                return;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts)
            {
                var delay = RetryDelays[attempt];
                Log.Warning(ex, $"Failed to send email to {task.Email}, attempt {attempt + 1}/{MaxRetryAttempts}. Retrying in {delay.TotalSeconds}s");
                
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to send email to {task.Email} after {MaxRetryAttempts} attempts");
            }
        }
    }
}
