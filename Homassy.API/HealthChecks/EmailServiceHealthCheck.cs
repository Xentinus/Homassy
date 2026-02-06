using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Homassy.API.Models.HealthCheck;

namespace Homassy.API.HealthChecks;

public class EmailServiceHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly HealthCheckOptions _options;

    public EmailServiceHealthCheck(IConfiguration configuration, IOptions<HealthCheckOptions> options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        
        _configuration = configuration;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var smtpServer = _configuration["Email:SmtpServer"];
        var smtpPortStr = _configuration["Email:SmtpPort"];

        if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpPortStr))
        {
            // Return Degraded instead of Unhealthy when email is not configured
            // This allows the app to run without email (e.g., in development with Kratos handling emails)
            return HealthCheckResult.Degraded(
                "Email configuration is missing",
                data: new Dictionary<string, object>
                {
                    ["configured"] = false
                });
        }

        var smtpPort = int.Parse(smtpPortStr);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls, cts.Token);
            await client.DisconnectAsync(true, cts.Token);

            return HealthCheckResult.Healthy("Email service is reachable");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"Email service connection timed out after {_options.TimeoutSeconds} seconds",
                data: new Dictionary<string, object>
                {
                    ["timeoutSeconds"] = _options.TimeoutSeconds
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Email service is unreachable: {ex.Message}",
                ex);
        }
    }
}
