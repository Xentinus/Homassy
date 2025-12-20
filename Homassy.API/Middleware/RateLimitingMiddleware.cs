using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Models.Common;
using Homassy.API.Models.RateLimit;
using Homassy.API.Services;
using Serilog;
using System.Text.Json;

namespace Homassy.API.Middleware
{
    public class RateLimitingMiddleware
    {
        public const string RateLimitLimitHeader = "X-RateLimit-Limit";
        public const string RateLimitRemainingHeader = "X-RateLimit-Remaining";
        public const string RateLimitResetHeader = "X-RateLimit-Reset";
        public const string RetryAfterHeader = "Retry-After";

        private readonly RequestDelegate _next;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.GetClientIpAddress();
            var endpoint = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var method = context.Request.Method;

            var globalMaxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:GlobalMaxAttempts") ?? "100");
            var globalWindowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:GlobalWindowMinutes") ?? "1");
            var globalWindow = TimeSpan.FromMinutes(globalWindowMinutes);
            var globalRateLimitKey = $"global:{clientIp}";

            if (RateLimitService.IsRateLimited(globalRateLimitKey, globalMaxAttempts, globalWindow))
            {
                var status = RateLimitService.GetRateLimitStatus(globalRateLimitKey, globalMaxAttempts, globalWindow);
                Log.Warning($"Global rate limit exceeded from IP {clientIp} for endpoint {method} {endpoint}");

                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                AddRateLimitHeaders(context, status);
                
                var errorResponse = ApiResponse.ErrorResponse(ErrorCodes.RateLimitExceeded);
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonOptions));
                return;
            }

            var endpointMaxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:EndpointMaxAttempts") ?? "30");
            var endpointWindowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:EndpointWindowMinutes") ?? "1");
            var endpointWindow = TimeSpan.FromMinutes(endpointWindowMinutes);
            var endpointRateLimitKey = $"endpoint:{endpoint}:{clientIp}";

            if (RateLimitService.IsRateLimited(endpointRateLimitKey, endpointMaxAttempts, endpointWindow))
            {
                var status = RateLimitService.GetRateLimitStatus(endpointRateLimitKey, endpointMaxAttempts, endpointWindow);
                Log.Warning($"Endpoint rate limit exceeded from IP {clientIp} for {method} {endpoint}");

                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                AddRateLimitHeaders(context, status);
                
                var errorResponse = ApiResponse.ErrorResponse(ErrorCodes.RateLimitExceeded);
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonOptions));
                return;
            }

            var globalStatus = RateLimitService.GetRateLimitStatus(globalRateLimitKey, globalMaxAttempts, globalWindow);
            var endpointStatus = RateLimitService.GetRateLimitStatus(endpointRateLimitKey, endpointMaxAttempts, endpointWindow);

            var mostRestrictiveStatus = globalStatus.Remaining <= endpointStatus.Remaining 
                ? globalStatus 
                : endpointStatus;

            context.Response.OnStarting(() =>
            {
                AddRateLimitHeaders(context, mostRestrictiveStatus);
                return Task.CompletedTask;
            });

            await _next(context);
        }

        private static void AddRateLimitHeaders(HttpContext context, RateLimitStatus status)
        {
            context.Response.Headers[RateLimitLimitHeader] = status.Limit.ToString();
            context.Response.Headers[RateLimitRemainingHeader] = status.Remaining.ToString();
            context.Response.Headers[RateLimitResetHeader] = status.ResetTimestamp.ToString();

            if (status.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers[RetryAfterHeader] = status.RetryAfterSeconds.Value.ToString();
            }
        }
    }
}