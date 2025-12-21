using Homassy.API.Models.ApplicationSettings;
using Microsoft.Extensions.Options;

namespace Homassy.API.Services;

public class GracefulShutdownService : IHostedService
{
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly GracefulShutdownSettings _settings;

    public GracefulShutdownService(
        ILogger<GracefulShutdownService> logger,
        IHostApplicationLifetime lifetime,
        IOptions<GracefulShutdownSettings> settings)
    {
        _logger = logger;
        _lifetime = lifetime;
        _settings = settings.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStopping.Register(OnShutdown);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void OnShutdown()
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Graceful shutdown disabled, stopping immediately");
            return;
        }

        _logger.LogInformation($"Graceful shutdown initiated, waiting up to {_settings.TimeoutSeconds} seconds for requests to complete");
    }
}
