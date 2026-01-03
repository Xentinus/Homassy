using Homassy.API.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data.Common;

Console.WriteLine("=== Homassy Database Migrator ===");

try
{
    // Load configuration
    // Note: We use the API's appsettings.json from the published output
    // Environment variables will override these settings
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var connectionString = configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        Console.Error.WriteLine("ERROR: Connection string 'DefaultConnection' not found");
        Environment.Exit(1);
    }

    Console.WriteLine($"Connection string loaded: {MaskConnectionString(connectionString)}");

    // Wait for PostgreSQL to be ready
    Console.WriteLine("Waiting for PostgreSQL to be ready...");
    await WaitForDatabaseAsync(connectionString, maxAttempts: 30, delaySeconds: 2);

    // Set configuration for HomassyDbContext
    HomassyDbContext.SetConfiguration(configuration);

    // Run migrations
    Console.WriteLine("Applying database migrations...");

    var optionsBuilder = new DbContextOptionsBuilder<HomassyDbContext>();
    optionsBuilder.UseNpgsql(connectionString);

    await using var context = new HomassyDbContext(optionsBuilder.Options);

    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
    var pendingCount = pendingMigrations.Count();

    if (pendingCount == 0)
    {
        Console.WriteLine("No pending migrations. Database is up to date.");
    }
    else
    {
        Console.WriteLine($"Found {pendingCount} pending migrations:");
        foreach (var migration in pendingMigrations)
        {
            Console.WriteLine($"  - {migration}");
        }

        await context.Database.MigrateAsync();
        Console.WriteLine("✓ All migrations applied successfully");
    }

    Console.WriteLine("Migration process completed successfully");
    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL ERROR during migration: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"Stack Trace: {ex.StackTrace}");
    Environment.Exit(1);
}

static async Task WaitForDatabaseAsync(string connectionString, int maxAttempts, int delaySeconds)
{
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await connection.CloseAsync();

            Console.WriteLine($"✓ PostgreSQL is ready (attempt {attempt}/{maxAttempts})");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Attempt {attempt}/{maxAttempts}: PostgreSQL not ready yet ({ex.Message})");

            if (attempt == maxAttempts)
            {
                Console.Error.WriteLine("ERROR: PostgreSQL failed to become ready within timeout");
                throw;
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
    }
}

static string MaskConnectionString(string connectionString)
{
    try
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (builder.ContainsKey("Password"))
        {
            builder["Password"] = "********";
        }
        return builder.ConnectionString;
    }
    catch
    {
        return "***masked***";
    }
}
