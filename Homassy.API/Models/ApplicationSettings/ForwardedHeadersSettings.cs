namespace Homassy.API.Models.ApplicationSettings;

/// <summary>
/// Trust boundary for the <c>X-Forwarded-*</c> headers. Any client can send those
/// headers, so they are only honoured when the request arrives from one of the
/// proxies listed here; everything else keeps the connection's remote address.
/// </summary>
public class ForwardedHeadersSettings
{
    /// <summary>
    /// Whether forwarded headers are processed at all. False in local development,
    /// where no reverse proxy sits in front of the API.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Trusted proxy networks in CIDR notation (e.g. <c>172.16.0.0/12</c> for the
    /// Docker bridge the Caddy container runs on).
    /// </summary>
    public List<string> KnownNetworks { get; set; } = [];

    /// <summary>
    /// Trusted proxy addresses, for hosts that are not covered by a network entry.
    /// </summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// How many entries may be consumed from the forwarded chain. Must cover every
    /// trusted hop in the deployed topology (client -> Cloudflare -> cloudflared ->
    /// Caddy -> API). Unwinding stops early at the first address that is not a known
    /// proxy, so this is an upper bound rather than the exact hop count.
    /// </summary>
    public int ForwardLimit { get; set; } = 3;
}
