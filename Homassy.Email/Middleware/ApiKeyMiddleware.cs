using System.Security.Cryptography;
using System.Text;

namespace Homassy.Email.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Health endpoints are exempt from API key validation
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var configuredKey = _configuration["Email:ApiKey"];

        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogError("Email:ApiKey is not configured");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var receivedKeyHeader))
        {
            _logger.LogWarning("Request to {Path} missing X-Api-Key header", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var receivedKey = receivedKeyHeader.ToString();
        var configuredKeyBytes = Encoding.UTF8.GetBytes(configuredKey);
        var receivedKeyBytes = Encoding.UTF8.GetBytes(receivedKey);

        // Pad to same length to avoid timing side channels
        var maxLen = Math.Max(configuredKeyBytes.Length, receivedKeyBytes.Length);
        var paddedConfigured = new byte[maxLen];
        var paddedReceived = new byte[maxLen];
        configuredKeyBytes.CopyTo(paddedConfigured, 0);
        receivedKeyBytes.CopyTo(paddedReceived, 0);

        if (!CryptographicOperations.FixedTimeEquals(paddedConfigured, paddedReceived))
        {
            _logger.LogWarning("Unauthorized request to {Path} – invalid API key", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
