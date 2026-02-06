using System.Collections.Concurrent;
using Homassy.API.Context;
using Homassy.API.Models.Kratos;
using Homassy.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Homassy.Tests.Infrastructure;

/// <summary>
/// Mock Kratos service for integration tests.
/// Stores test sessions and returns them when queried.
/// </summary>
public class MockKratosService : IKratosService
{
    private readonly ConcurrentDictionary<string, KratosSession> _testSessions = new();

    public void RegisterSession(string sessionToken, KratosSession session)
    {
        _testSessions[sessionToken] = session;
    }

    public void ClearSessions()
    {
        _testSessions.Clear();
    }

    public Task<KratosSession?> GetSessionAsync(string? cookie, string? sessionToken, CancellationToken cancellationToken = default)
    {
        // Try to find session by token first
        if (!string.IsNullOrEmpty(sessionToken) && _testSessions.TryGetValue(sessionToken, out var session))
        {
            return Task.FromResult<KratosSession?>(session);
        }

        // Try to find by looking for mock-session- prefix in the token
        if (!string.IsNullOrEmpty(sessionToken) && sessionToken.StartsWith("mock-session-"))
        {
            var identityId = sessionToken.Replace("mock-session-", "");
            var matchingSession = _testSessions.Values.FirstOrDefault(s => s.Identity.Id == identityId);
            if (matchingSession != null)
            {
                return Task.FromResult<KratosSession?>(matchingSession);
            }
        }

        return Task.FromResult<KratosSession?>(null);
    }

    public Task<KratosIdentity?> GetIdentityAsync(string identityId, CancellationToken cancellationToken = default)
    {
        var session = _testSessions.Values.FirstOrDefault(s => s.Identity.Id == identityId);
        return Task.FromResult(session?.Identity);
    }

    public Task<KratosIdentity?> UpdateIdentityTraitsAsync(string identityId, KratosTraits traits, CancellationToken cancellationToken = default)
    {
        var session = _testSessions.Values.FirstOrDefault(s => s.Identity.Id == identityId);
        if (session != null)
        {
            session.Identity.Traits = traits;
            return Task.FromResult<KratosIdentity?>(session.Identity);
        }
        return Task.FromResult<KratosIdentity?>(null);
    }

    public Task<bool> DeleteIdentitySessionsAsync(string identityId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _testSessions
            .Where(kvp => kvp.Value.Identity.Id == identityId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _testSessions.TryRemove(key, out _);
        }

        return Task.FromResult(keysToRemove.Any());
    }

    public Task<List<KratosIdentity>> GetIdentitiesAsync(int page = 0, int perPage = 100, CancellationToken cancellationToken = default)
    {
        var identities = _testSessions.Values
            .Select(s => s.Identity)
            .Skip(page * perPage)
            .Take(perPage)
            .ToList();
        return Task.FromResult(identities);
    }

    public Task<KratosIdentity?> GetIdentityByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var identity = _testSessions.Values
            .Select(s => s.Identity)
            .FirstOrDefault(i => i.Traits?.Email?.Equals(email, StringComparison.OrdinalIgnoreCase) == true);
        return Task.FromResult(identity);
    }

    public Task<KratosIdentity?> CreateIdentityAsync(KratosTraits traits, bool verifyEmail = true, CancellationToken cancellationToken = default)
    {
        var identity = new KratosIdentity
        {
            Id = Guid.NewGuid().ToString(),
            SchemaId = "default",
            SchemaUrl = "mock://schema",
            State = "active",
            Traits = traits,
            VerifiableAddresses = verifyEmail && !string.IsNullOrEmpty(traits.Email)
                ? new List<KratosVerifiableAddress>
                {
                    new KratosVerifiableAddress
                    {
                        Id = Guid.NewGuid().ToString(),
                        Value = traits.Email,
                        Verified = true,
                        Via = "email",
                        Status = "completed"
                    }
                }
                : new List<KratosVerifiableAddress>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        // Store in a test session for retrieval
        var session = new KratosSession
        {
            Id = Guid.NewGuid().ToString(),
            Active = true,
            Identity = identity,
            AuthenticatedAt = DateTime.UtcNow,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _testSessions[$"mock-created-{identity.Id}"] = session;
        
        return Task.FromResult<KratosIdentity?>(identity);
    }
}

public class HomassyWebApplicationFactory : WebApplicationFactory<Program>
{
    private MockKratosService? _mockKratosService;

    public MockKratosService MockKratos => _mockKratosService ??= new MockKratosService();

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

        builder.ConfigureServices(services =>
        {
            // Replace the real KratosService with our mock
            services.RemoveAll<IKratosService>();
            services.AddSingleton<IKratosService>(sp => MockKratos);
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
            // JwtService.Initialize removed - using Kratos for authentication
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Registers a test session with the mock Kratos service.
    /// </summary>
    public void RegisterTestSession(string kratosIdentityId, string email, string displayName, int userId)
    {
        var session = new KratosSession
        {
            Id = Guid.NewGuid().ToString(),
            Active = true,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            AuthenticatedAt = DateTime.UtcNow,
            IssuedAt = DateTime.UtcNow,
            Identity = new KratosIdentity
            {
                Id = kratosIdentityId,
                SchemaId = "default",
                SchemaUrl = "http://localhost:4433/schemas/default",
                State = "active",
                StateChangedAt = DateTime.UtcNow,
                Traits = new KratosTraits
                {
                    Email = email,
                    Name = displayName,
                    DisplayName = displayName
                },
                VerifiableAddresses = new List<KratosVerifiableAddress>
                {
                    new KratosVerifiableAddress
                    {
                        Id = Guid.NewGuid().ToString(),
                        Value = email,
                        Verified = true,
                        Via = "email",
                        Status = "completed"
                    }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            AuthenticationMethods = new List<KratosAuthenticationMethod>
            {
                new KratosAuthenticationMethod
                {
                    Method = "password",
                    Aal = "aal1",
                    CompletedAt = DateTime.UtcNow
                }
            }
        };

        MockKratos.RegisterSession($"mock-session-{kratosIdentityId}", session);
    }

    // GetVerificationCodeForEmail removed - Kratos handles authentication

    public int? GetUserIdByEmail(string email)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var normalizedEmail = email.ToLowerInvariant().Trim();
        return context.Users.FirstOrDefault(u => u.Email == normalizedEmail)?.Id;
    }

    public (IServiceScope Scope, HomassyDbContext Context) CreateScopedDbContext()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        return (scope, context);
    }

    public async Task CleanupTestUserAsync(string email)
    {
        var (scope, context) = CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = context.Users.FirstOrDefault(u => u.Email == normalizedEmail);

        if (user != null)
        {
            // Clean up session from mock Kratos
            if (!string.IsNullOrEmpty(user.KratosIdentityId))
            {
                await MockKratos.DeleteIdentitySessionsAsync(user.KratosIdentityId);
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

    // GetFailedLoginAttempts and GetLockedOutUntil removed - Kratos handles lockout
}
