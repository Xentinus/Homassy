using Homassy.Email.Services;

namespace Homassy.Email.Workers;

public sealed class EmailWorkerService : BackgroundService
{
    private static readonly TimeSpan[] Backoffs =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8)
    ];
    private const int MaxAttempts = 3;

    private readonly IEmailQueueService _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailWorkerService> _logger;

    public EmailWorkerService(
        IEmailQueueService queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailWorkerService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailWorkerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSenderService>();
                await sender.SendAsync(message, stoppingToken);

                _logger.LogInformation(
                    "Email sent to {To} subject {Subject} attempt {AttemptCount}",
                    message.To, message.Subject, message.AttemptCount);
            }
            catch (Exception ex)
            {
                var nextAttempt = message.AttemptCount + 1;

                if (nextAttempt >= MaxAttempts)
                {
                    _logger.LogError(ex,
                        "Permanent email failure for {To} subject {Subject} after {MaxAttempts} attempts",
                        message.To, message.Subject, MaxAttempts);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Email send failed for {To} subject {Subject}, attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s",
                        message.To, message.Subject, nextAttempt, MaxAttempts, Backoffs[message.AttemptCount].TotalSeconds);

                    await Task.Delay(Backoffs[message.AttemptCount], stoppingToken);

                    var retryMessage = message with { AttemptCount = nextAttempt };
                    await _queue.TryEnqueueAsync(retryMessage, stoppingToken);
                }
            }
        }
    }
}
