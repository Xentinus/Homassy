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

        /// <summary>Shared bucket for requests that matched no route (404s, probes, scans).</summary>
        public const string UnmatchedEndpointKey = "unmatched";

        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <remarks>
        /// The limits come from the injected configuration rather than the process-wide
        /// <see cref="ConfigService"/> on purpose. The limiter is the one component the whole test
        /// suite runs through, and reading a mutable static at request time let a unit test that
        /// installs deliberately tiny limits bleed into every integration test running in parallel —
        /// which is exactly how a 401 assertion started seeing 429. Constructor injection makes that
        /// impossible: <c>UseMiddleware</c> resolves this from the app's own container.
        /// </remarks>
        public RateLimitingMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.GetClientIpAddress();
            var endpoint = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var endpointKey = GetEndpointKey(context);
            var method = context.Request.Method;

            var globalMaxAttempts = int.Parse(_configuration["RateLimiting:GlobalMaxAttempts"] ?? "100");
            var globalWindowMinutes = int.Parse(_configuration["RateLimiting:GlobalWindowMinutes"] ?? "1");
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

            var endpointMaxAttempts = int.Parse(_configuration["RateLimiting:EndpointMaxAttempts"] ?? "30");
            var endpointWindowMinutes = int.Parse(_configuration["RateLimiting:EndpointWindowMinutes"] ?? "1");
            var endpointWindow = TimeSpan.FromMinutes(endpointWindowMinutes);
            var endpointRateLimitKey = $"endpoint:{endpointKey}:{clientIp}";

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

        /// <summary>
        /// The endpoint part of the rate-limit key. Uses the matched route template rather
        /// than the request path, because <see cref="RateLimitService"/> holds its buckets in
        /// a process-wide dictionary: keying on the raw path lets a caller grow that
        /// dictionary without bound by walking made-up URLs. The number of route templates is
        /// fixed, and everything that matched no route shares a single bucket.
        /// </summary>
        public static string GetEndpointKey(HttpContext context)
        {
            var endpoint = context.GetEndpoint();

            if (endpoint is RouteEndpoint routeEndpoint && !string.IsNullOrEmpty(routeEndpoint.RoutePattern.RawText))
            {
                return routeEndpoint.RoutePattern.RawText.ToLowerInvariant();
            }

            return endpoint?.DisplayName?.ToLowerInvariant() ?? UnmatchedEndpointKey;
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