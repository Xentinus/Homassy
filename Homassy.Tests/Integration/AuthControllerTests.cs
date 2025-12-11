using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class AuthControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public AuthControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    #region Request Verification Code Tests
    [Fact]
    public async Task RequestVerificationCode_ValidEmail_ReturnsSuccess()
    {
        // Arrange
        var request = new LoginRequest { Email = "nonexistent-test@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/request-code", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Log the response
        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.Contains("verification code", content.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestVerificationCode_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest { Email = "invalid-email-without-at" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/request-code", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestVerificationCode_EmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest { Email = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/request-code", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestVerificationCode_MissingEmailField_ReturnsBadRequest()
    {
        // Arrange - Send empty JSON
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1.0/auth/request-code", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    #endregion

    #region Verify Code Tests
    [Fact]
    public async Task VerifyCode_InvalidCode_ReturnsUnauthorized()
    {
        // Arrange
        var verifyRequest = new VerifyLoginRequest
        {
            Email = "test@example.com",
            VerificationCode = "00000000"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_EmptyCode_ReturnsBadRequest()
    {
        // Arrange
        var verifyRequest = new VerifyLoginRequest
        {
            Email = "test@example.com",
            VerificationCode = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_InvalidEmailFormat_ReturnsBadRequest()
    {
        // Arrange
        var verifyRequest = new VerifyLoginRequest
        {
            Email = "not-an-email",
            VerificationCode = "12345678"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    #endregion

    #region Protected Endpoints Without Token
    [Fact]
    public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/auth/me");

        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/v1.0/auth/logout", null);

        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            AccessToken = "invalid-access-token",
            RefreshToken = "invalid-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/refresh", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Register Tests
    [Fact]
    public async Task Register_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var uniqueEmail = $"test-register-{Guid.NewGuid()}@example.com";
        var request = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Test User"
        };

        try
        {
            // Act
            var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status Code: {response.StatusCode}");
            _output.WriteLine($"Response Body: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
            Assert.NotNull(content);
            Assert.True(content.Success);
        }
        finally
        {
            // Cleanup
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "not-a-valid-email",
            Name = "Test User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingEmail_ReturnsBadRequest()
    {
        // Arrange - Send request without required Email
        var content = new StringContent("{\"name\":\"Test User\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1.0/auth/register", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingName_ReturnsBadRequest()
    {
        // Arrange - Send request without required Name
        var content = new StringContent("{\"email\":\"test@example.com\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1.0/auth/register", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_NameTooShort_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            Name = "A" // Too short, minimum is 2
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmptyRequest_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1.0/auth/register", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    #endregion

    #region Full Authentication Flow
    [Fact]
    public async Task FullAuthenticationFlow_WithValidCredentials_Succeeds()
    {
        // Arrange - Create a unique test user
        var uniqueEmail = $"test-auth-flow-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Auth Flow Test User"
        };

        try
        {
            // Step 1: Register user
            _output.WriteLine("=== Step 1: Register ===");
            var registerResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);
            var registerBody = await registerResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Register Status: {registerResponse.StatusCode}");
            _output.WriteLine($"Register Response: {registerBody}");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Step 2: Get verification code from database
            _output.WriteLine("\n=== Step 2: Get Verification Code from DB ===");
            var verificationCode = _factory.GetVerificationCodeForEmail(uniqueEmail);
            _output.WriteLine($"Verification Code: {verificationCode}");
            Assert.NotNull(verificationCode);

            // Step 3: Verify code and get tokens
            _output.WriteLine("\n=== Step 3: Verify Code ===");
            var verifyRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = verificationCode
            };
            var verifyResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
            var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Verify Status: {verifyResponse.StatusCode}");
            _output.WriteLine($"Verify Response: {verifyBody}");
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

            var authContent = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
            Assert.NotNull(authContent);
            Assert.True(authContent.Success);
            Assert.NotNull(authContent.Data);
            Assert.NotNull(authContent.Data.AccessToken);
            Assert.NotNull(authContent.Data.RefreshToken);

            _output.WriteLine($"Access Token: {authContent.Data.AccessToken[..50]}...");
            _output.WriteLine($"Refresh Token: {authContent.Data.RefreshToken[..20]}...");

            // Step 4: Use access token to get current user
            _output.WriteLine("\n=== Step 4: Get Current User ===");
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authContent.Data.AccessToken);

            var meResponse = await _client.GetAsync("/api/v1.0/auth/me");
            var meBody = await meResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Me Status: {meResponse.StatusCode}");
            _output.WriteLine($"Me Response: {meBody}");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

            var userContent = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserInfo>>();
            Assert.NotNull(userContent);
            Assert.True(userContent.Success);
            Assert.NotNull(userContent.Data);
            Assert.Equal("Auth Flow Test User", userContent.Data.Name);

            // Step 5: Logout
            _output.WriteLine("\n=== Step 5: Logout ===");
            var logoutResponse = await _client.PostAsync("/api/v1.0/auth/logout", null);
            var logoutBody = await logoutResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Logout Status: {logoutResponse.StatusCode}");
            _output.WriteLine($"Logout Response: {logoutBody}");
            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

            _output.WriteLine("\n=== Full Authentication Flow Completed Successfully! ===");
        }
        finally
        {
            // Cleanup
            _client.DefaultRequestHeaders.Authorization = null;
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task RefreshToken_WithValidTokens_ReturnsNewTokens()
    {
        // Arrange
        var uniqueEmail = $"test-refresh-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Refresh Token Test User"
        };

        try
        {
            // Register and authenticate
            await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);
            var verificationCode = _factory.GetVerificationCodeForEmail(uniqueEmail);

            var verifyRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = verificationCode!
            };
            var verifyResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
            var authContent = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

            Assert.NotNull(authContent?.Data);

            // Set auth header
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authContent.Data.AccessToken);

            // Act - Refresh token
            var refreshRequest = new RefreshTokenRequest
            {
                AccessToken = authContent.Data.AccessToken,
                RefreshToken = authContent.Data.RefreshToken
            };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/refresh", refreshRequest);
            var refreshBody = await refreshResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Refresh Status: {refreshResponse.StatusCode}");
            _output.WriteLine($"Refresh Response: {refreshBody}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

            var refreshContent = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<RefreshTokenResponse>>();
            Assert.NotNull(refreshContent);
            Assert.True(refreshContent.Success);
            Assert.NotNull(refreshContent.Data?.AccessToken);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }
    #endregion
}
