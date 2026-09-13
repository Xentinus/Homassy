namespace Homassy.API.Models.RateLimit
{
    /// <summary>
    /// One bucket's state: when its window opened and how many attempts have landed in it.
    /// </summary>
    /// <remarks>
    /// Immutable on purpose. The counter used to be a mutable property incremented inside a
    /// <c>ConcurrentDictionary.AddOrUpdate</c> delegate, which gives the delegate no exclusivity —
    /// two requests on the same key could both read 7 and both write 8, so the recorded count
    /// drifted below the real one under exactly the load the limiter exists for, and a reader
    /// could observe a half-updated <c>Attempts</c>/<c>FirstAttempt</c> pair. Replacing the whole
    /// instance publishes both fields together, and reference equality is what lets
    /// <c>TryUpdate</c> act as a compare-and-swap: this is a class rather than a record so two
    /// distinct snapshots that happen to hold equal values can never be mistaken for each other.
    /// </remarks>
    public sealed class RateLimitInfo
    {
        public RateLimitInfo(DateTime firstAttempt, int attempts)
        {
            FirstAttempt = firstAttempt;
            Attempts = attempts;
        }

        /// <summary>When the current window opened (UTC).</summary>
        public DateTime FirstAttempt { get; }

        /// <summary>Attempts recorded in the current window, including the one being served.</summary>
        public int Attempts { get; }
    }
}
