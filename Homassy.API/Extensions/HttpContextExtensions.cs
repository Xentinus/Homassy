using System.Net.Sockets;

namespace Homassy.API.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// The address of the caller, as used for rate limiting and security logging.
        /// </summary>
        /// <remarks>
        /// Deliberately does not read <c>X-Forwarded-For</c> or <c>X-Real-IP</c>: any client
        /// can send those, and trusting them lets a caller mint a fresh rate-limit bucket per
        /// request and choose the address that shows up in the logs. Forwarded headers are
        /// unwound by <c>UseForwardedHeaders</c>, which rewrites the remote address only when
        /// the request came from a proxy listed in <c>ForwardedHeaders:KnownNetworks</c> /
        /// <c>KnownProxies</c>; everything else keeps the real connection address.
        /// </remarks>
        public static string GetClientIpAddress(this HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp is null)
            {
                return "unknown";
            }

            // Kestrel reports IPv4 callers as ::ffff:a.b.c.d when listening dual-stack.
            // Normalising keeps one bucket per client instead of one per representation.
            if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6 && remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            return remoteIp.ToString();
        }
    }
}
