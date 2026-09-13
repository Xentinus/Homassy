using Homassy.API.Models.ApplicationSettings;
using Microsoft.Extensions.Options;

namespace Homassy.API.Services;

/// <summary>
/// Reports what the shutdown drain is waiting for and whether it finished.
/// </summary>
/// <remarks>
/// The drain itself is configured, not implemented here: <c>Program.cs</c> sets
/// <see cref="HostOptions.ShutdownTimeout"/> from <see cref="GracefulShutdownSettings"/>, and
/// Kestrel stops accepting connections on the stop signal and then waits for the requests already
/// in flight within that window.
///
/// This used to be a log line and nothing else, next to a <c>Thread.Sleep</c> in <c>Program.cs</c>
/// that waited out the full timeout on every stop whether or not anything was in flight — so an
/// idle API took 30 seconds longer to stop than it needed to, and Docker's 10-second default grace
/// period killed the container mid-sleep, before the pipeline had drained and before the logs were
/// flushed. Both are gone. What is left is the part that was actually missing: a stop that says
/// how many requests it is waiting for and how long they took (#85).
/// </remarks>
public class GracefulShutdownService : IHostedService
{
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly InFlightRequestTracker _tracker;
    private readonly GracefulShutdownSettings _settings;

    public GracefulShutdownService(
        ILogger<GracefulShutdownService> logger,
        IHostApplicationLifetime lifetime,
        InFlightRequestTracker tracker,
        IOptions<GracefulShutdownSettings> settings)
    {
        _logger = logger;
        _lifetime = lifetime;
        _tracker = tracker;
        _settings = settings.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _tracker.Drained += OnDrained;
        _lifetime.ApplicationStopping.Register(OnShutdown);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _tracker.Drained -= OnDrained;
        return Task.CompletedTask;
    }

    private void OnShutdown()
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Graceful shutdown disabled, stopping immediately");
            return;
        }

        var inFlight = _tracker.BeginDraining();

        if (inFlight == 0)
        {
            _logger.LogInformation("Shutdown signal received with no requests in flight, stopping immediately");
            return;
        }

        _logger.LogInformation(
            "Shutdown signal received with {InFlightRequests} request(s) in flight; the host waits up to {TimeoutSeconds}s for them",
            inFlight,
            _settings.TimeoutSeconds);
    }

    private void OnDrained(TimeSpan elapsed)
    {
        // Only interesting when something was actually waited for; the idle case is already logged
        // by OnShutdown, which is what raised this.
        if (elapsed < TimeSpan.FromMilliseconds(1))
        {
            return;
        }

        _logger.LogInformation("All in-flight requests completed {ElapsedMs}ms after the shutdown signal", (int)elapsed.TotalMilliseconds);
    }
}
