using System.Net;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// The test host runs in the "Testing" environment, so the loopback shortcut in the CORS
/// policy is off and the allowlist is the only thing that can grant an origin — the same
/// shape as Production.
/// </summary>
public class CorsPolicyTests : IClassFixture<HomassyWebApplicationFactory>
{
    private const string AllowedOrigin = "http://localhost:3000";

    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public CorsPolicyTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task Preflight_FromAnAllowlistedOrigin_IsGranted()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1.0/health");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://localhost:3000.evil.tld")]
    [InlineData("http://localhost:4200")]      // loopback, but not on the allowlist
    [InlineData("https://localhost:3000")]     // scheme differs from the allowlisted origin
    public async Task Preflight_FromAnUnlistedOrigin_IsNotGranted(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1.0/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        _output.WriteLine($"Origin: {origin} -> {response.StatusCode}");
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Request_FromAnUnlistedOrigin_IsNotGrantedAccessControlHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1.0/health");
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("garbage")]
    [InlineData("http://")]
    [InlineData("://missing-scheme")]
    public async Task Request_WithAMalformedOrigin_IsDeniedRatherThanFailing(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1.0/health");
        request.Headers.TryAddWithoutValidation("Origin", origin);

        var response = await _client.SendAsync(request);

        _output.WriteLine($"Origin: {origin} -> {response.StatusCode}");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Request_WithoutAnOrigin_IsUnaffected()
    {
        var response = await _client.GetAsync("/api/v1.0/health");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
