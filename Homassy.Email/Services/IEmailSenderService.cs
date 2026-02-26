using Homassy.Email.Models;

namespace Homassy.Email.Services;

public interface IEmailSenderService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
