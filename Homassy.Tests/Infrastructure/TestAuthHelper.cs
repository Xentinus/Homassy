using System.Net.Http.Headers;
using System.Net.Http.Json;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;

namespace Homassy.Tests.Infrastructure;
public class TestAuthHelper
{
    private readonly HomassyWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const int CacheRefreshDelayMs = 6000;

    public TestAuthHelper(HomassyWebApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    public async Task<(string Email, AuthResponse Auth)> CreateAndAuthenticateUserAsync(string? nameSuffix = null)
    {
        var uniqueEmail = $"test-{nameSuffix ?? Guid.NewGuid().ToString()[..8]}@example.com";
        var userName = $"Test User {nameSuffix ?? ""}".Trim();

        // Clean up any existing user with this email to ensure fresh registration
        await _factory.CleanupTestUserAsync(uniqueEmail);

        // Register
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = userName
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);

        // Check if registration succeeded
        if (!registerResponse.IsSuccessStatusCode)
        {
            var errorContent = await registerResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Registration failed for {uniqueEmail}. Status: {registerResponse.StatusCode}, Response: {errorContent}");
        }

        // Get verification code from DB with retry to handle timing issues
        // The registration API may return before the database transaction is fully visible
        string? verificationCode = null;
        const int maxRetries = 15;
        const int retryDelayMs = 300;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            verificationCode = _factory.GetVerificationCodeForEmail(uniqueEmail);
            if (!string.IsNullOrEmpty(verificationCode))
            {
                break;
            }
            
            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelayMs);
            }
        }
        
        if (string.IsNullOrEmpty(verificationCode))
        {
            throw new InvalidOperationException($"Failed to get verification code for {uniqueEmail}");
        }

        // Verify and get tokens
        var verifyRequest = new VerifyLoginRequest
        {
            Email = uniqueEmail,
            VerificationCode = verificationCode
        };
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
        var authContent = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        if (authContent?.Data == null)
        {
            throw new InvalidOperationException($"Failed to authenticate user {uniqueEmail}");
        }

        // Wait for cache to refresh (CacheManagementService refreshes every 5 seconds)
        await Task.Delay(CacheRefreshDelayMs);

        return (uniqueEmail, authContent.Data);
    }

    public void SetAuthToken(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public void ClearAuthToken()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    public async Task CleanupUserAsync(string email)
    {
        await _factory.CleanupTestUserAsync(email);
    }
}
