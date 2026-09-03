using Homassy.API.Context;
using Microsoft.Extensions.Configuration;

namespace Homassy.Tests.Infrastructure;

/// <summary>
/// Loads the same configuration the test host uses and installs it on the static hooks
/// (<see cref="HomassyDbContext.SetConfiguration"/>, <c>ConfigService</c>).
/// </summary>
/// <remarks>
/// Those hooks are process-wide, and xUnit runs test classes in parallel. A unit test that only
/// needs a context object must therefore install the <em>real</em> configuration rather than a
/// placeholder connection string, or it will point another class's queries at a database that
/// does not exist while they are running.
/// </remarks>
public static class TestConfiguration
{
    private static readonly Lazy<IConfiguration> Instance = new(Build, isThreadSafe: true);

    public static IConfiguration Configuration => Instance.Value;

    /// <summary>Installs the test configuration on <see cref="HomassyDbContext"/>.</summary>
    public static void EnsureDbContextConfigured()
    {
        HomassyDbContext.SetConfiguration(Configuration);
    }

    private static IConfiguration Build()
    {
        var projectDir = Directory.GetCurrentDirectory();

        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(projectDir, "..", "Homassy.API", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(projectDir, "appsettings.Testing.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
