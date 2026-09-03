using Homassy.API.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Homassy.Tests.Infrastructure;

/// <summary>
/// Loads the same configuration the test host uses, for the unit tests that need a
/// <see cref="HomassyDbContext"/> without standing up a host.
/// </summary>
/// <remarks>
/// <c>ConfigService</c> is still a process-wide static, and xUnit runs test classes in parallel,
/// so a unit test that touches it must install the <em>real</em> configuration rather than a
/// placeholder. The context itself no longer has a static hook: it is always built from options,
/// which is what <see cref="DbContextFactory"/> supplies.
/// </remarks>
public static class TestConfiguration
{
    private static readonly Lazy<IConfiguration> Instance = new(Build, isThreadSafe: true);

    private static readonly Lazy<IDbContextFactory<HomassyDbContext>> FactoryInstance =
        new(BuildDbContextFactory, isThreadSafe: true);

    public static IConfiguration Configuration => Instance.Value;

    /// <summary>
    /// A context factory over the test configuration, equivalent to the one the host registers.
    /// </summary>
    public static IDbContextFactory<HomassyDbContext> DbContextFactory => FactoryInstance.Value;

    private static IConfiguration Build()
    {
        var projectDir = Directory.GetCurrentDirectory();

        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(projectDir, "..", "Homassy.API", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(projectDir, "appsettings.Testing.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static IDbContextFactory<HomassyDbContext> BuildDbContextFactory()
    {
        var options = new DbContextOptionsBuilder<HomassyDbContext>()
            .UseNpgsql(Configuration.GetConnectionString("DefaultConnection"))
            .Options;

        return new OptionsDbContextFactory(options);
    }

    private sealed class OptionsDbContextFactory : IDbContextFactory<HomassyDbContext>
    {
        private readonly DbContextOptions<HomassyDbContext> _options;

        public OptionsDbContextFactory(DbContextOptions<HomassyDbContext> options)
        {
            _options = options;
        }

        public HomassyDbContext CreateDbContext() => new(_options);
    }
}
