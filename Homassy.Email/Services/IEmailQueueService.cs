using Homassy.Email.Models;

namespace Homassy.Email.Services;

public interface IEmailQueueService
{
    ValueTask<bool> TryEnqueueAsync(EmailMessage message, CancellationToken ct = default);
    ValueTask<EmailMessage> DequeueAsync(CancellationToken ct);
    int Count { get; }
}
