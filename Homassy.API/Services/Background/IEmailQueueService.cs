using Homassy.API.Models.Background;

namespace Homassy.API.Services.Background;

public interface IEmailQueueService
{
    ValueTask<bool> TryQueueEmailAsync(EmailTask task, CancellationToken cancellationToken = default);
}
