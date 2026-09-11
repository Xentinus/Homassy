using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

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
        var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryExpirationService>();

        // The notification centre's retention sweep (#116), before the early return below so it
        // still runs on an installation with no eligible push recipients at all. It rides on this
        // worker rather than getting one of its own: this is the hourly notification scheduler,
        // hourly is far more often than a 60-day window needs, and the alternative was a whole
        // BackgroundService whose entire body is one DELETE.
        await PruneNotificationsAsync(context, utcNow, cancellationToken);

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
                await ProcessUserNotificationsAsync(context, inventoryService, userData, utcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing push notifications for user {UserId}", userData.UserId);
            }
        }
    }

    private async Task ProcessUserNotificationsAsync(
        HomassyDbContext context,
        InventoryExpirationService inventoryService,
        EligibleUserData userData,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var timeZoneId = userData.TimeZone.ToTimeZoneId();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var userLocalTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

        Log.Information("User {UserId} local time: {LocalTime} (timezone: {TZ})", userData.UserId, userLocalTime, timeZoneId);

        if (userLocalTime.Hour != 07)
        {
            Log.Information("Skipping user {UserId} - not send time (current hour: {Hour})", userData.UserId, userLocalTime.Hour);
            return;
        }

        Log.Information("Sending notification to user {UserId}", userData.UserId);

        var today = userLocalTime.Date;
        var isMonday = userLocalTime.DayOfWeek == DayOfWeek.Monday;

        if (userData.PushWeeklySummaryEnabled && isMonday)
        {
            await SendWeeklyNotificationAsync(context, inventoryService, userData, today, cancellationToken);
        }
    }

    private async Task SendWeeklyNotificationAsync(
        HomassyDbContext context,
        InventoryExpirationService inventoryService,
        EligibleUserData userData,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var subscriptions = await context.UserPushSubscriptions
            .Where(s => s.UserId == userData.UserId && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        if (subscriptions.All(s => s.LastWeeklyNotificationSentAt?.Date == today))
            return;

        var count = await inventoryService.GetExpiringCountForUserAsync(
            userData.UserId, userData.FamilyId, cancellationToken);

        var (title, body) = PushNotificationContentService.GetWeeklyNotificationContent(userData.Language, count);
        var actionTitle = GetActionTitle(userData.Language);

        // The inbox row for the same summary (#116), added from the same method that sends the
        // push so the two cannot diverge. Recorded before the sends, because the send loop below is a
        // network call per device and rows added first all share one timestamp - a batch that
        // belongs to one event should group under one moment in the inbox.
        //
        // Note the eligibility above still requires a live push subscription, so a user with push
        // switched off entirely gets no weekly-summary row. That is deliberate: the per-device
        // `LastWeeklyNotificationSentAt` stamp is what makes this notification fire once, and a
        // user with no subscription has no row to stamp. The weekly summary is the one periodic
        // digest here; every event-driven notification does reach an in-app-only recipient.
        if (userData.InAppNotificationsEnabled)
        {
            NotificationFunctions.Record(
                context,
                [userData.UserId],
                [NotificationEnvelopes.WeeklySummary(count)],
                "/products",
                DateTime.UtcNow);

            await context.SaveChangesAsync(cancellationToken);
        }

        // The one notification in the app that carries a count the icon badge can show (#130):
        // the service worker writes it onto the app icon, so a user who never opens the app still
        // sees the right number. Only sent here — a recipient reached this far because their push
        // preference is on, so the "respect the user's preference" rule is satisfied by
        // construction rather than by a second check in the worker.
        await SendToSubscriptionsAsync(context, subscriptions, title, body, actionTitle, today, isWeekly: true, badgeCount: count, cancellationToken);
    }

    private async Task SendToSubscriptionsAsync(
        HomassyDbContext context,
        List<UserPushSubscription> subscriptions,
        string title,
        string body,
        string actionTitle,
        DateTime today,
        bool isWeekly,
        int? badgeCount,
        CancellationToken cancellationToken)
    {
        var hasChanges = false;

        foreach (var subscription in subscriptions)
        {
            if (isWeekly && subscription.LastWeeklyNotificationSentAt?.Date == today)
                continue;
            if (!isWeekly && subscription.LastDailyNotificationSentAt?.Date == today)
                continue;

            var success = await _webPushService.SendNotificationAsync(
                subscription, title, body, "/products", actionTitle, badgeCount, cancellationToken);

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

    private static string GetActionTitle(Language language) => language switch
    {
        Language.German => "Homassy öffnen",
        Language.English => "Open Homassy",
        _ => "Homassy megnyitása"
    };

    /// <summary>
    /// Drops notification-centre rows past their retention window. Never throws out of here: a
    /// failed sweep must not stop the hour's notifications from being sent.
    /// </summary>
    private static async Task PruneNotificationsAsync(
        HomassyDbContext context,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var removed = await NotificationFunctions.PruneAsync(context, utcNow, cancellationToken);
            if (removed > 0)
            {
                Log.Information(
                    "Pruned {Count} notifications older than {Days} days",
                    removed, NotificationFunctions.RetentionDays);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to prune old notifications");
        }
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
                PushWeeklySummaryEnabled = u.NotificationPreferences!.PushWeeklySummaryEnabled,
                InAppNotificationsEnabled = u.NotificationPreferences!.InAppNotificationsEnabled
            })
            .ToListAsync(cancellationToken);
    }
}

internal sealed class EligibleUserData
{
    public int UserId { get; init; }
    public int? FamilyId { get; init; }
    public UserTimeZone TimeZone { get; init; }
    public Language Language { get; init; }
    public bool PushNotificationsEnabled { get; init; }
    public bool PushWeeklySummaryEnabled { get; init; }

    /// <summary>Whether this user's weekly summary is also recorded in the notification centre (#116).</summary>
    public bool InAppNotificationsEnabled { get; init; }
}
