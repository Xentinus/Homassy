using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Services.Background;

public sealed class PushNotificationSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebPushService _webPushService;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public PushNotificationSchedulerService(IServiceScopeFactory scopeFactory, IWebPushService webPushService)
    {
        _scopeFactory = scopeFactory;
        _webPushService = webPushService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Push notification scheduler service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in push notification scheduler service");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Push notification scheduler service stopped");
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        Log.Information("Push notification check started at UTC: {UtcNow}", utcNow);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var eligibleUsers = await GetEligibleUsersAsync(context, cancellationToken);

        if (eligibleUsers.Count == 0)
        {
            Log.Information("No eligible users found for push notifications");
            return;
        }

        Log.Information("Processing push notifications for {Count} eligible users", eligibleUsers.Count);

        foreach (var userData in eligibleUsers)
        {
            try
            {
                await ProcessUserNotificationsAsync(context, userData, utcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing push notifications for user {UserId}", userData.UserId);
            }
        }
    }

    private async Task ProcessUserNotificationsAsync(
        HomassyDbContext context,
        EligibleUserData userData,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // Convert current UTC time to user's local time
        var timeZoneId = userData.TimeZone.ToTimeZoneId();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var userLocalTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

        Log.Information("User {UserId} local time: {LocalTime} (timezone: {TZ})", userData.UserId, userLocalTime, timeZoneId);

        // Check if it's 7 AM in user's timezone
        if (userLocalTime.Hour != 07)
        {
            Log.Information("Skipping user {UserId} - not send time (current hour: {Hour})", userData.UserId, userLocalTime.Hour);
            return;
        }

        Log.Information("Sending notification to user {UserId}", userData.UserId);

        var today = userLocalTime.Date;
        var isMonday = userLocalTime.DayOfWeek == DayOfWeek.Monday;

        // Weekly notification (Monday)
        if (userData.PushWeeklySummaryEnabled && isMonday)
        {
            await SendWeeklyNotificationAsync(context, userData, today, cancellationToken);
        }
    }

    private async Task SendWeeklyNotificationAsync(
        HomassyDbContext context,
        EligibleUserData userData,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var subscriptions = await context.UserPushSubscriptions
            .Where(s => s.UserId == userData.UserId && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        // Check deduplication
        if (subscriptions.All(s => s.LastWeeklyNotificationSentAt?.Date == today))
            return;

        var count = await PushNotificationFunctions.GetExpiringCountForUserAsync(
            userData.UserId, userData.FamilyId, context, cancellationToken);

        // Weekly always sends (even with 0 count)
        var (title, body) = PushNotificationContentService.GetWeeklyNotificationContent(userData.Language, count);

        var actionTitle = GetActionTitle(userData.Language);
        await SendToSubscriptionsAsync(context, subscriptions, title, body, actionTitle, today, isWeekly: true, cancellationToken);
    }

    private async Task SendToSubscriptionsAsync(
        HomassyDbContext context,
        List<UserPushSubscription> subscriptions,
        string title,
        string body,
        string actionTitle,
        DateTime today,
        bool isWeekly,
        CancellationToken cancellationToken)
    {
        var hasChanges = false;

        foreach (var subscription in subscriptions)
        {
            // Skip if already sent to this device today
            if (isWeekly && subscription.LastWeeklyNotificationSentAt?.Date == today)
                continue;
            if (!isWeekly && subscription.LastDailyNotificationSentAt?.Date == today)
                continue;

            var success = await _webPushService.SendNotificationAsync(
                subscription, title, body, "/products", actionTitle, cancellationToken);

            if (success)
            {
                if (isWeekly)
                    subscription.LastWeeklyNotificationSentAt = DateTime.UtcNow;
                else
                    subscription.LastDailyNotificationSentAt = DateTime.UtcNow;
                hasChanges = true;
            }
            else
            {
                // Subscription invalid - soft delete
                subscription.DeleteRecord();
                hasChanges = true;
                Log.Information("Removed invalid push subscription {Endpoint} for user {UserId}",
                    subscription.Endpoint, subscription.UserId);
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GetActionTitle(Language language)
    {
        return language switch
        {
            Language.German => "Homassy öffnen",
            Language.English => "Open Homassy",
            _ => "Homassy megnyitása"
        };
    }

    private static async Task<List<EligibleUserData>> GetEligibleUsersAsync(
        HomassyDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.NotificationPreferences != null)
            .Where(u => u.NotificationPreferences!.PushNotificationsEnabled ||
                        u.NotificationPreferences!.PushWeeklySummaryEnabled)
            .Where(u => context.UserPushSubscriptions
                .Any(s => s.UserId == u.Id && !s.IsDeleted))
            .Select(u => new EligibleUserData
            {
                UserId = u.Id,
                FamilyId = u.FamilyId,
                TimeZone = u.Profile != null ? u.Profile.DefaultTimeZone : UserTimeZone.CentralEuropeStandardTime,
                Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian,
                PushNotificationsEnabled = u.NotificationPreferences!.PushNotificationsEnabled,
                PushWeeklySummaryEnabled = u.NotificationPreferences!.PushWeeklySummaryEnabled
            })
            .ToListAsync(cancellationToken);
    }
}

internal class EligibleUserData
{
    public int UserId { get; init; }
    public int? FamilyId { get; init; }
    public UserTimeZone TimeZone { get; init; }
    public Language Language { get; init; }
    public bool PushNotificationsEnabled { get; init; }
    public bool PushWeeklySummaryEnabled { get; init; }
}
