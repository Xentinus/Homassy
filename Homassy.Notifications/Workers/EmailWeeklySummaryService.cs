using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

public sealed class EmailWeeklySummaryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public EmailWeeklySummaryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Email weekly summary service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailSummariesAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in email weekly summary service");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Email weekly summary service stopped");
    }

    private async Task ProcessEmailSummariesAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        Log.Information("Email weekly summary check started at UTC: {UtcNow}", utcNow);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryExpirationService>();
        var emailClient = scope.ServiceProvider.GetRequiredService<EmailServiceClient>();

        var eligibleUsers = await GetEligibleUsersAsync(context, cancellationToken);

        if (eligibleUsers.Count == 0)
        {
            Log.Information("No eligible users found for email weekly summary");
            return;
        }

        Log.Information("Processing email weekly summaries for {Count} eligible users", eligibleUsers.Count);

        foreach (var userData in eligibleUsers)
        {
            try
            {
                await ProcessUserEmailSummaryAsync(context, inventoryService, emailClient, userData, utcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing email weekly summary for user {UserId}", userData.UserId);
            }
        }
    }

    private async Task ProcessUserEmailSummaryAsync(
        HomassyDbContext context,
        InventoryExpirationService inventoryService,
        EmailServiceClient emailClient,
        EmailEligibleUserData userData,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var timeZoneId = userData.TimeZone.ToTimeZoneId();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var userLocalTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

        Log.Information("User {UserId} local time: {LocalTime} (timezone: {TZ})", userData.UserId, userLocalTime, timeZoneId);

        if (userLocalTime.DayOfWeek != DayOfWeek.Monday || userLocalTime.Hour != 07)
        {
            Log.Information("Skipping user {UserId} - not Monday 07:xx local time (day: {Day}, hour: {Hour})",
                userData.UserId, userLocalTime.DayOfWeek, userLocalTime.Hour);
            return;
        }

        var today = userLocalTime.Date;

        // Deduplication: skip if already sent today
        if (userData.LastWeeklyEmailSentAt?.Date == today)
        {
            Log.Information("Skipping user {UserId} - weekly email already sent today", userData.UserId);
            return;
        }

        Log.Information("Sending weekly email summary to user {UserId} ({Email})", userData.UserId, userData.Email);

        var allItems = await inventoryService.GetExpiringItemsForUserAsync(userData.UserId, userData.FamilyId, cancellationToken);
        var expiredItems = allItems.Where(i => i.IsExpired).ToList();
        var expiringSoonItems = allItems.Where(i => !i.IsExpired).ToList();

        var success = await emailClient.SendWeeklySummaryAsync(
            userData.Email,
            userData.Language,
            userData.Name,
            expiredItems,
            expiringSoonItems,
            cancellationToken);

        if (success)
        {
            // Update LastWeeklyEmailSentAt to prevent duplicate sends
            var prefs = await context.UserNotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userData.UserId, cancellationToken);

            if (prefs is not null)
            {
                prefs.LastWeeklyEmailSentAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }

            Log.Information("Weekly email summary sent to user {UserId} ({Email})", userData.UserId, userData.Email);
        }
        else
        {
            Log.Warning("Failed to send weekly email summary to user {UserId} ({Email})", userData.UserId, userData.Email);
        }
    }

    private static async Task<List<EmailEligibleUserData>> GetEligibleUsersAsync(
        HomassyDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.NotificationPreferences != null)
            .Where(u => u.NotificationPreferences!.EmailWeeklySummaryEnabled)
            .Select(u => new EmailEligibleUserData
            {
                UserId = u.Id,
                FamilyId = u.FamilyId,
                Email = u.Email,
                Name = u.Name,
                TimeZone = u.Profile != null ? u.Profile.DefaultTimeZone : UserTimeZone.CentralEuropeStandardTime,
                Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian,
                LastWeeklyEmailSentAt = u.NotificationPreferences!.LastWeeklyEmailSentAt
            })
            .ToListAsync(cancellationToken);
    }
}

internal sealed class EmailEligibleUserData
{
    public int UserId { get; init; }
    public int? FamilyId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public UserTimeZone TimeZone { get; init; }
    public Language Language { get; init; }
    public DateTime? LastWeeklyEmailSentAt { get; init; }
}

