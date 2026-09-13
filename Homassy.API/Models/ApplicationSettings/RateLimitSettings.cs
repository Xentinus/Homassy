using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ApplicationSettings;

/// <summary>
/// The <c>RateLimiting</c> configuration section, bound and validated once at startup.
/// </summary>
/// <remarks>
/// <see cref="Middleware.RateLimitingMiddleware"/> used to read these four values out of
/// <see cref="IConfiguration"/> and <c>int.Parse</c> them on every single request. That cost aside,
/// <c>int.Parse</c> throws on a non-numeric value, so one typo in <c>appsettings.json</c> or in an
/// environment variable turned every request into a 500 — the health endpoints included — instead
/// of failing at startup where it would be noticed. Registered with
/// <c>ValidateDataAnnotations().ValidateOnStart()</c>, a bad value now stops the host from
/// starting; the values stay configurable from configuration and environment variables exactly as
/// before.
/// </remarks>
public class RateLimitSettings
{
    /// <summary>Requests one client IP may make across all endpoints in <see cref="GlobalWindowMinutes"/>.</summary>
    [Range(1, int.MaxValue)]
    public int GlobalMaxAttempts { get; set; } = 100;

    [Range(1, MaxWindowMinutes)]
    public int GlobalWindowMinutes { get; set; } = 1;

    /// <summary>Requests one client IP may make against one route template in <see cref="EndpointWindowMinutes"/>.</summary>
    [Range(1, int.MaxValue)]
    public int EndpointMaxAttempts { get; set; } = 30;

    [Range(1, MaxWindowMinutes)]
    public int EndpointWindowMinutes { get; set; } = 1;

    /// <summary>
    /// The longest window that actually works, bounded by the sweeper rather than by the limiter.
    /// </summary>
    /// <remarks>
    /// <see cref="Services.RateLimitCleanupService"/> drops every bucket older than two hours, so
    /// a window configured above that has its counters reset out from under it and the effective
    /// limit silently becomes the sweeper's cadence. Validating it here is what stops a setting
    /// that cannot work from being accepted at startup. Raise both together or not at all.
    /// </remarks>
    public const int MaxWindowMinutes = 120;

    public TimeSpan GlobalWindow => TimeSpan.FromMinutes(GlobalWindowMinutes);

    public TimeSpan EndpointWindow => TimeSpan.FromMinutes(EndpointWindowMinutes);
}
