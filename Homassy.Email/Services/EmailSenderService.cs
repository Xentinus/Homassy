using Homassy.Email.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Homassy.Email.Services;

public sealed class EmailSenderService : IEmailSenderService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSenderService> _logger;

    public EmailSenderService(IConfiguration configuration, ILogger<EmailSenderService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var smtpServer = _configuration["Email:SmtpServer"] ?? throw new InvalidOperationException("Email:SmtpServer is not configured.");
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var senderEmail = _configuration["Email:SenderEmail"] ?? throw new InvalidOperationException("Email:SenderEmail is not configured.");
        var username = _configuration["Email:Username"] ?? throw new InvalidOperationException("Email:Username is not configured.");
        var password = _configuration["Email:Password"] ?? throw new InvalidOperationException("Email:Password is not configured.");

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress("Homassy", senderEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody
        };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(username, password, ct);
        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent to {To} subject {Subject}", message.To, message.Subject);
    }
}
