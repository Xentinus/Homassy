using Homassy.API.Exceptions;
using Homassy.API.Models.ApplicationSettings;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.RegularExpressions;

namespace Homassy.API.Middleware;

public class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestTimeoutSettings _settings;

    public RequestTimeoutMiddleware(RequestDelegate next, IOptions<RequestTimeoutSettings> settings)
    {
        _next = next;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var timeoutSeconds = GetTimeoutForEndpoint(context.Request.Path.Value);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted,
            timeoutCts.Token);

        var originalCancellationToken = context.RequestAborted;
        context.RequestAborted = linkedCts.Token;

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !originalCancellationToken.IsCancellationRequested)
        {
            Log.Warning("Request timed out after {TimeoutSeconds}s for {Method} {Path}",
                timeoutSeconds, context.Request.Method, context.Request.Path);

            throw new RequestTimeoutException($"Request timed out after {timeoutSeconds} seconds");
        }
    }

    private int GetTimeoutForEndpoint(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return _settings.DefaultTimeoutSeconds;
        }

        foreach (var endpoint in _settings.Endpoints)
        {
            if (MatchesPattern(path, endpoint.PathPattern))
            {
                return endpoint.TimeoutSeconds;
            }
        }

        return _settings.DefaultTimeoutSeconds;
    }

    private static bool MatchesPattern(string path, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }
}
