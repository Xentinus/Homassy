using Homassy.API.Models.RateLimit;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Services
{
    /// <summary>
    /// Fixed-window attempt counting, per key, in this process's memory.
    /// </summary>
    /// <remarks>
    /// Per-process, so an instance behind a load balancer counts only what reached it; the
    /// Redis-backed limiter in #68 supersedes that. Until then this is the one that runs, so its
    /// count has to be exact: every mutation goes through <see cref="RegisterAttempt"/>, which
    /// swaps a whole immutable <see cref="RateLimitInfo"/> in a compare-and-swap retry loop rather
    /// than mutating one in place.
    /// </remarks>
    public static class RateLimitService
    {
        private static readonly ConcurrentDictionary<string, RateLimitInfo> _attempts = new();

        /// <summary>
        /// Records one attempt against <paramref name="key"/> and returns the resulting status —
        /// including whether the attempt is over the limit.
        /// </summary>
        /// <remarks>
        /// The status comes from the very snapshot this attempt produced, so the caller's headers
        /// describe the request it is answering. Reading the dictionary again afterwards would
        /// describe whatever the bucket looked like by then instead.
        /// </remarks>
        public static RateLimitStatus RegisterAttempt(string key, int maxAttempts, TimeSpan window)
        {
            var now = DateTime.UtcNow;

            while (true)
            {
                if (!_attempts.TryGetValue(key, out var existing))
                {
                    var opened = new RateLimitInfo(now, 1);

                    if (_attempts.TryAdd(key, opened))
                    {
                        return BuildStatus(opened, maxAttempts, window, now);
                    }

                    // Another request opened the bucket first; re-read and count against theirs.
                    continue;
                }

                var updated = now - existing.FirstAttempt > window
                    ? new RateLimitInfo(now, 1)
                    : new RateLimitInfo(existing.FirstAttempt, existing.Attempts + 1);

                // Reference comparison against the snapshot this iteration read: it succeeds only
                // if nothing else replaced the bucket in between, so no increment is ever lost.
                if (_attempts.TryUpdate(key, updated, existing))
                {
                    if (updated.Attempts > maxAttempts)
                    {
                        Log.Warning($"Rate limit exceeded for key: {key}");
                    }

                    return BuildStatus(updated, maxAttempts, window, now);
                }
            }
        }

        /// <summary>
        /// Records one attempt and reports whether it is over the limit. Prefer
        /// <see cref="RegisterAttempt"/> where the headers are wanted too — this is the same call
        /// with the rest of the answer thrown away.
        /// </summary>
        public static bool IsRateLimited(string key, int maxAttempts, TimeSpan window)
        {
            return RegisterAttempt(key, maxAttempts, window).IsLimited;
        }

        /// <summary>
        /// Reads a bucket without counting against it.
        /// </summary>
        public static RateLimitStatus GetRateLimitStatus(string key, int maxAttempts, TimeSpan window)
        {
            var now = DateTime.UtcNow;

            if (_attempts.TryGetValue(key, out var info) && now - info.FirstAttempt <= window)
            {
                return BuildStatus(info, maxAttempts, window, now);
            }

            // No bucket, or one whose window has run out: the next attempt opens a fresh one.
            return new RateLimitStatus
            {
                Limit = maxAttempts,
                Remaining = maxAttempts,
                ResetTimestamp = new DateTimeOffset(now.Add(window)).ToUnixTimeSeconds()
            };
        }

        public static void ResetAttempts(string key)
        {
            _attempts.TryRemove(key, out _);
        }

        public static TimeSpan? GetLockoutRemaining(string key, TimeSpan window)
        {
            if (_attempts.TryGetValue(key, out var info))
            {
                var elapsed = DateTime.UtcNow - info.FirstAttempt;
                var remaining = window - elapsed;
                return remaining > TimeSpan.Zero ? remaining : null;
            }
            return null;
        }

        public static void CleanupExpiredEntries(TimeSpan window)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _attempts
                .Where(kvp => now - kvp.Value.FirstAttempt > window)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _attempts.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Derives the whole status from one snapshot, so the headers cannot contradict each other.
        /// </summary>
        private static RateLimitStatus BuildStatus(RateLimitInfo info, int maxAttempts, TimeSpan window, DateTime now)
        {
            var resetTime = info.FirstAttempt.Add(window);
            var remaining = Math.Max(0, maxAttempts - info.Attempts);

            var status = new RateLimitStatus
            {
                Limit = maxAttempts,
                Remaining = remaining,
                ResetTimestamp = new DateTimeOffset(resetTime, TimeSpan.Zero).ToUnixTimeSeconds(),
                IsLimited = info.Attempts > maxAttempts
            };

            if (remaining == 0)
            {
                status.RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling((resetTime - now).TotalSeconds));
            }

            return status;
        }
    }
}
