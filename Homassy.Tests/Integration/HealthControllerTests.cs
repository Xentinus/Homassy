using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.HealthCheck;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class HealthControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HomassyWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public HealthControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task GetHealth_WithoutAuth_ReturnsResponse()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {responseBody}");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetHealth_ReturnsValidHealthCheckResponse()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        _output.WriteLine($"Status: {content?.Data?.Status}");
        _output.WriteLine($"Duration: {content?.Data?.Duration}");

        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.NotNull(content.Data);
        Assert.NotNull(content.Data.Status);
        Assert.NotNull(content.Data.Duration);
        Assert.NotNull(content.Data.Dependencies);
    }

    [Fact]
    public async Task GetHealth_ReturnsDatabaseDependency()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        var keys = content?.Data?.Dependencies?.Keys.ToArray() ?? [];
        _output.WriteLine($"Dependencies: {string.Join(", ", keys)}");

        Assert.NotNull(content?.Data?.Dependencies);
        Assert.True(content.Data.Dependencies.ContainsKey("database"));
    }

    [Fact]
    public async Task GetHealth_ReturnsOpenFoodFactsDependency()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        var keys = content?.Data?.Dependencies?.Keys.ToArray() ?? [];
        _output.WriteLine($"Dependencies: {string.Join(", ", keys)}");

        Assert.NotNull(content?.Data?.Dependencies);
        Assert.True(content.Data.Dependencies.ContainsKey("openfoodfacts"));
    }

    /// <summary>
    /// The API registers exactly two health checks (Program.cs): "database" and "openfoodfacts".
    /// Email delivery lives in the separate Homassy.Email service and has its own health endpoint,
    /// so the API deliberately reports no "email" dependency.
    /// </summary>
    [Fact]
    public async Task GetHealth_ReportsOnlyTheApisOwnDependencies()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        var keys = content?.Data?.Dependencies?.Keys.ToArray() ?? [];
        _output.WriteLine($"Dependencies: {string.Join(", ", keys)}");

        Assert.NotNull(content?.Data?.Dependencies);
        Assert.Equal(["database", "openfoodfacts"], keys.OrderBy(k => k).ToArray());
        Assert.False(content.Data.Dependencies.ContainsKey("email"));
    }

    [Fact]
    public async Task GetHealth_DependenciesHaveDuration()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        Assert.NotNull(content?.Data?.Dependencies);

        foreach (var dependency in content.Data.Dependencies)
        {
            _output.WriteLine($"{dependency.Key}: {dependency.Value.Status} - {dependency.Value.Duration}");
            Assert.NotNull(dependency.Value.Duration);
            Assert.Contains("ms", dependency.Value.Duration);
        }
    }

    [Fact]
    public async Task GetReadiness_WithoutAuth_ReturnsResponse()
    {
        var response = await _client.GetAsync("/api/v1.0/health/ready");
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {responseBody}");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetReadiness_ReturnsValidHealthCheckResponse()
    {
        var response = await _client.GetAsync("/api/v1.0/health/ready");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        _output.WriteLine($"Status: {content?.Data?.Status}");
        _output.WriteLine($"Duration: {content?.Data?.Duration}");

        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.NotNull(content.Data);
        Assert.NotNull(content.Data.Status);
    }

    [Fact]
    public async Task GetReadiness_ChecksOnlyReadyTaggedDependencies()
    {
        var response = await _client.GetAsync("/api/v1.0/health/ready");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        var keys = content?.Data?.Dependencies?.Keys.ToArray() ?? [];
        _output.WriteLine($"Readiness dependencies: {string.Join(", ", keys)}");

        Assert.NotNull(content?.Data?.Dependencies);
        Assert.True(content.Data.Dependencies.ContainsKey("database"));
        Assert.False(content.Data.Dependencies.ContainsKey("openfoodfacts"));
        Assert.False(content.Data.Dependencies.ContainsKey("email"));
    }

    [Fact]
    public async Task GetLiveness_WithoutAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1.0/health/live");

        _output.WriteLine($"Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLiveness_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/api/v1.0/health/live");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        _output.WriteLine($"Status: {content?.Data?.Status}");

        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.NotNull(content.Data);
        Assert.Equal("Healthy", content.Data.Status);
    }

    [Fact]
    public async Task GetLiveness_ReturnsEmptyDependencies()
    {
        var response = await _client.GetAsync("/api/v1.0/health/live");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        _output.WriteLine($"Dependencies count: {content?.Data?.Dependencies?.Count}");

        Assert.NotNull(content?.Data?.Dependencies);
        Assert.Empty(content.Data.Dependencies);
    }

    [Fact]
    public async Task GetLiveness_AlwaysSucceeds()
    {
        for (var i = 0; i < 3; i++)
        {
            var response = await _client.GetAsync("/api/v1.0/health/live");
            
            _output.WriteLine($"Attempt {i + 1}: {response.StatusCode}");
            
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetHealth_ResponseHasCorrectContentType()
    {
        var response = await _client.GetAsync("/api/v1.0/health");

        var contentType = response.Content.Headers.ContentType?.MediaType;
        _output.WriteLine($"Content-Type: {contentType}");

        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task GetReadiness_ResponseHasCorrectContentType()
    {
        var response = await _client.GetAsync("/api/v1.0/health/ready");

        var contentType = response.Content.Headers.ContentType?.MediaType;
        _output.WriteLine($"Content-Type: {contentType}");

        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task GetLiveness_ResponseHasCorrectContentType()
    {
        var response = await _client.GetAsync("/api/v1.0/health/live");

        var contentType = response.Content.Headers.ContentType?.MediaType;
        _output.WriteLine($"Content-Type: {contentType}");

        Assert.Equal("application/json", contentType);
    }

    #region Public Endpoints Do Not Touch Kratos

    /// <summary>
    /// The Docker healthcheck hits /api/v1.0/health/ready every 30s. It used to make a full
    /// whoami round trip first, because the middleware's skip list held paths
    /// ("/api/v1/health") that the versioned routes never produce — so a slow or unreachable
    /// Kratos made readiness probing slow for a reason unrelated to readiness, and `--wait` on
    /// deploy could time out on it. A session token is sent deliberately: without one the
    /// middleware short-circuits anyway, and the test would pass without proving anything.
    /// </summary>
    [Theory]
    [InlineData("/api/v1.0/health")]
    [InlineData("/api/v1.0/health/ready")]
    [InlineData("/api/v1.0/health/live")]
    [InlineData("/api/version")]
    [InlineData("/api/v1.0/errorcodes")]
    public async Task PublicEndpoints_EvenWithASessionToken_DoNotCallKratos(string path)
    {
        _factory.MockKratos.ResetCallCounts();

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Session-Token", "mock-session-that-does-not-exist");

        var response = await _client.SendAsync(request);

        _output.WriteLine($"{path} -> {response.StatusCode}, Kratos calls: {_factory.MockKratos.GetSessionCallCount}");

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _factory.MockKratos.GetSessionCallCount);
    }

    /// <summary>
    /// The counterpart: an authenticated route must still be validated, so the skip above is
    /// narrow rather than a blanket opt-out.
    /// </summary>
    [Fact]
    public async Task AuthenticatedEndpoint_WithASessionToken_StillCallsKratos()
    {
        _factory.MockKratos.ResetCallCounts();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1.0/auth/me");
        request.Headers.Add("X-Session-Token", "mock-session-that-does-not-exist");

        await _client.SendAsync(request);

        Assert.Equal(1, _factory.MockKratos.GetSessionCallCount);
    }

    #endregion
}
