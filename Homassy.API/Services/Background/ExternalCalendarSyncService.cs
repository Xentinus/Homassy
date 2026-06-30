using Homassy.API.Context;
using Homassy.API.Functions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Services.Background;

public sealed class ExternalCalendarSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(1);

    public ExternalCalendarSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("External calendar sync service started");

        await SyncAllAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SyncInterval, stoppingToken);
                await SyncAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "External calendar sync service encountered an error; retrying in 5 minutes");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("External calendar sync service stopped");
    }

    private async Task SyncAllAsync(CancellationToken ct)
    {
        Log.Information("External calendar sync: starting sync of all enabled calendars...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

            var calendars = await context.FamilyExternalCalendars
                .Where(c => c.IsEnabled)
                .ToListAsync(ct);

            if (calendars.Count == 0)
            {
                Log.Debug("External calendar sync: no enabled calendars found");
                return;
            }

            Log.Information("External calendar sync: syncing {Count} calendar(s)", calendars.Count);

            var httpClient = _httpClientFactory.CreateClient("ExternalCalendarSync");
            int synced = 0, failed = 0;

            foreach (var calendar in calendars)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await ExternalCalendarFunctions.SyncCalendarAsync(calendar, context, httpClient, ct);
                    synced++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Error(ex, "External calendar sync: failed for calendar {CalendarId}", calendar.PublicId);
                }
            }

            await context.SaveChangesAsync(ct);

            Log.Information(
                "External calendar sync complete — synced: {Synced}, failed: {Failed}",
                synced, failed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "External calendar sync: unexpected error during full sync");
        }
    }
}
