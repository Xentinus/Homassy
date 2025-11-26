using Serilog;

namespace Homassy.API.Services
{
    public class RateLimitCleanupService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Rate limit cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    RateLimitService.CleanupExpiredEntries(TimeSpan.FromHours(2));
                    Log.Information("Rate limit cleanup completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in rate limit cleanup");
                }
            }
        }
    }
}