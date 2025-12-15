using Homassy.API.Models;
using Serilog;
using System.Diagnostics;

namespace Homassy.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestLoggingOptions _options;

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Auth-Token"
    };

    private static readonly HashSet<string> SensitiveQueryParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "token",
        "secret",
        "apikey",
        "api_key",
        "access_token",
        "refresh_token"
    };

    public RequestLoggingMiddleware(RequestDelegate next, RequestLoggingOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            LogRequest(context, stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogRequest(HttpContext context, long elapsedMs)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString() ?? "-";
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var statusCode = context.Response.StatusCode;
        var queryString = SanitizeQueryString(context.Request.QueryString.Value);

        var logMessage = string.IsNullOrEmpty(queryString)
            ? $"HTTP {method} {path} responded {statusCode} in {elapsedMs}ms [CorrelationId: {correlationId}]"
            : $"HTTP {method} {path}{queryString} responded {statusCode} in {elapsedMs}ms [CorrelationId: {correlationId}]";

        if (statusCode >= 500)
        {
            Log.Error(logMessage);
        }
        else if (statusCode >= 400)
        {
            Log.Warning(logMessage);
        }
        else
        {
            Log.Information(logMessage);
        }

        if (_options.DetailedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            LogDetailedRequest(context, correlationId);
        }
    }

    private void LogDetailedRequest(HttpContext context, string correlationId)
    {
        var headers = context.Request.Headers
            .Where(h => !SensitiveHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        Log.Debug(
            $"Request details [CorrelationId: {correlationId}]: Headers: {headers}, ContentType: {context.Request.ContentType}, ContentLength: {context.Request.ContentLength}");
    }

    private bool IsExcludedPath(PathString path)
    {
        return _options.ExcludedPaths.Any(excluded =>
            path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private string? SanitizeQueryString(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return null;
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString);
        var sanitized = new List<string>();

        foreach (var param in query)
        {
            if (SensitiveQueryParams.Contains(param.Key))
            {
                sanitized.Add($"{param.Key}=[REDACTED]");
            }
            else
            {
                sanitized.Add($"{param.Key}={param.Value}");
            }
        }

        return sanitized.Count > 0 ? "?" + string.Join("&", sanitized) : null;
    }
}
