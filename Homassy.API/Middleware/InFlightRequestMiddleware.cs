using Homassy.API.Services;

namespace Homassy.API.Middleware;

/// <summary>
/// Keeps <see cref="InFlightRequestTracker"/> in step with the pipeline, so shutdown can report
/// whether the drain finished or the timeout cut it off. First in the pipeline on purpose: a
/// request the counter never saw is a request the shutdown log claims is not there.
/// </summary>
public class InFlightRequestMiddleware
{
    /// <summary>The path segment every SignalR hub is mapped under - see <c>Program.cs</c>.</summary>
    public const string HubPathPrefix = "/hubs";

    private readonly RequestDelegate _next;
    private readonly InFlightRequestTracker _tracker;

    public InFlightRequestMiddleware(RequestDelegate next, InFlightRequestTracker tracker)
    {
        _next = next;
        _tracker = tracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsLongLivedConnection(context))
        {
            await _next(context);
            return;
        }

        _tracker.Enter();

        try
        {
            await _next(context);
        }
        finally
        {
            _tracker.Exit();
        }
    }

    /// <summary>
    /// A SignalR connection, which does not return from the pipeline until the client goes away.
    /// </summary>
    /// <remarks>
    /// Counting these would make the number meaningless: with four hubs mapped, a household with
    /// three devices connected keeps a dozen "requests" permanently in flight, the idle-stop
    /// fast path would never fire on the real box, and the "all in-flight requests completed"
    /// timing would be measuring socket teardown rather than the request drain it names. Their
    /// shutdown is SignalR's own - <c>HttpConnectionManager</c> closes them on
    /// <c>ApplicationStopping</c> - so nothing is lost by leaving them out of this count.
    ///
    /// Both tests are needed: the WebSocket check misses the long-polling and server-sent-events
    /// transports, and the path check is what catches those. This middleware runs before
    /// <c>UseRouting</c> (deliberately - a request that matched no route is still in flight), so
    /// the matched endpoint is not available to check instead.
    /// </remarks>
    private static bool IsLongLivedConnection(HttpContext context) =>
        context.WebSockets.IsWebSocketRequest
        || context.Request.Path.StartsWithSegments(HubPathPrefix, StringComparison.OrdinalIgnoreCase);
}
