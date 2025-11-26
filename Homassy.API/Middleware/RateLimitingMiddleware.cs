using Homassy.API.Extensions;
using Homassy.API.Models.Common;
using Homassy.API.Services;
using Serilog;
using System.Text.Json;

namespace Homassy.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.GetClientIpAddress();
            var endpoint = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var method = context.Request.Method;

            // Global IP-based rate limiting (applies to all endpoints)
            var globalRateLimitKey = $"global:{clientIp}";
            var globalMaxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:GlobalMaxAttempts") ?? "100");
            var globalWindowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:GlobalWindowMinutes") ?? "1");
            var globalWindow = TimeSpan.FromMinutes(globalWindowMinutes);

            if (RateLimitService.IsRateLimited(globalRateLimitKey, globalMaxAttempts, globalWindow))
            {
                var remaining = RateLimitService.GetLockoutRemaining(globalRateLimitKey, globalWindow);
                Log.Warning($"Global rate limit exceeded from IP {clientIp} for endpoint {method} {endpoint}");

                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                
                var errorResponse = ApiResponse.ErrorResponse(
                    $"Too many requests. Please try again in {remaining?.TotalMinutes:F0} minutes.");
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            // Endpoint-specific rate limiting
            var endpointRateLimitKey = $"endpoint:{endpoint}:{clientIp}";
            var endpointMaxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:EndpointMaxAttempts") ?? "30");
            var endpointWindowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:EndpointWindowMinutes") ?? "1");
            var endpointWindow = TimeSpan.FromMinutes(endpointWindowMinutes);

            if (RateLimitService.IsRateLimited(endpointRateLimitKey, endpointMaxAttempts, endpointWindow))
            {
                var remaining = RateLimitService.GetLockoutRemaining(endpointRateLimitKey, endpointWindow);
                Log.Warning($"Endpoint rate limit exceeded from IP {clientIp} for {method} {endpoint}");

                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                
                var errorResponse = ApiResponse.ErrorResponse(
                    $"Too many requests to this endpoint. Please try again in {remaining?.TotalMinutes:F0} minutes.");
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            await _next(context);
        }
    }
}