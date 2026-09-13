using Homassy.API.Models.ApplicationSettings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Homassy.Tests.Unit;

/// <summary>
/// The rate limits used to be read and <c>int.Parse</c>d out of IConfiguration on every request,
/// so a typo in appsettings.json or in an environment variable turned every single request — the
/// health endpoints included — into a 500, rather than failing at startup where somebody would
/// notice. They are options now, validated on start (#82).
/// </summary>
public class RateLimitSettingsTests
{
    private static IHost BuildHost(Dictionary<string, string?> rateLimiting)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(rateLimiting).Build();

        return new HostBuilder()
            .ConfigureServices(services => services
                .AddOptions<RateLimitSettings>()
                .Bind(configuration.GetSection("RateLimiting"))
                .ValidateDataAnnotations()
                .ValidateOnStart())
            .Build();
    }

    [Fact]
    public async Task AWellFormedConfiguration_StartsAndBindsEveryValue()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RateLimiting:GlobalMaxAttempts"] = "100",
            ["RateLimiting:GlobalWindowMinutes"] = "1",
            ["RateLimiting:EndpointMaxAttempts"] = "30",
            ["RateLimiting:EndpointWindowMinutes"] = "2"
        });

        await host.StartAsync();

        var settings = host.Services.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        Assert.Equal(100, settings.GlobalMaxAttempts);
        Assert.Equal(30, settings.EndpointMaxAttempts);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.GlobalWindow);
        Assert.Equal(TimeSpan.FromMinutes(2), settings.EndpointWindow);

        await host.StopAsync();
    }

    [Fact]
    public async Task ANonNumericValue_StopsTheHostFromStarting()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RateLimiting:GlobalMaxAttempts"] = "one hundred",
            ["RateLimiting:GlobalWindowMinutes"] = "1",
            ["RateLimiting:EndpointMaxAttempts"] = "30",
            ["RateLimiting:EndpointWindowMinutes"] = "1"
        });

        await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
    }

    [Theory]
    [InlineData("RateLimiting:GlobalMaxAttempts", "0")]
    [InlineData("RateLimiting:GlobalMaxAttempts", "-1")]
    [InlineData("RateLimiting:GlobalWindowMinutes", "0")]
    [InlineData("RateLimiting:EndpointMaxAttempts", "0")]
    [InlineData("RateLimiting:EndpointWindowMinutes", "-5")]
    public async Task AnOutOfRangeValue_StopsTheHostFromStarting(string key, string value)
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RateLimiting:GlobalMaxAttempts"] = "100",
            ["RateLimiting:GlobalWindowMinutes"] = "1",
            ["RateLimiting:EndpointMaxAttempts"] = "30",
            ["RateLimiting:EndpointWindowMinutes"] = "1",
            [key] = value
        });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    /// <summary>A section that is absent entirely falls back to the production defaults.</summary>
    [Fact]
    public async Task AMissingSection_FallsBackToTheDefaults()
    {
        using var host = BuildHost([]);

        await host.StartAsync();

        var settings = host.Services.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        Assert.Equal(100, settings.GlobalMaxAttempts);
        Assert.Equal(30, settings.EndpointMaxAttempts);

        await host.StopAsync();
    }
}
