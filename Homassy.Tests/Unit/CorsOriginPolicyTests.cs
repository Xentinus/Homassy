using Homassy.API.Security;

namespace Homassy.Tests.Unit;

/// <summary>
/// The CORS predicate runs on a caller-controlled header before GlobalExceptionMiddleware,
/// so a bad value has to be a denial rather than an exception, and a near-miss on the
/// allowlist has to be a denial rather than a match.
/// </summary>
public class CorsOriginPolicyTests
{
    private static readonly IReadOnlyList<Uri> Allowed = CorsOriginPolicy.ParseAllowedOrigins(
    [
        "https://app.example.com",
        "http://localhost:3000"
    ]);

    [Fact]
    public void IsAllowed_ExactMatch_IsAllowed()
    {
        Assert.True(CorsOriginPolicy.IsAllowed("https://app.example.com", Allowed));
    }

    [Fact]
    public void IsAllowed_SchemeAndHostCompareCaseInsensitively()
    {
        Assert.True(CorsOriginPolicy.IsAllowed("HTTPS://APP.EXAMPLE.COM", Allowed));
    }

    [Fact]
    public void IsAllowed_ImplicitDefaultPort_MatchesTheExplicitOne()
    {
        Assert.True(CorsOriginPolicy.IsAllowed("https://app.example.com:443", Allowed));
    }

    [Theory]
    [InlineData("https://app.example.com.evil.tld")]   // suffix, not the same host
    [InlineData("https://evil.app.example.com")]       // subdomain of the allowed host
    [InlineData("http://app.example.com")]             // scheme differs
    [InlineData("https://app.example.com:8443")]       // port differs
    [InlineData("https://app-example.com")]
    public void IsAllowed_NearMisses_AreRejected(string origin)
    {
        Assert.False(CorsOriginPolicy.IsAllowed(origin, Allowed));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("http://")]
    public void IsAllowed_MalformedOrigin_IsRejectedWithoutThrowing(string? origin)
    {
        Assert.False(CorsOriginPolicy.IsAllowed(origin, Allowed));
    }

    [Fact]
    public void IsAllowed_WithAnEmptyAllowlist_RejectsEverything()
    {
        Assert.False(CorsOriginPolicy.IsAllowed("https://app.example.com", []));
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://localhost:44300")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:3000")]
    public void IsLoopback_RecognisesLocalOrigins(string origin)
    {
        Assert.True(CorsOriginPolicy.IsLoopback(origin));
    }

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost.evil.tld")]
    [InlineData("garbage")]
    [InlineData(null)]
    public void IsLoopback_RejectsEverythingElse(string? origin)
    {
        Assert.False(CorsOriginPolicy.IsLoopback(origin));
    }

    [Fact]
    public void ParseAllowedOrigins_DropsUnparseableEntries()
    {
        var parsed = CorsOriginPolicy.ParseAllowedOrigins(["https://app.example.com", "not a url", ""]);

        Assert.Single(parsed);
        Assert.Equal("https://app.example.com/", parsed[0].ToString());
    }
}
