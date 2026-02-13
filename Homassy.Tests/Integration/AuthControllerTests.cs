using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.Kratos;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// Tests for the Kratos-based AuthController endpoints.
/// Note: Full authentication flow requires a running Kratos instance.
/// These tests verify endpoint behavior without valid Kratos sessions.
/// </summary>
public class AuthControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public AuthControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
    }

    #region Unauthenticated Access Tests
    [Fact]
    public async Task GetCurrentUser_WithoutSession_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/auth/me");
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_WithoutSession_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/auth/session");
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncUser_WithoutSession_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/v1.0/auth/sync", null);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Public Endpoint Tests
    [Fact]
    public async Task GetKratosConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/auth/config");
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<KratosConfig>>();
        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.NotNull(content.Data);
    }

    [Fact]
    public async Task GetKratosConfig_ReturnsValidUrls()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/auth/config");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<KratosConfig>>();

        // Assert
        Assert.NotNull(content?.Data);
        Assert.NotEmpty(content.Data.PublicUrl);
        Assert.Equal("/self-service/login/browser", content.Data.LoginUrl);
        Assert.Equal("/self-service/registration/browser", content.Data.RegistrationUrl);
        Assert.Equal("/self-service/logout/browser", content.Data.LogoutUrl);
        Assert.Equal("/self-service/settings/browser", content.Data.SettingsUrl);
        Assert.Equal("/self-service/recovery/browser", content.Data.RecoveryUrl);
        Assert.Equal("/self-service/verification/browser", content.Data.VerificationUrl);

        _output.WriteLine($"Kratos Public URL: {content.Data.PublicUrl}");
    }
    #endregion

    #region Invalid Session Tests
    [Fact]
    public async Task GetCurrentUser_WithInvalidSessionCookie_ReturnsUnauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1.0/auth/me");
        request.Headers.Add("Cookie", "ory_kratos_session=invalid-session-token");

        // Act
        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidSessionHeader_ReturnsUnauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1.0/auth/me");
        request.Headers.Add("X-Session-Token", "invalid-session-token");

        // Act
        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Body: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Old Endpoint Removal Tests
    [Fact]
    public async Task RequestCode_EndpointRemoved_ReturnsNotFound()
    {
        // This endpoint was removed in favor of Kratos
        var response = await _client.PostAsync("/api/v1.0/auth/request-code", null);
        
        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Should return 404 since endpoint no longer exists
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_EndpointRemoved_ReturnsNotFound()
    {
        // This endpoint was removed in favor of Kratos
        var response = await _client.PostAsync("/api/v1.0/auth/verify-code", null);
        
        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Should return 404 since endpoint no longer exists
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_EndpointRemoved_ReturnsNotFound()
    {
        // This endpoint was removed in favor of Kratos
        var response = await _client.PostAsync("/api/v1.0/auth/refresh", null);
        
        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Should return 404 since endpoint no longer exists
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_EndpointRemoved_ReturnsNotFound()
    {
        // This endpoint was removed in favor of Kratos
        var response = await _client.PostAsync("/api/v1.0/auth/register", null);
        
        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Should return 404 since endpoint no longer exists
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndpointRemoved_ReturnsNotFound()
    {
        // This endpoint was removed in favor of Kratos
        var response = await _client.PostAsync("/api/v1.0/auth/logout", null);
        
        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Should return 404 since endpoint no longer exists
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    #endregion
}
