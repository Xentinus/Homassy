using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Background service that monitors family shopping lists for new item additions.
/// Every 5 minutes it checks for new ShoppingListItemAdd activities on family lists
/// (where the family has at least 2 members). When no new items have been added
/// for 5 minutes after an active session, it notifies all family members who did NOT
/// contribute to that session. Sessions are held purely in memory and cleared after
/// notifications are dispatched.
/// </summary>
public sealed class ShoppingListActivityMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebPushService _webPushService;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(5);

    // Key: ShoppingListId
    private readonly Dictionary<int, ShoppingListSession> _activeSessions = new();
    private DateTime _lastRun;

    public ShoppingListActivityMonitorService(IServiceScopeFactory scopeFactory, IWebPushService webPushService)
    {
        _scopeFactory = scopeFactory;
        _webPushService = webPushService;
        _lastRun = DateTime.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Shopping list activity monitor service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in shopping list activity monitor service");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Shopping list activity monitor service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = _lastRun;
        _lastRun = now;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        // Step 1: Load new ShoppingListItemAdd activities since the last run
        var activityData = await context.Activities
            .Where(a => a.ActivityType == ActivityType.ShoppingListItemAdd
                && a.FamilyId != null
                && a.Timestamp >= since
                && a.Timestamp < now)
            .Select(a => new { a.UserId, a.FamilyId, a.Timestamp, ItemId = a.RecordId })
            .ToListAsync(cancellationToken);

        if (activityData.Count > 0)
        {
            // Step 2: Resolve ShoppingListId for each activity (via ShoppingListItem)
            var itemIds = activityData.Select(a => a.ItemId).Distinct().ToList();
            var itemToListId = await context.ShoppingListItems
                .Where(sli => itemIds.Contains(sli.Id))
                .Select(sli => new { sli.Id, sli.ShoppingListId })
                .ToDictionaryAsync(sli => sli.Id, sli => sli.ShoppingListId, cancellationToken);

            // Step 3: Load shopping list metadata for resolved list IDs
            var listIds = itemToListId.Values.Distinct().ToList();
            var listInfo = await context.ShoppingLists
                .Where(sl => listIds.Contains(sl.Id) && !sl.IsDeleted && sl.FamilyId != null)
                .Select(sl => new { sl.Id, sl.Name, sl.FamilyId })
                .ToDictionaryAsync(sl => sl.Id, sl => sl, cancellationToken);

            // Step 4: Identify families with at least 2 active members
            var familyIds = listInfo.Values.Select(sl => sl.FamilyId!.Value).Distinct().ToList();
            var eligibleFamilyIds = await context.Users
                .Where(u => u.FamilyId != null && familyIds.Contains(u.FamilyId!.Value) && !u.IsDeleted)
                .GroupBy(u => u.FamilyId!.Value)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken);

            var eligibleFamilySet = new HashSet<int>(eligibleFamilyIds);

            // Step 5: Update in-memory sessions for eligible activities
            foreach (var activity in activityData)
            {
                if (!itemToListId.TryGetValue(activity.ItemId, out var shoppingListId))
                    continue;

                if (!listInfo.TryGetValue(shoppingListId, out var list))
                    continue;

                if (!eligibleFamilySet.Contains(list.FamilyId!.Value))
                    continue;

                if (_activeSessions.TryGetValue(shoppingListId, out var existing))
                {
                    existing.ContributingUserIds.Add(activity.UserId);
                    existing.NewItemCount++;
                    if (activity.Timestamp > existing.LastActivityAt)
                        existing.LastActivityAt = activity.Timestamp;
                }
                else
                {
                    _activeSessions[shoppingListId] = new ShoppingListSession
                    {
                        ShoppingListId = shoppingListId,
                        FamilyId = list.FamilyId!.Value,
                        ShoppingListName = list.Name,
                        LastActivityAt = activity.Timestamp,
                        ContributingUserIds = new HashSet<int> { activity.UserId },
                        NewItemCount = 1
                    };
                }
            }
        }

        // Step 6: Find sessions with no new activity for >= sessionTimeout and notify
        var staleSessions = _activeSessions.Values
            .Where(s => s.LastActivityAt < now - _sessionTimeout)
            .ToList();

        foreach (var session in staleSessions)
        {
            try
            {
                await SendNotificationsAsync(context, session, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending shopping list notifications for list {ShoppingListId}", session.ShoppingListId);
            }
            finally
            {
                _activeSessions.Remove(session.ShoppingListId);
            }
        }

        if (staleSessions.Count > 0 || activityData.Count > 0)
        {
            Log.Information(
                "Shopping list monitor: {NewActivities} new activities processed, {Stale} sessions notified, {Active} active sessions remaining",
                activityData.Count, staleSessions.Count, _activeSessions.Count);
        }
    }

    private async Task SendNotificationsAsync(
        HomassyDbContext context,
        ShoppingListSession session,
        CancellationToken cancellationToken)
    {
        var contributingIds = session.ContributingUserIds;

        var recipients = await context.Users
            .AsNoTracking()
            .Where(u => u.FamilyId == session.FamilyId
                && !u.IsDeleted
                && !contributingIds.Contains(u.Id)
                && u.NotificationPreferences != null
                && u.NotificationPreferences.PushNotificationsEnabled)
            .Where(u => context.UserPushSubscriptions.Any(s => s.UserId == u.Id && !s.IsDeleted))
            .Select(u => new
            {
                u.Id,
                Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian
            })
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            Log.Information(
                "Shopping list '{ListName}' session closed – no eligible recipients (list {ShoppingListId})",
                session.ShoppingListName, session.ShoppingListId);
            return;
        }

        var hasChanges = false;

        foreach (var recipient in recipients)
        {
            var subscriptions = await context.UserPushSubscriptions
                .Where(s => s.UserId == recipient.Id && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            var (title, body) = PushNotificationContentService.GetShoppingListActivityContent(
                recipient.Language, session.ShoppingListName, session.NewItemCount);

            var actionTitle = GetActionTitle(recipient.Language);

            foreach (var subscription in subscriptions)
            {
                var success = await _webPushService.SendNotificationAsync(
                    subscription, title, body, "/shopping-lists", actionTitle, cancellationToken);

                if (!success)
                {
                    subscription.DeleteRecord();
                    hasChanges = true;
                    Log.Information(
                        "Removed invalid push subscription {Endpoint} for user {UserId}",
                        subscription.Endpoint, recipient.Id);
                }
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        Log.Information(
            "Shopping list '{ListName}' notification sent to {Count} users (list {ShoppingListId})",
            session.ShoppingListName, recipients.Count, session.ShoppingListId);
    }

    private static string GetActionTitle(Language language) => language switch
    {
        Language.German => "Homassy öffnen",
        Language.English => "Open Homassy",
        _ => "Homassy megnyitása"
    };
}

internal sealed class ShoppingListSession
{
    public int ShoppingListId { get; init; }
    public int FamilyId { get; init; }
    public string ShoppingListName { get; set; } = string.Empty;
    public DateTime LastActivityAt { get; set; }
    public HashSet<int> ContributingUserIds { get; init; } = new();
    public int NewItemCount { get; set; }
}
