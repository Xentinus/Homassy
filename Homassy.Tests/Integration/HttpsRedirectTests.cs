using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Homassy.API.Models.ApplicationSettings;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class HttpsRedirectTests : IClassFixture<HttpsRedirectTests.HttpsEnabledWebApplicationFactory>
{
    private readonly HttpsEnabledWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public HttpsRedirectTests(HttpsEnabledWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public void WhenHttpsEnabledThenConfigurationIsCorrectlyLoaded()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<HttpsSettings>>();

        _output.WriteLine($"Https:Enabled = {options.Value.Enabled}");
        _output.WriteLine($"Https:HttpsPort = {options.Value.HttpsPort}");

        Assert.True(options.Value.Enabled);
        Assert.Equal(5001, options.Value.HttpsPort);
    }

    [Fact]
    public void WhenHttpsEnabledThenHstsSettingsAreCorrectlyLoaded()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<HttpsSettings>>();

        _output.WriteLine($"Hsts:Enabled = {options.Value.Hsts.Enabled}");
        _output.WriteLine($"Hsts:MaxAgeDays = {options.Value.Hsts.MaxAgeDays}");
        _output.WriteLine($"Hsts:IncludeSubDomains = {options.Value.Hsts.IncludeSubDomains}");
        _output.WriteLine($"Hsts:Preload = {options.Value.Hsts.Preload}");

        Assert.True(options.Value.Hsts.Enabled);
        Assert.Equal(365, options.Value.Hsts.MaxAgeDays);
        Assert.True(options.Value.Hsts.IncludeSubDomains);
        Assert.False(options.Value.Hsts.Preload);
    }

    [Fact]
    public async Task WhenHttpsEnabledThenSecurityHeadersArePresent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1.0/auth/me");

        _output.WriteLine($"X-Content-Type-Options: {response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff)}");
        _output.WriteLine($"X-Frame-Options: {response.Headers.TryGetValues("X-Frame-Options", out var frameOptions)}");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
    }

    public class HttpsEnabledWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.Sources.Clear();
                config.AddJsonFile(Path.Combine(projectDir, "..", "Homassy.API", "appsettings.json"), optional: true);
                config.AddJsonFile(Path.Combine(projectDir, "appsettings.Testing.json"), optional: true);
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Https:Enabled"] = "true",
                    ["Https:HttpsPort"] = "5001",
                    ["Https:Hsts:Enabled"] = "true",
                    ["Https:Hsts:MaxAgeDays"] = "365",
                    ["Https:Hsts:IncludeSubDomains"] = "true",
                    ["Https:Hsts:Preload"] = "false"
                });
                config.AddEnvironmentVariables();
            });

            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                Homassy.API.Context.HomassyDbContext.SetConfiguration(configuration);
                Homassy.API.Services.ConfigService.Initialize(configuration);
            });

            return base.CreateHost(builder);
        }
    }
}

public class HttpsDisabledTests : IClassFixture<HttpsDisabledTests.HttpsDisabledWebApplicationFactory>
{
    private readonly HttpsDisabledWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public HttpsDisabledTests(HttpsDisabledWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public void WhenHttpsDisabledThenConfigurationIsCorrectlyLoaded()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<HttpsSettings>>();

        _output.WriteLine($"Https:Enabled = {options.Value.Enabled}");

        Assert.False(options.Value.Enabled);
    }

    [Fact]
    public async Task WhenHttpsDisabledThenRequestsStillWork()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1.0/auth/me");

        _output.WriteLine($"Status Code: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public class HttpsDisabledWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.Sources.Clear();
                config.AddJsonFile(Path.Combine(projectDir, "..", "Homassy.API", "appsettings.json"), optional: true);
                config.AddJsonFile(Path.Combine(projectDir, "appsettings.Testing.json"), optional: true);
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Https:Enabled"] = "false"
                });
                config.AddEnvironmentVariables();
            });

            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                Homassy.API.Context.HomassyDbContext.SetConfiguration(configuration);
                Homassy.API.Services.ConfigService.Initialize(configuration);
            });

            return base.CreateHost(builder);
        }
    }
}
