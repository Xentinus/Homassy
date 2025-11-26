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