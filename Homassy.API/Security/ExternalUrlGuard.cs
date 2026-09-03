using System.Net;
using System.Net.Sockets;

namespace Homassy.API.Security;

/// <summary>
/// Decides whether a user-supplied URL may be fetched by the server.
/// </summary>
/// <remarks>
/// An authenticated user can store an iCal feed URL that the API then fetches on a timer from
/// inside the Docker network. Without this guard that is a server-side request forgery
/// primitive: the Kratos admin API (<c>http://homassy-kratos:4434</c>, unauthenticated by
/// design), the internal service-to-service routes, Postgres, and a cloud host's instance
/// metadata endpoint are all reachable, and part of the response comes back to the user
/// through the cached-events payload.
///
/// The check runs twice. Once at validation time, so the user gets a clear error at the moment
/// they save the calendar; and once at connect time, because a hostname that resolved to a
/// public address when it was saved can resolve to an internal one later (DNS rebinding).
/// <see cref="CreateConnectCallback"/> is the one that actually enforces it.
/// </remarks>
public static class ExternalUrlGuard
{
    private const string WebcalScheme = "webcal://";

    /// <summary>
    /// Whether plain http feeds are tolerated. Set once at startup from the hosting
    /// environment; true only in Development, where a local feed may have no TLS. A static
    /// rather than an injected dependency because the guard is also reached from the static
    /// sync path and from model validation, and all three must agree.
    /// </summary>
    public static bool AllowInsecureScheme { get; set; }

    /// <summary>Calendar apps hand out webcal:// links; they are https in all but the scheme.</summary>
    public static string Normalize(string url)
    {
        if (url.StartsWith(WebcalScheme, StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + url[WebcalScheme.Length..];
        }

        return url;
    }

    /// <summary>
    /// Parses and screens a URL without touching the network. <paramref name="allowHttp"/> is
    /// only true in Development, where a local feed may not have TLS.
    /// </summary>
    public static bool TryValidate(string? url, bool allowHttp, out Uri? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL is required.";
            return false;
        }

        if (!Uri.TryCreate(Normalize(url.Trim()), UriKind.Absolute, out var uri))
        {
            error = "URL is not a valid absolute URL.";
            return false;
        }

        var isHttps = uri.Scheme == Uri.UriSchemeHttps;
        var isHttp = uri.Scheme == Uri.UriSchemeHttp;

        if (!isHttps && !(allowHttp && isHttp))
        {
            error = "URL must use https.";
            return false;
        }

        var host = uri.Host;

        if (IPAddress.TryParse(host, out var literal))
        {
            if (IsBlocked(literal))
            {
                error = "URL must point to a public host.";
                return false;
            }
        }
        else if (!host.Contains('.'))
        {
            // "homassy-kratos", "postgres", "localhost" - a single label resolves through the
            // container network's search domain, never to a public calendar feed.
            error = "URL must point to a fully qualified public host.";
            return false;
        }

        normalized = uri;
        return true;
    }

    /// <summary>
    /// True for any address the server must not be pointed at: loopback, link-local (which
    /// covers the 169.254.169.254 cloud metadata endpoint), the private and carrier-grade NAT
    /// ranges, multicast, and the reserved blocks.
    /// </summary>
    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var octets = address.GetAddressBytes();

            return octets[0] switch
            {
                0 => true,                                          // 0.0.0.0/8 "this network"
                10 => true,                                         // 10.0.0.0/8 private
                100 => octets[1] >= 64 && octets[1] <= 127,         // 100.64.0.0/10 carrier-grade NAT
                127 => true,                                        // loopback (already covered)
                169 => octets[1] == 254,                            // 169.254.0.0/16 link-local + metadata
                172 => octets[1] >= 16 && octets[1] <= 31,          // 172.16.0.0/12 private (Docker bridge)
                192 => (octets[1] == 168)                           // 192.168.0.0/16 private
                       || (octets[1] == 0 && octets[2] == 0)        // 192.0.0.0/24 IETF assignments
                       || (octets[1] == 0 && octets[2] == 2),       // 192.0.2.0/24 documentation
                198 => (octets[1] == 18 || octets[1] == 19)         // 198.18.0.0/15 benchmarking
                       || (octets[1] == 51 && octets[2] == 100),    // 198.51.100.0/24 documentation
                203 => octets[1] == 0 && octets[2] == 113,          // 203.0.113.0/24 documentation
                >= 224 => true,                                     // multicast, reserved, broadcast
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal
                || address.IsIPv6Multicast
                || address.IsIPv6Teredo)
            {
                return true;
            }

            // 2002::/16 (6to4) embeds an IPv4 address in bytes 2-5; screen the embedded one.
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 0x20 && bytes[1] == 0x02)
            {
                return IsBlocked(new IPAddress(bytes[2..6]));
            }

            return false;
        }

        // Neither IPv4 nor IPv6: nothing this application should be dialling.
        return true;
    }

    /// <summary>
    /// Resolves <paramref name="host"/> and returns the addresses that are safe to connect to.
    /// Empty means the host is unusable - either it does not resolve, or it resolves to
    /// something off limits.
    /// </summary>
    public static async Task<IReadOnlyList<IPAddress>> ResolveAllowedAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return IsBlocked(literal) ? [] : [literal];
        }

        IPAddress[] resolved;

        try
        {
            resolved = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
        }

        // Every answer has to be allowed, not merely one of them: a host that resolves to both a
        // public and an internal address would otherwise still reach the internal one.
        return resolved.Length > 0 && !resolved.Any(IsBlocked) ? resolved : [];
    }

    /// <summary>
    /// A <see cref="SocketsHttpHandler.ConnectCallback"/> that re-screens the host immediately
    /// before the socket opens. This is where the guarantee actually lives: a validation-time
    /// check can be defeated by re-pointing DNS afterwards, and this cannot.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> CreateConnectCallback()
    {
        return async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;
            var addresses = await ResolveAllowedAddressesAsync(host, cancellationToken);

            if (addresses.Count == 0)
            {
                throw new IOException($"Refusing to connect to '{host}': it does not resolve to a public address.");
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync([.. addresses], context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
    }
}
