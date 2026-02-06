using Homassy.API.Context;
using Homassy.Migrator.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data.Common;

// Parse command line arguments
var command = args.Length > 0 ? args[0] : "migrate";
var isDryRun = args.Contains("--dry-run");

Console.WriteLine("=== Homassy Database Migrator ===");
Console.WriteLine($"Command: {command}");

try
{
    // Load configuration
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

    // Create DbContext
    var optionsBuilder = new DbContextOptionsBuilder<HomassyDbContext>();
    optionsBuilder.UseNpgsql(connectionString);
    await using var context = new HomassyDbContext(optionsBuilder.Options);

    // Execute command
    switch (command.ToLower())
    {
        case "migrate":
            await RunEfMigrationsAsync(context);
            break;

        case "migrate-to-kratos":
            await RunEfMigrationsAsync(context); // Ensure DB is up to date first
            await RunKratosMigrationAsync(context, configuration, isDryRun);
            break;

        case "verify-kratos":
            await VerifyKratosMigrationAsync(context, configuration);
            break;

        case "kratos-stats":
            await ShowKratosStatsAsync(context, configuration);
            break;

        default:
            Console.WriteLine($"Unknown command: {command}");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  migrate              - Run EF Core database migrations (default)");
            Console.WriteLine("  migrate-to-kratos    - Migrate users to Kratos identity system");
            Console.WriteLine("                         Options: --dry-run");
            Console.WriteLine("  verify-kratos        - Verify Kratos migration status");
            Console.WriteLine("  kratos-stats         - Show Kratos migration statistics");
            Environment.Exit(1);
            break;
    }

    Console.WriteLine("\nOperation completed successfully");
    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"Stack Trace: {ex.StackTrace}");
    Environment.Exit(1);
}

static async Task RunEfMigrationsAsync(HomassyDbContext context)
{
    Console.WriteLine("\n--- Running EF Core Migrations ---");

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
}

static async Task RunKratosMigrationAsync(HomassyDbContext context, IConfiguration configuration, bool dryRun)
{
    Console.WriteLine("\n--- Running Kratos User Migration ---");

    if (dryRun)
    {
        Console.WriteLine("⚠️  DRY RUN MODE - No changes will be made");
    }

    var migration = new KratosUserMigration(context, configuration);
    var summary = await migration.MigrateAllUsersAsync(dryRun);

    if (summary.FailedCount > 0)
    {
        Console.WriteLine("\n⚠️  Some users failed to migrate. Check the output above for details.");
        Environment.Exit(2); // Partial success
    }
}

static async Task VerifyKratosMigrationAsync(HomassyDbContext context, IConfiguration configuration)
{
    Console.WriteLine("\n--- Verifying Kratos Migration ---");

    var migration = new KratosUserMigration(context, configuration);
    var allMigrated = await migration.VerifyMigrationAsync();

    if (!allMigrated)
    {
        Environment.Exit(3); // Verification failed
    }
}

static async Task ShowKratosStatsAsync(HomassyDbContext context, IConfiguration configuration)
{
    Console.WriteLine("\n--- Kratos Migration Statistics ---");

    var migration = new KratosUserMigration(context, configuration);
    var (total, migrated, pending) = await migration.GetMigrationStatsAsync();

    Console.WriteLine($"Total Users:      {total}");
    Console.WriteLine($"Migrated Users:   {migrated}");
    Console.WriteLine($"Pending Migration: {pending}");

    var percentage = total > 0 ? (migrated * 100.0 / total) : 100;
    Console.WriteLine($"Progress:         {percentage:F1}%");
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
