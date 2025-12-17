namespace Homassy.API.Models.RateLimit;

public class RateLimitStatus
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public long ResetTimestamp { get; set; }
    public int? RetryAfterSeconds { get; set; }
}
