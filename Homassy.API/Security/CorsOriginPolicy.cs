namespace Homassy.API.Security;

/// <summary>
/// Decides whether an <c>Origin</c> header value is on the configured allowlist.
/// </summary>
/// <remarks>
/// The header is caller-controlled and need not be a URL at all, so every value is parsed
/// defensively: a malformed <c>Origin</c> has to come out as a plain CORS denial rather than
/// an exception thrown inside CORS evaluation, which would surface as a 500 because it is
/// raised before <c>GlobalExceptionMiddleware</c> in the pipeline.
/// </remarks>
public static class CorsOriginPolicy
{
    /// <summary>
    /// Parses the configured origins, dropping any entry that is not an absolute URL.
    /// </summary>
    public static IReadOnlyList<Uri> ParseAllowedOrigins(IEnumerable<string> configuredOrigins)
    {
        var parsed = new List<Uri>();

        foreach (var origin in configuredOrigins)
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                parsed.Add(uri);
            }
        }

        return parsed;
    }

    /// <summary>
    /// True when <paramref name="origin"/> matches one of <paramref name="allowedOrigins"/>.
    /// Scheme and host compare case-insensitively (they are already normalised by
    /// <see cref="Uri"/>), the port compares exactly, and the path is ignored — so
    /// <c>https://app.example.com</c> never matches <c>https://app.example.com.evil.tld</c>.
    /// </summary>
    public static bool IsAllowed(string? origin, IReadOnlyList<Uri> allowedOrigins)
    {
        if (!TryParseOrigin(origin, out var candidate))
        {
            return false;
        }

        foreach (var allowed in allowedOrigins)
        {
            if (string.Equals(allowed.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(allowed.Host, candidate.Host, StringComparison.OrdinalIgnoreCase)
                && allowed.Port == candidate.Port)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True for an origin served from the local machine (any loopback address, any port).
    /// Only ever consulted in Development, where enumerating every dev-server port by hand
    /// would be unworkable.
    /// </summary>
    public static bool IsLoopback(string? origin)
    {
        return TryParseOrigin(origin, out var candidate) && candidate.IsLoopback;
    }

    private static bool TryParseOrigin(string? origin, out Uri parsed)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            parsed = null!;
            return false;
        }

        // Uri.TryCreate accepts things that are not origins at all (e.g. "file:///etc/passwd",
        // "mailto:a@b"); requiring http/https keeps the comparison meaningful.
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            parsed = null!;
            return false;
        }

        parsed = uri;
        return true;
    }
}
