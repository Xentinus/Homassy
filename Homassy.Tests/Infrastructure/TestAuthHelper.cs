using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Homassy.Tests.Infrastructure;

/// <summary>
/// Test authentication helper.
/// Creates test users and manages mock Kratos authentication for integration tests.
/// </summary>
public class TestAuthHelper
{
    private readonly HomassyWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string? _currentTestUserId;

    // Mock auth response for compatibility with existing tests
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public TestAuthHelper(HomassyWebApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    /// <summary>
    /// Creates a test user and returns mock authentication credentials.
    /// This creates a user in the database with a mock Kratos identity ID
    /// and sets up the authentication headers for testing.
    /// </summary>
    public async Task<(string email, AuthResponse auth)> CreateAndAuthenticateUserAsync(string testPrefix)
    {
        var email = $"{testPrefix}-{Guid.NewGuid():N}@test.homassy.local";
        var kratosIdentityId = Guid.NewGuid().ToString();
        var displayName = $"Test User {testPrefix}";

        // Create user in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var user = new User
        {
            PublicId = Guid.NewGuid(),
            Email = email,
            Name = displayName,
            KratosIdentityId = kratosIdentityId,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Status = UserStatus.Active
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create profile for the user
        var profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = displayName,
            DefaultCurrency = Currency.Huf,
            DefaultLanguage = Language.Hungarian,
            DefaultTimeZone = UserTimeZone.CentralEuropeStandardTime
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        _currentTestUserId = kratosIdentityId;

        // Register this session with the mock Kratos service
        _factory.RegisterTestSession(kratosIdentityId, email, displayName, user.Id);

        var auth = new AuthResponse
        {
            AccessToken = $"mock-session-{kratosIdentityId}",
            RefreshToken = $"mock-refresh-{kratosIdentityId}"
        };

        return (email, auth);
    }

    /// <summary>
    /// Sets the authentication token header for subsequent requests.
    /// </summary>
    public void SetAuthToken(string token)
    {
        // Clear any existing auth
        _client.DefaultRequestHeaders.Remove("X-Session-Token");
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Authorization = null;

        // Use X-Session-Token header for mock Kratos auth
        _client.DefaultRequestHeaders.Add("X-Session-Token", token);
    }

    /// <summary>
    /// Clears the authentication token.
    /// </summary>
    public void ClearAuthToken()
    {
        ClearAuth();
    }

    /// <summary>
    /// Sets a mock Kratos session token header for testing protected endpoints.
    /// </summary>
    public void SetMockSessionToken(string sessionToken)
    {
        _client.DefaultRequestHeaders.Remove("X-Session-Token");
        _client.DefaultRequestHeaders.Add("X-Session-Token", sessionToken);
    }

    /// <summary>
    /// Sets the session cookie for Kratos authentication.
    /// </summary>
    public void SetMockSessionCookie(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", $"ory_kratos_session={sessionCookie}");
    }

    /// <summary>
    /// Clears all authentication headers.
    /// </summary>
    public void ClearAuth()
    {
        _client.DefaultRequestHeaders.Remove("X-Session-Token");
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Authorization = null;
        _currentTestUserId = null;
    }

    /// <summary>
    /// Cleans up a test user from the database.
    /// </summary>
    public async Task CleanupUserAsync(string email)
    {
        await _factory.CleanupTestUserAsync(email);
    }
}
