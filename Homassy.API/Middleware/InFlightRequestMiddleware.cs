using Homassy.API.Services;

namespace Homassy.API.Middleware;

/// <summary>
/// Keeps <see cref="InFlightRequestTracker"/> in step with the pipeline, so shutdown can report
/// whether the drain finished or the timeout cut it off. First in the pipeline on purpose: a
/// request the counter never saw is a request the shutdown log claims is not there.
/// </summary>
public class InFlightRequestMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InFlightRequestTracker _tracker;

    public InFlightRequestMiddleware(RequestDelegate next, InFlightRequestTracker tracker)
    {
        _next = next;
        _tracker = tracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
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
}
