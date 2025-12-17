using Homassy.API.Models.RateLimit;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Services
{
    public static class RateLimitService
    {
        private static readonly ConcurrentDictionary<string, RateLimitInfo> _attempts = new();

        public static bool IsRateLimited(string key, int maxAttempts, TimeSpan window)
        {
            var now = DateTime.UtcNow;

            var info = _attempts.AddOrUpdate(
                key,
                _ => new RateLimitInfo { FirstAttempt = now, Attempts = 1 },
                (_, existing) =>
                {
                    if (now - existing.FirstAttempt > window)
                    {
                        return new RateLimitInfo { FirstAttempt = now, Attempts = 1 };
                    }

                    existing.Attempts++;
                    return existing;
                });

            if (info.Attempts > maxAttempts)
            {
                Log.Warning($"Rate limit exceeded for key: {key}");
                return true;
            }

            return false;
        }

        public static RateLimitStatus GetRateLimitStatus(string key, int maxAttempts, TimeSpan window)
        {
            var now = DateTime.UtcNow;
            
            if (_attempts.TryGetValue(key, out var info))
            {
                var elapsed = now - info.FirstAttempt;
                
                if (elapsed > window)
                {
                    return new RateLimitStatus
                    {
                        Limit = maxAttempts,
                        Remaining = maxAttempts,
                        ResetTimestamp = new DateTimeOffset(now.Add(window)).ToUnixTimeSeconds()
                    };
                }

                var remaining = Math.Max(0, maxAttempts - info.Attempts);
                var resetTime = info.FirstAttempt.Add(window);
                var resetTimestamp = new DateTimeOffset(resetTime, TimeSpan.Zero).ToUnixTimeSeconds();

                var status = new RateLimitStatus
                {
                    Limit = maxAttempts,
                    Remaining = remaining,
                    ResetTimestamp = resetTimestamp
                };

                if (remaining == 0)
                {
                    var retryAfter = (int)Math.Ceiling((resetTime - now).TotalSeconds);
                    status.RetryAfterSeconds = Math.Max(1, retryAfter);
                }

                return status;
            }

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
    }
}