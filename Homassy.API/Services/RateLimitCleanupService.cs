namespace Homassy.API.Services
{
    public class RateLimitCleanupService  : BackgroundService
    {
        private readonly RateLimitService _rateLimitService;
        private readonly ILogger<RateLimitCleanupService> _logger;

        public RateLimitCleanupService(
            RateLimitService rateLimitService,
            ILogger<RateLimitCleanupService> logger)
        {
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Rate limit cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    _rateLimitService.CleanupExpiredEntries(TimeSpan.FromHours(2));
                    _logger.LogInformation("Rate limit cleanup completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in rate limit cleanup");
                }
            }
        }
    }
}
