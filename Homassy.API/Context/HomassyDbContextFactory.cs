using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Homassy.API.Context;

/// <summary>
/// Design-time factory for HomassyDbContext to support EF Core migrations.
/// </summary>
public class HomassyDbContextFactory : IDesignTimeDbContextFactory<HomassyDbContext>
{
    public HomassyDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Set configuration for HomassyDbContext
        HomassyDbContext.SetConfiguration(configuration);

        // Configure DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<HomassyDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        return new HomassyDbContext(optionsBuilder.Options);
    }
}