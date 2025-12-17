using Homassy.API.Context;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Services.Background;

public sealed class TokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public TokenCleanupService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Token cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in token cleanup service");
            }
        }

        Log.Information("Token cleanup service stopped");
    }

    private async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var utcNow = DateTime.UtcNow;

        var verificationCodeCleanup = await context.UserAuthentications
            .Where(a => a.VerificationCodeExpiry != null && a.VerificationCodeExpiry < utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.VerificationCode, (string?)null)
                .SetProperty(a => a.VerificationCodeExpiry, (DateTime?)null),
                cancellationToken);

        var previousTokenCleanup = await context.UserAuthentications
            .Where(a => a.PreviousRefreshTokenExpiry != null && a.PreviousRefreshTokenExpiry < utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.PreviousRefreshToken, (string?)null)
                .SetProperty(a => a.PreviousRefreshTokenExpiry, (DateTime?)null),
                cancellationToken);

        if (verificationCodeCleanup > 0 || previousTokenCleanup > 0)
        {
            Log.Information($"Token cleanup completed: {verificationCodeCleanup} verification codes, {previousTokenCleanup} previous refresh tokens");
        }
    }
}
