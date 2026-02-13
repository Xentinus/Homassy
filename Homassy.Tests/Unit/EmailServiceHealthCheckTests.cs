using Homassy.API.HealthChecks;
using Homassy.API.Models.HealthCheck;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Homassy.Tests.Unit;

public class EmailServiceHealthCheckTests
{
    private static EmailServiceHealthCheck CreateHealthCheck(
        Dictionary<string, string?>? configValues = null,
        HealthCheckOptions? options = null)
    {
        configValues ??= new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = "smtp.example.com",
            ["Email:SmtpPort"] = "587"
        };

        options ??= new HealthCheckOptions { TimeoutSeconds = 5 };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new EmailServiceHealthCheck(configuration, Options.Create(options));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSmtpServerNotConfigured_ReturnsDegraded()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = null,
            ["Email:SmtpPort"] = "587"
        };
        var healthCheck = CreateHealthCheck(config);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Returns Degraded (not Unhealthy) because email is optional when Kratos handles emails
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("configuration is missing", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSmtpPortNotConfigured_ReturnsDegraded()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = "smtp.example.com",
            ["Email:SmtpPort"] = null
        };
        var healthCheck = CreateHealthCheck(config);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Returns Degraded (not Unhealthy) because email is optional when Kratos handles emails
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("configuration is missing", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSmtpServerEmpty_ReturnsDegraded()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = "   ",
            ["Email:SmtpPort"] = "587"
        };
        var healthCheck = CreateHealthCheck(config);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Returns Degraded (not Unhealthy) because email is optional when Kratos handles emails
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConfigurationMissing_DataContainsConfiguredFalse()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = null,
            ["Email:SmtpPort"] = null
        };
        var healthCheck = CreateHealthCheck(config);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("configured"));
        Assert.Equal(false, result.Data["configured"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSmtpConnectionFails_ReturnsUnhealthy()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = "invalid.nonexistent.server.local",
            ["Email:SmtpPort"] = "587"
        };
        var options = new HealthCheckOptions { TimeoutSeconds = 2 };
        var healthCheck = CreateHealthCheck(config, options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSmtpConnectionFails_DoesNotExposeServerDetails()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:SmtpServer"] = "invalid.nonexistent.server.local",
            ["Email:SmtpPort"] = "587"
        };
        var options = new HealthCheckOptions { TimeoutSeconds = 2 };
        var healthCheck = CreateHealthCheck(config, options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.True(result.Data == null || !result.Data.ContainsKey("server"));
        Assert.True(result.Data == null || !result.Data.ContainsKey("port"));
    }

    [Fact]
    public void Constructor_WhenConfigurationIsNull_ThrowsArgumentNullException()
    {
        var options = Options.Create(new HealthCheckOptions());

        Assert.Throws<ArgumentNullException>(() => new EmailServiceHealthCheck(null!, options));
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Throws<ArgumentNullException>(() => new EmailServiceHealthCheck(config, null!));
    }
}
