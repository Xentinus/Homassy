using Homassy.Data.Enums;
using Homassy.API.Extensions;
using Homassy.API.Models.ApplicationSettings;
using Homassy.Data.Models.Common;
using Homassy.API.Models.RateLimit;
using Homassy.API.Services;
using Microsoft.Extensions.Options;
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
        private readonly IOptionsMonitor<RateLimitSettings> _settings;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <remarks>
        /// The limits come from injected options rather than the process-wide
        /// <see cref="ConfigService"/> on purpose. The limiter is the one component the whole test
        /// suite runs through, and reading a mutable static at request time let a unit test that
        /// installs deliberately tiny limits bleed into every integration test running in parallel —
        /// which is exactly how a 401 assertion started seeing 429. Constructor injection makes that
        /// impossible: <c>UseMiddleware</c> resolves this from the app's own container.
        ///
        /// <see cref="IOptionsMonitor{TOptions}"/> rather than <see cref="IOptions{TOptions}"/> so a
        /// configuration reload still takes effect, which the per-request <c>IConfiguration</c> reads
        /// this replaced also allowed. What is gone is the per-request cost of those reads and the
        /// <c>int.Parse</c> that turned a typo in a setting into a 500 on every request instead of a
        /// startup failure — see <see cref="RateLimitSettings"/>.
        /// </remarks>
        public RateLimitingMiddleware(RequestDelegate next, IOptionsMonitor<RateLimitSettings> settings)
        {
            _next = next;
            _settings = settings;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.GetClientIpAddress();
            var endpoint = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var endpointKey = GetEndpointKey(context);
            var method = context.Request.Method;
            var settings = _settings.CurrentValue;

            // One registered attempt per scope, and the status that attempt produced. Counting and
            // then re-reading the bucket was two independent reads per scope per request, and the
            // headers could describe a bucket state that was never the one this request saw.
            var globalRateLimitKey = $"global:{clientIp}";
            var globalStatus = RateLimitService.RegisterAttempt(globalRateLimitKey, settings.GlobalMaxAttempts, settings.GlobalWindow);

            if (globalStatus.IsLimited)
            {
                Log.Warning($"Global rate limit exceeded from IP {clientIp} for endpoint {method} {endpoint}");
                await WriteRateLimitedResponseAsync(context, globalStatus);
                return;
            }

            var endpointRateLimitKey = $"endpoint:{endpointKey}:{clientIp}";
            var endpointStatus = RateLimitService.RegisterAttempt(endpointRateLimitKey, settings.EndpointMaxAttempts, settings.EndpointWindow);

            if (endpointStatus.IsLimited)
            {
                Log.Warning($"Endpoint rate limit exceeded from IP {clientIp} for {method} {endpoint}");
                await WriteRateLimitedResponseAsync(context, endpointStatus);
                return;
            }

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

        private static async Task WriteRateLimitedResponseAsync(HttpContext context, RateLimitStatus status)
        {
            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            AddRateLimitHeaders(context, status);

            var errorResponse = ApiResponse.ErrorResponse(ErrorCodes.RateLimitExceeded);

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonOptions));
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
