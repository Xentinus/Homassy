using Homassy.API.Functions;
using Homassy.API.Models.ApplicationSettings;
using Homassy.API.Services;
using Homassy.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Homassy.Tests.Integration;

/// <summary>
/// Pins the parts of <c>Program.cs</c> that are configuration rather than code, and that the
/// tests around them would otherwise not notice the loss of.
/// </summary>
/// <remarks>
/// Both fixes in this area are one line of wiring plus a lot of behaviour that follows from it,
/// and both had tests that built their own host: <c>GracefulShutdownDrainTests</c> sets
/// <see cref="HostOptions.ShutdownTimeout"/> itself, and <c>RateLimitSettingsTests</c> spells out
/// its own <c>AddOptions().Bind().ValidateDataAnnotations().ValidateOnStart()</c> chain. Delete
/// either line from <c>Program.cs</c> and every one of those tests stays green while the app
/// falls back to a 5-second shutdown and to unvalidated limits. These assert against the real
/// application host, so the wiring itself is what is under test.
/// </remarks>
public class HostWiringTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HomassyWebApplicationFactory _factory;

    public HostWiringTests(HomassyWebApplicationFactory factory)
    {
        _factory = factory;
        // Forces the host to be built, which is also what would surface a ValidateOnStart failure.
        _ = factory.CreateClient();
    }

    /// <summary>
    /// The drain window is <see cref="HostOptions.ShutdownTimeout"/>, and it has to come from
    /// <c>GracefulShutdown:TimeoutSeconds</c> — the host's own default is 5 seconds, so without
    /// this the configured value would govern nothing (#85).
    /// </summary>
    [Fact]
    public void ShutdownTimeout_ComesFromTheGracefulShutdownSettings()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var configured = configuration.GetSection("GracefulShutdown").Get<GracefulShutdownSettings>()
                         ?? new GracefulShutdownSettings();

        Assert.True(configured.Enabled, "The Testing configuration is expected to leave graceful shutdown enabled.");

        var hostOptions = _factory.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(configured.TimeoutSeconds), hostOptions.ShutdownTimeout);
        Assert.NotEqual(TimeSpan.FromSeconds(5), hostOptions.ShutdownTimeout);   // the host's default
    }

    /// <summary>The tracker the shutdown log reads has to be a singleton, or the count is per-scope noise.</summary>
    [Fact]
    public void InFlightRequestTracker_IsRegisteredAsASingleton()
    {
        var first = _factory.Services.GetRequiredService<InFlightRequestTracker>();
        var second = _factory.Services.GetRequiredService<InFlightRequestTracker>();

        Assert.Same(first, second);
    }

    /// <summary>
    /// The limits are bound from the <c>RateLimiting</c> section rather than read per request (#82).
    /// </summary>
    [Fact]
    public void RateLimitSettings_AreBoundFromTheRateLimitingSection()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var settings = _factory.Services.GetRequiredService<IOptionsMonitor<RateLimitSettings>>().CurrentValue;

        Assert.Equal(int.Parse(configuration["RateLimiting:GlobalMaxAttempts"]!), settings.GlobalMaxAttempts);
        Assert.Equal(int.Parse(configuration["RateLimiting:EndpointMaxAttempts"]!), settings.EndpointMaxAttempts);
        Assert.Equal(int.Parse(configuration["RateLimiting:GlobalWindowMinutes"]!), settings.GlobalWindowMinutes);
        Assert.Equal(int.Parse(configuration["RateLimiting:EndpointWindowMinutes"]!), settings.EndpointWindowMinutes);
    }

    /// <summary>
    /// A malformed limit has to stop the host, not every request. That is
    /// <c>ValidateDataAnnotations().ValidateOnStart()</c>: the first registers the validator, the
    /// second registers the startup validator that runs it before the app serves anything.
    /// </summary>
    [Fact]
    public void RateLimitSettings_AreValidatedAtStartup()
    {
        Assert.NotEmpty(_factory.Services.GetServices<IValidateOptions<RateLimitSettings>>());
        Assert.NotNull(_factory.Services.GetService<IStartupValidator>());
    }

    /// <summary>
    /// Every class in the Functions layer resolves from a request scope.
    /// </summary>
    /// <remarks>
    /// The layer used to construct itself with <c>new</c>, which meant a missing registration or a
    /// constructor cycle was invisible until the endpoint that needed it was called - and a cycle
    /// is not a compile error, it is a stack overflow at runtime. Now that the classes take each
    /// other through their constructors (#133), resolving all of them from one scope is what says
    /// the graph is both complete and acyclic. A new dependency that closes a cycle fails here
    /// rather than in production.
    /// </remarks>
    [Fact]
    public void EveryFunctionsClass_ResolvesFromARequestScope()
    {
        var types = typeof(UserFunctions).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == typeof(UserFunctions).Namespace)
            .Where(t => t.Name.EndsWith("Functions", StringComparison.Ordinal) || t.Name == nameof(FamilyCache))
            // A class with only static members (UnitFunctions) is not a service and is not
            // registered; what this is about is the classes that take dependencies.
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Length > 0))
            .OrderBy(t => t.Name)
            .ToList();

        Assert.NotEmpty(types);

        using var scope = _factory.Services.CreateScope();

        foreach (var type in types)
        {
            var resolved = Record.Exception(() => scope.ServiceProvider.GetRequiredService(type));
            Assert.True(resolved is null, $"{type.Name} did not resolve: {resolved?.Message}");
        }
    }
}
