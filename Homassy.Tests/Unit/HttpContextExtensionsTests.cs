using Homassy.API.Extensions;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace Homassy.Tests.Unit;

/// <summary>
/// The client address feeds rate limiting and security logging, so it must come from the
/// connection rather than from a header the caller controls.
/// </summary>
public class HttpContextExtensionsTests
{
    [Fact]
    public void GetClientIpAddress_ReturnsTheConnectionAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("203.0.113.7", context.GetClientIpAddress());
    }

    [Theory]
    [InlineData("X-Forwarded-For", "10.9.9.9")]
    [InlineData("X-Forwarded-For", "198.51.100.4, 10.0.0.1")]
    [InlineData("X-Real-IP", "10.9.9.9")]
    public void GetClientIpAddress_IgnoresForwardingHeaders(string header, string value)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        context.Request.Headers[header] = value;

        Assert.Equal("203.0.113.7", context.GetClientIpAddress());
    }

    [Fact]
    public void GetClientIpAddress_NormalisesIPv4MappedAddresses()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.7");

        Assert.Equal("203.0.113.7", context.GetClientIpAddress());
    }

    [Fact]
    public void GetClientIpAddress_WithoutARemoteAddress_ReturnsUnknown()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;

        Assert.Equal("unknown", context.GetClientIpAddress());
    }
}
