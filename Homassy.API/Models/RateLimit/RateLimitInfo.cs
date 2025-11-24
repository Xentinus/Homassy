namespace Homassy.API.Models.RateLimit
{
    public class RateLimitInfo
    {
        public required DateTime FirstAttempt { get; set; }
        public int Attempts { get; set; }
    }
}
