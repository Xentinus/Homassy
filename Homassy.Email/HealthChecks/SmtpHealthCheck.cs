using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Homassy.Email.HealthChecks;

public sealed class SmtpHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpHealthCheck> _logger;

    public SmtpHealthCheck(IConfiguration configuration, ILogger<SmtpHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var smtpServer = _configuration["Email:SmtpServer"];
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];

        if (string.IsNullOrEmpty(smtpServer))
            return HealthCheckResult.Unhealthy("Email:SmtpServer is not configured.");

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls, cancellationToken);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                await client.AuthenticateAsync(username, password, cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);
            return HealthCheckResult.Healthy("SMTP connection successful.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP health check failed for {Server}:{Port}", smtpServer, smtpPort);
            return HealthCheckResult.Unhealthy("SMTP connection failed.", ex);
        }
    }
}
