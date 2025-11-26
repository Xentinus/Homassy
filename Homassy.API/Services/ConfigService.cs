namespace Homassy.API.Services
{
    public static class ConfigService
    {
        private static IConfiguration? _configuration;

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static string GetValue(string key)
        {
            return _configuration?[key] ?? throw new InvalidOperationException($"Configuration key '{key}' not found");
        }

        public static string? GetValueOrDefault(string key, string? defaultValue = null)
        {
            return _configuration?[key] ?? defaultValue;
        }

        public static IConfiguration Configuration => 
            _configuration ?? throw new InvalidOperationException("ConfigService not initialized");
    }
}