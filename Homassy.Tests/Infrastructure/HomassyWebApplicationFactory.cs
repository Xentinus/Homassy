using Homassy.API.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Homassy.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration testing with real database.
/// Provides methods to access verification codes directly from DB for authentication tests.
/// </summary>
public class HomassyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Get the test project directory to find appsettings.Testing.json
        var projectDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources
            config.Sources.Clear();
            
            // Add the API project's appsettings.json first as base
            config.AddJsonFile(Path.Combine(projectDir, "..", "Homassy.API", "appsettings.json"), optional: true);
            
            // Add test-specific configuration (overrides)
            if (File.Exists(configPath))
            {
                config.AddJsonFile(configPath, optional: false);
            }
            
            config.AddEnvironmentVariables();
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure static services are initialized before host creation
        builder.ConfigureServices((context, services) =>
        {
            // Re-initialize static services with test configuration
            var configuration = context.Configuration;
            Homassy.API.Context.HomassyDbContext.SetConfiguration(configuration);
            Homassy.API.Services.ConfigService.Initialize(configuration);
            Homassy.API.Services.EmailService.Initialize(configuration);
            Homassy.API.Services.JwtService.Initialize(configuration);
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Gets the verification code for a user by email directly from the database.
    /// Use this to complete authentication flow in tests without email.
    /// </summary>
    public string? GetVerificationCodeForEmail(string email)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = context.Users.FirstOrDefault(u => u.Email == normalizedEmail);

        if (user == null) return null;

        var auth = context.UserAuthentications.FirstOrDefault(a => a.UserId == user.Id);
        return auth?.VerificationCode;
    }

    /// <summary>
    /// Gets a user's internal ID by email for test setup/cleanup.
    /// </summary>
    public int? GetUserIdByEmail(string email)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var normalizedEmail = email.ToLowerInvariant().Trim();
        return context.Users.FirstOrDefault(u => u.Email == normalizedEmail)?.Id;
    }

    /// <summary>
    /// Creates a scoped DbContext for test operations.
    /// Remember to dispose the scope after use.
    /// </summary>
    public (IServiceScope Scope, HomassyDbContext Context) CreateScopedDbContext()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        return (scope, context);
    }

    /// <summary>
    /// Cleans up a test user by email. Use in test cleanup.
    /// </summary>
    public async Task CleanupTestUserAsync(string email)
    {
        var (scope, context) = CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = context.Users.FirstOrDefault(u => u.Email == normalizedEmail);

        if (user != null)
        {
            // Explicitly remove related records first to ensure complete cleanup
            var auth = context.UserAuthentications.FirstOrDefault(a => a.UserId == user.Id);
            if (auth != null)
            {
                context.UserAuthentications.Remove(auth);
            }

            var profile = context.UserProfiles.FirstOrDefault(p => p.UserId == user.Id);
            if (profile != null)
            {
                context.UserProfiles.Remove(profile);
            }

            var notificationPrefs = context.UserNotificationPreferences.FirstOrDefault(n => n.UserId == user.Id);
            if (notificationPrefs != null)
            {
                context.UserNotificationPreferences.Remove(notificationPrefs);
            }

            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }
    }
}
