using Homassy.API.Security;
using System.Net;

namespace Homassy.Tests.Unit;

/// <summary>
/// The iCal URL is supplied by any authenticated user and fetched by the API from inside the
/// Docker network, so anything the guard lets through is somewhere the server can be pointed.
/// </summary>
public class ExternalUrlGuardTests
{
    #region Address Screening

    [Theory]
    [InlineData("127.0.0.1")]              // loopback
    [InlineData("127.1.2.3")]              // the rest of 127/8
    [InlineData("::1")]                    // IPv6 loopback
    [InlineData("0.0.0.0")]                // "this network"
    [InlineData("10.0.0.1")]               // private
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]             // private, the Docker bridge range
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.1")]            // private
    [InlineData("169.254.169.254")]        // cloud instance metadata
    [InlineData("169.254.0.1")]            // link-local
    [InlineData("100.64.0.1")]             // carrier-grade NAT
    [InlineData("224.0.0.1")]              // multicast
    [InlineData("255.255.255.255")]        // broadcast
    [InlineData("::ffff:10.0.0.1")]        // IPv4-mapped private address
    [InlineData("fc00::1")]                // unique local
    [InlineData("fd12:3456::1")]
    [InlineData("fe80::1")]                // link-local
    [InlineData("ff02::1")]                // multicast
    [InlineData("::")]                     // unspecified
    [InlineData("2002:0a00:0001::1")]      // 6to4 wrapping 10.0.0.1
    public void IsBlocked_RejectsNonPublicAddresses(string address)
    {
        Assert.True(ExternalUrlGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.0.1")]             // just below the private block
    [InlineData("172.32.0.1")]             // just above it
    [InlineData("192.167.0.1")]
    [InlineData("100.63.255.255")]         // just below the CGNAT block
    [InlineData("2606:4700:4700::1111")]
    public void IsBlocked_AllowsPublicAddresses(string address)
    {
        Assert.False(ExternalUrlGuard.IsBlocked(IPAddress.Parse(address)));
    }

    #endregion

    #region URL Screening

    [Theory]
    [InlineData("https://calendar.google.com/calendar/ical/x/basic.ics")]
    [InlineData("https://outlook.office365.com/owa/calendar/feed.ics")]
    [InlineData("https://example.com:8443/feed.ics")]
    public void TryValidate_AcceptsPublicHttpsFeeds(string url)
    {
        Assert.True(ExternalUrlGuard.TryValidate(url, allowHttp: false, out var uri, out var error));
        Assert.Null(error);
        Assert.NotNull(uri);
    }

    [Fact]
    public void TryValidate_RewritesWebcalToHttps()
    {
        Assert.True(ExternalUrlGuard.TryValidate("webcal://example.com/feed.ics", allowHttp: false, out var uri, out _));

        Assert.Equal("https", uri!.Scheme);
        Assert.Equal("example.com", uri.Host);
    }

    [Theory]
    [InlineData("http://homassy-kratos:4434/identities")]      // internal admin API, single label
    [InlineData("http://homassy-api:8080/api/v1.0/internal")]  // internal service route
    [InlineData("http://postgres:5432/")]
    [InlineData("http://localhost:8080/feed.ics")]
    [InlineData("https://localhost/feed.ics")]
    [InlineData("https://127.0.0.1/feed.ics")]
    [InlineData("https://10.0.0.5/feed.ics")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/feed.ics")]
    [InlineData("https://192.168.1.1/feed.ics")]
    public void TryValidate_RejectsInternalTargets(string url)
    {
        Assert.False(ExternalUrlGuard.TryValidate(url, allowHttp: true, out _, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/")]
    [InlineData("ftp://example.com/feed.ics")]
    [InlineData("not a url")]
    [InlineData("/relative/feed.ics")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryValidate_RejectsNonHttpUrls(string? url)
    {
        Assert.False(ExternalUrlGuard.TryValidate(url, allowHttp: true, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_RejectsPlainHttpUnlessExplicitlyAllowed()
    {
        Assert.False(ExternalUrlGuard.TryValidate("http://example.com/feed.ics", allowHttp: false, out _, out var error));
        Assert.Equal("URL must use https.", error);

        Assert.True(ExternalUrlGuard.TryValidate("http://example.com/feed.ics", allowHttp: true, out _, out _));
    }

    #endregion

    #region Resolution

    [Fact]
    public async Task ResolveAllowedAddressesAsync_ForAHostThatResolvesToLoopback_ReturnsNothing()
    {
        var addresses = await ExternalUrlGuard.ResolveAllowedAddressesAsync("localhost", CancellationToken.None);

        Assert.Empty(addresses);
    }

    [Fact]
    public async Task ResolveAllowedAddressesAsync_ForAnUnresolvableHost_ReturnsNothing()
    {
        var host = $"{Guid.NewGuid():N}.invalid";

        var addresses = await ExternalUrlGuard.ResolveAllowedAddressesAsync(host, CancellationToken.None);

        Assert.Empty(addresses);
    }

    [Fact]
    public async Task ResolveAllowedAddressesAsync_ForAPublicLiteral_ReturnsIt()
    {
        var addresses = await ExternalUrlGuard.ResolveAllowedAddressesAsync("8.8.8.8", CancellationToken.None);

        Assert.Equal(IPAddress.Parse("8.8.8.8"), Assert.Single(addresses));
    }

    #endregion
}
