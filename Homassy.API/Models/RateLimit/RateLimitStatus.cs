namespace Homassy.API.Models.RateLimit;

/// <summary>
/// What the limiter knows about one bucket at one instant. Always built from a single
/// <see cref="RateLimitInfo"/> snapshot, so <see cref="IsLimited"/>, <see cref="Remaining"/>,
/// <see cref="ResetTimestamp"/> and <see cref="RetryAfterSeconds"/> are consistent with each
/// other and with the attempt that produced them.
/// </summary>
public class RateLimitStatus
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public long ResetTimestamp { get; set; }
    public int? RetryAfterSeconds { get; set; }

    /// <summary>True when the attempt this status came from went over <see cref="Limit"/>.</summary>
    public bool IsLimited { get; set; }
}
