using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.HealthCheck;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class HealthControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public HealthControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
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

    [Fact]
    public async Task GetHealth_ReturnsEmailDependency()
    {
        var response = await _client.GetAsync("/api/v1.0/health");
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<HealthCheckResponse>>();

        var keys = content?.Data?.Dependencies?.Keys.ToArray() ?? [];
        _output.WriteLine($"Dependencies: {string.Join(", ", keys)}");

        Assert.NotNull(content?.Data?.Dependencies);
        Assert.True(content.Data.Dependencies.ContainsKey("email"));
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
}
