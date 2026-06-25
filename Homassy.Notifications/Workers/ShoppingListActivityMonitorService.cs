using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Background service that monitors family shopping lists for changes and notifies family members.
///
/// Two kinds of notifications are produced:
/// <list type="bullet">
/// <item>Immediate, per-event notifications when a family shopping list is created or deleted.</item>
/// <item>Aggregated, per-action session summaries for item changes. Item add/edit/delete/purchase
/// activities on a list are accumulated into an in-memory session; once no further activity occurs
/// for the session timeout, one notification per non-zero action type (added / edited / deleted /
/// purchased) is sent to all family members who did NOT contribute to that session, each carrying
/// the count.</item>
/// </list>
/// Only families with at least two active members are notified. Sessions are held purely in memory
/// and cleared after notifications are dispatched.
/// </summary>
public sealed class ShoppingListActivityMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(5);

    private static readonly ActivityType[] TrackedItemActivities =
    {
        ActivityType.ShoppingListItemAdd,
        ActivityType.ShoppingListItemUpdate,
        ActivityType.ShoppingListItemDelete,
        ActivityType.ShoppingListItemPurchase,
        ActivityType.ShoppingListItemQuickPurchase
    };

    // Key: ShoppingListId
    private readonly Dictionary<int, ShoppingListSession> _activeSessions = new();
    private DateTime _lastRun;

    public ShoppingListActivityMonitorService(IServiceScopeFactory scopeFactory, FamilyPushNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
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

        await ProcessListEventsAsync(context, since, now, cancellationToken);
        await ProcessItemActivitiesAsync(context, since, now, cancellationToken);
    }

    /// <summary>
    /// Sends an immediate notification for each shopping list created/deleted since the last run.
    /// The activity row already carries the family id and the list name, so no list lookup is needed
    /// (this also works for lists that have since been deleted).
    /// </summary>
    private async Task ProcessListEventsAsync(HomassyDbContext context, DateTime since, DateTime now, CancellationToken cancellationToken)
    {
        var listEvents = await context.Activities
            .Where(a => (a.ActivityType == ActivityType.ShoppingListCreate || a.ActivityType == ActivityType.ShoppingListDelete)
                && a.FamilyId != null
                && a.Timestamp >= since
                && a.Timestamp < now)
            .Select(a => new { a.UserId, FamilyId = a.FamilyId!.Value, a.ActivityType, a.RecordName })
            .ToListAsync(cancellationToken);

        foreach (var ev in listEvents)
        {
            try
            {
                var recipients = await _notifier.GetRecipientsAsync(context, ev.FamilyId, new HashSet<int> { ev.UserId }, cancellationToken);
                if (recipients.Count == 0)
                    continue;

                var created = ev.ActivityType == ActivityType.ShoppingListCreate;
                await _notifier.DispatchAsync(context, recipients, language =>
                {
                    var content = created
                        ? PushNotificationContentService.GetShoppingListCreatedContent(language, ev.RecordName)
                        : PushNotificationContentService.GetShoppingListDeletedContent(language, ev.RecordName);
                    return new[] { content };
                }, "/shopping-lists", cancellationToken);

                Log.Information(
                    "Shopping list {EventType} notification sent to {Count} users for '{ListName}'",
                    ev.ActivityType, recipients.Count, ev.RecordName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending shopping list {EventType} notification for '{ListName}'", ev.ActivityType, ev.RecordName);
            }
        }
    }

    /// <summary>
    /// Accumulates item add/edit/delete/purchase activities into per-list sessions and dispatches
    /// aggregated notifications once a session has been idle for at least <see cref="_sessionTimeout"/>.
    /// </summary>
    private async Task ProcessItemActivitiesAsync(HomassyDbContext context, DateTime since, DateTime now, CancellationToken cancellationToken)
    {
        var activityData = await context.Activities
            .Where(a => TrackedItemActivities.Contains(a.ActivityType)
                && a.FamilyId != null
                && a.Timestamp >= since
                && a.Timestamp < now)
            .Select(a => new { a.UserId, a.Timestamp, ItemId = a.RecordId, a.ActivityType })
            .ToListAsync(cancellationToken);

        if (activityData.Count > 0)
        {
            // Resolve ShoppingListId for each activity (via ShoppingListItem).
            // IgnoreQueryFilters so soft-deleted items (from delete activities) still resolve.
            var itemIds = activityData.Select(a => a.ItemId).Distinct().ToList();
            var itemToListId = await context.ShoppingListItems
                .IgnoreQueryFilters()
                .Where(sli => itemIds.Contains(sli.Id))
                .Select(sli => new { sli.Id, sli.ShoppingListId })
                .ToDictionaryAsync(sli => sli.Id, sli => sli.ShoppingListId, cancellationToken);

            // Load shopping list metadata (including soft-deleted lists, so a just-deleted list can still be named).
            var listIds = itemToListId.Values.Distinct().ToList();
            var listInfo = await context.ShoppingLists
                .IgnoreQueryFilters()
                .Where(sl => listIds.Contains(sl.Id) && sl.FamilyId != null)
                .Select(sl => new { sl.Id, sl.Name, sl.FamilyId })
                .ToDictionaryAsync(sl => sl.Id, sl => sl, cancellationToken);

            // Identify families with at least 2 active members.
            var familyIds = listInfo.Values.Select(sl => sl.FamilyId!.Value).Distinct().ToList();
            var eligibleFamilyIds = await context.Users
                .Where(u => u.FamilyId != null && familyIds.Contains(u.FamilyId!.Value) && !u.IsDeleted)
                .GroupBy(u => u.FamilyId!.Value)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken);

            var eligibleFamilySet = new HashSet<int>(eligibleFamilyIds);

            // Update in-memory sessions for eligible activities.
            foreach (var activity in activityData)
            {
                if (!itemToListId.TryGetValue(activity.ItemId, out var shoppingListId))
                    continue;

                if (!listInfo.TryGetValue(shoppingListId, out var list))
                    continue;

                if (!eligibleFamilySet.Contains(list.FamilyId!.Value))
                    continue;

                if (!_activeSessions.TryGetValue(shoppingListId, out var session))
                {
                    session = new ShoppingListSession
                    {
                        ShoppingListId = shoppingListId,
                        FamilyId = list.FamilyId!.Value,
                        ShoppingListName = list.Name,
                        LastActivityAt = activity.Timestamp
                    };
                    _activeSessions[shoppingListId] = session;
                }

                session.Record(activity.ActivityType, activity.Timestamp, activity.UserId);
            }
        }

        // Find sessions with no new activity for >= sessionTimeout and notify.
        var staleSessions = _activeSessions.Values
            .Where(s => s.LastActivityAt < now - _sessionTimeout)
            .ToList();

        foreach (var session in staleSessions)
        {
            try
            {
                await SendSessionNotificationsAsync(context, session, cancellationToken);
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

    private async Task SendSessionNotificationsAsync(
        HomassyDbContext context,
        ShoppingListSession session,
        CancellationToken cancellationToken)
    {
        var recipients = await _notifier.GetRecipientsAsync(context, session.FamilyId, session.ContributingUserIds, cancellationToken);

        if (recipients.Count == 0)
        {
            Log.Information(
                "Shopping list '{ListName}' session closed – no eligible recipients (list {ShoppingListId})",
                session.ShoppingListName, session.ShoppingListId);
            return;
        }

        await _notifier.DispatchAsync(context, recipients,
            language => BuildItemNotifications(session, language),
            "/shopping-lists", cancellationToken);

        Log.Information(
            "Shopping list '{ListName}' notifications sent to {Count} users (added {Added}, edited {Edited}, deleted {Deleted}, purchased {Purchased}, list {ShoppingListId})",
            session.ShoppingListName, recipients.Count,
            session.AddedCount, session.EditedCount, session.DeletedCount, session.PurchasedCount,
            session.ShoppingListId);
    }

    private static IReadOnlyList<(string Title, string Body)> BuildItemNotifications(
        ShoppingListSession session, Language language)
    {
        var notifications = new List<(string Title, string Body)>(4);

        if (session.AddedCount > 0)
            notifications.Add(PushNotificationContentService.GetShoppingListItemsAddedContent(
                language, session.ShoppingListName, session.AddedCount));

        if (session.EditedCount > 0)
            notifications.Add(PushNotificationContentService.GetShoppingListItemsEditedContent(
                language, session.ShoppingListName, session.EditedCount));

        if (session.DeletedCount > 0)
            notifications.Add(PushNotificationContentService.GetShoppingListItemsDeletedContent(
                language, session.ShoppingListName, session.DeletedCount));

        if (session.PurchasedCount > 0)
            notifications.Add(PushNotificationContentService.GetShoppingListItemsPurchasedContent(
                language, session.ShoppingListName, session.PurchasedCount));

        return notifications;
    }
}

internal sealed class ShoppingListSession
{
    public int ShoppingListId { get; init; }
    public int FamilyId { get; init; }
    public string ShoppingListName { get; set; } = string.Empty;
    public DateTime LastActivityAt { get; set; }
    public HashSet<int> ContributingUserIds { get; } = new();
    public int AddedCount { get; private set; }
    public int EditedCount { get; private set; }
    public int DeletedCount { get; private set; }
    public int PurchasedCount { get; private set; }

    public void Record(ActivityType activityType, DateTime timestamp, int userId)
    {
        ContributingUserIds.Add(userId);

        if (timestamp > LastActivityAt)
            LastActivityAt = timestamp;

        switch (activityType)
        {
            case ActivityType.ShoppingListItemAdd:
                AddedCount++;
                break;
            case ActivityType.ShoppingListItemUpdate:
                EditedCount++;
                break;
            case ActivityType.ShoppingListItemDelete:
                DeletedCount++;
                break;
            case ActivityType.ShoppingListItemPurchase:
            case ActivityType.ShoppingListItemQuickPurchase:
                PurchasedCount++;
                break;
        }
    }
}
