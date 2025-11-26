using System.Collections.Concurrent;

namespace Homassy.API.Services
{
    public static class ConfigService
    {
        private static IConfiguration? _configuration;
        private static readonly ConcurrentDictionary<string, string?> _cache = new();

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
            _cache.Clear();
        }

        public static string GetValue(string key)
        {
            if (_cache.TryGetValue(key, out var cachedValue) && cachedValue != null)
            {
                return cachedValue;
            }

            var value = _configuration?[key] ?? throw new InvalidOperationException($"Configuration key '{key}' not found");
            _cache.TryAdd(key, value);
            return value;
        }

        public static string? GetValueOrDefault(string key, string? defaultValue = null)
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue ?? defaultValue;
            }

            var value = _configuration?[key] ?? defaultValue;
            _cache.TryAdd(key, value);
            return value;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        public static void InvalidateCacheKey(string key)
        {
            _cache.TryRemove(key, out _);
        }

        public static IConfiguration Configuration => 
            _configuration ?? throw new InvalidOperationException("ConfigService not initialized");
    }
}