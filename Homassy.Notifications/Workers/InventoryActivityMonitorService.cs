using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Background service that monitors family product inventory (készlet) for changes and notifies the
/// other family members. Inventory create/update/delete/consume activities are accumulated into an
/// in-memory session per family; once no further inventory activity occurs for the session timeout,
/// one notification per non-zero action type (created / updated / deleted / consumed) is sent to all
/// family members who did NOT contribute to that session, each carrying the count.
///
/// Inventory has no "list" container, so sessions are keyed by FamilyId. Notifications are count-only
/// (no product names). Families with no eligible recipient (e.g. single-member families) receive
/// nothing. Sessions are held purely in memory and cleared after notifications are dispatched.
/// </summary>
public sealed class InventoryActivityMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(5);

    private static readonly ActivityType[] TrackedInventoryActivities =
    {
        ActivityType.ProductInventoryCreate,
        ActivityType.ProductInventoryUpdate,
        ActivityType.ProductInventoryDelete,
        ActivityType.ProductInventoryDecrease
    };

    // Key: FamilyId
    private readonly Dictionary<int, InventorySession> _activeSessions = new();
    private DateTime _lastRun;

    public InventoryActivityMonitorService(IServiceScopeFactory scopeFactory, FamilyPushNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _lastRun = DateTime.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Inventory activity monitor service started");

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
                Log.Error(ex, "Error in inventory activity monitor service");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Inventory activity monitor service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = _lastRun;
        _lastRun = now;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var activityData = await context.Activities
            .Where(a => TrackedInventoryActivities.Contains(a.ActivityType)
                && a.FamilyId != null
                && a.Timestamp >= since
                && a.Timestamp < now)
            .Select(a => new { a.UserId, FamilyId = a.FamilyId!.Value, a.Timestamp, a.ActivityType })
            .ToListAsync(cancellationToken);

        foreach (var activity in activityData)
        {
            if (!_activeSessions.TryGetValue(activity.FamilyId, out var session))
            {
                session = new InventorySession
                {
                    FamilyId = activity.FamilyId,
                    LastActivityAt = activity.Timestamp
                };
                _activeSessions[activity.FamilyId] = session;
            }

            session.Record(activity.ActivityType, activity.Timestamp, activity.UserId);
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
                Log.Error(ex, "Error sending inventory notifications for family {FamilyId}", session.FamilyId);
            }
            finally
            {
                _activeSessions.Remove(session.FamilyId);
            }
        }

        if (staleSessions.Count > 0 || activityData.Count > 0)
        {
            Log.Information(
                "Inventory monitor: {NewActivities} new activities processed, {Stale} sessions notified, {Active} active sessions remaining",
                activityData.Count, staleSessions.Count, _activeSessions.Count);
        }
    }

    private async Task SendSessionNotificationsAsync(
        HomassyDbContext context,
        InventorySession session,
        CancellationToken cancellationToken)
    {
        var recipients = await _notifier.GetRecipientsAsync(context, session.FamilyId, session.ContributingUserIds, cancellationToken);

        if (recipients.Count == 0)
        {
            Log.Information(
                "Inventory session closed – no eligible recipients (family {FamilyId})",
                session.FamilyId);
            return;
        }

        await _notifier.DispatchAsync(context, recipients,
            language => BuildInventoryNotifications(session, language),
            "/products", cancellationToken);

        Log.Information(
            "Inventory notifications sent to {Count} users (created {Created}, updated {Updated}, deleted {Deleted}, consumed {Consumed}, family {FamilyId})",
            recipients.Count,
            session.CreatedCount, session.UpdatedCount, session.DeletedCount, session.ConsumedCount,
            session.FamilyId);
    }

    private static IReadOnlyList<(string Title, string Body)> BuildInventoryNotifications(
        InventorySession session, Language language)
    {
        var notifications = new List<(string Title, string Body)>(4);

        if (session.CreatedCount > 0)
            notifications.Add(PushNotificationContentService.GetInventoryItemsCreatedContent(language, session.CreatedCount));

        if (session.UpdatedCount > 0)
            notifications.Add(PushNotificationContentService.GetInventoryItemsUpdatedContent(language, session.UpdatedCount));

        if (session.DeletedCount > 0)
            notifications.Add(PushNotificationContentService.GetInventoryItemsDeletedContent(language, session.DeletedCount));

        if (session.ConsumedCount > 0)
            notifications.Add(PushNotificationContentService.GetInventoryItemsConsumedContent(language, session.ConsumedCount));

        return notifications;
    }
}

internal sealed class InventorySession
{
    public int FamilyId { get; init; }
    public DateTime LastActivityAt { get; set; }
    public HashSet<int> ContributingUserIds { get; } = new();
    public int CreatedCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public int DeletedCount { get; private set; }
    public int ConsumedCount { get; private set; }

    public void Record(ActivityType activityType, DateTime timestamp, int userId)
    {
        ContributingUserIds.Add(userId);

        if (timestamp > LastActivityAt)
            LastActivityAt = timestamp;

        switch (activityType)
        {
            case ActivityType.ProductInventoryCreate:
                CreatedCount++;
                break;
            case ActivityType.ProductInventoryUpdate:
                UpdatedCount++;
                break;
            case ActivityType.ProductInventoryDelete:
                DeletedCount++;
                break;
            case ActivityType.ProductInventoryDecrease:
                ConsumedCount++;
                break;
        }
    }
}
