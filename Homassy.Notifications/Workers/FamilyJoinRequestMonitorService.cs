using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Background service that turns family join-request activity into push notifications:
/// <list type="bullet">
/// <item>When a user requests to join a family, every existing member is notified.</item>
/// <item>When a member approves or declines a request, the requester is notified.</item>
/// </list>
/// Join requests are time-sensitive, so this worker polls more frequently than the activity
/// aggregation monitors.
/// </summary>
public sealed class FamilyJoinRequestMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    private DateTime _lastRun;

    public FamilyJoinRequestMonitorService(IServiceScopeFactory scopeFactory, FamilyPushNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _lastRun = DateTime.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Family join request monitor service started");

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
                Log.Error(ex, "Error in family join request monitor service");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Family join request monitor service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = _lastRun;
        _lastRun = now;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        await ProcessNewRequestsAsync(context, since, now, cancellationToken);
        await ProcessDecisionsAsync(context, since, now, cancellationToken);
    }

    /// <summary>
    /// Notifies every member of the target family when a new join request is created. The actor
    /// (the requester) is excluded; the activity carries the requester's display name in RecordName.
    /// </summary>
    private async Task ProcessNewRequestsAsync(HomassyDbContext context, DateTime since, DateTime now, CancellationToken cancellationToken)
    {
        var events = await context.Activities
            .Where(a => a.ActivityType == ActivityType.FamilyJoinRequestCreate
                && a.FamilyId != null
                && a.Timestamp >= since
                && a.Timestamp < now)
            .Select(a => new { FamilyId = a.FamilyId!.Value, RequesterName = a.RecordName, a.UserId })
            .ToListAsync(cancellationToken);

        foreach (var ev in events)
        {
            try
            {
                var recipients = await _notifier.GetRecipientsAsync(context, ev.FamilyId, new HashSet<int> { ev.UserId }, cancellationToken);
                if (recipients.Count == 0)
                    continue;

                await _notifier.DispatchAsync(context, recipients,
                    language => new[] { PushNotificationContentService.GetFamilyJoinRequestContent(language, ev.RequesterName) },
                    "/profile/family", cancellationToken);

                Log.Information("Join request notification sent to {Count} members of family {FamilyId}", recipients.Count, ev.FamilyId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending join request notification for family {FamilyId}", ev.FamilyId);
            }
        }
    }

    /// <summary>
    /// Notifies the requester when a member approves or declines their request. The activity's
    /// RecordId is the join request id, which we resolve to the requester and the family.
    /// </summary>
    private async Task ProcessDecisionsAsync(HomassyDbContext context, DateTime since, DateTime now, CancellationToken cancellationToken)
    {
        var events = await context.Activities
            .Where(a => (a.ActivityType == ActivityType.FamilyJoinRequestApprove || a.ActivityType == ActivityType.FamilyJoinRequestDecline)
                && a.Timestamp >= since
                && a.Timestamp < now)
            .Select(a => new { a.ActivityType, RequestId = a.RecordId })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
            return;

        var requestIds = events.Select(e => e.RequestId).Distinct().ToList();
        var requests = await context.FamilyJoinRequests
            .IgnoreQueryFilters()
            .Where(r => requestIds.Contains(r.Id))
            .Select(r => new { r.Id, r.UserId, r.FamilyId })
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var familyIds = requests.Values.Select(r => r.FamilyId).Distinct().ToList();
        var familyNames = await context.Families
            .IgnoreQueryFilters()
            .Where(f => familyIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);

        foreach (var ev in events)
        {
            try
            {
                if (!requests.TryGetValue(ev.RequestId, out var request))
                    continue;

                if (await _notifier.GetRecipientAsync(context, request.UserId, cancellationToken) is not { } recipient)
                    continue;

                var familyName = familyNames.GetValueOrDefault(request.FamilyId, string.Empty);
                var approved = ev.ActivityType == ActivityType.FamilyJoinRequestApprove;

                await _notifier.DispatchAsync(context, new[] { recipient },
                    language => new[]
                    {
                        approved
                            ? PushNotificationContentService.GetFamilyJoinApprovedContent(language, familyName)
                            : PushNotificationContentService.GetFamilyJoinDeclinedContent(language, familyName)
                    },
                    "/profile/family", cancellationToken);

                Log.Information("Join request {Decision} notification sent to user {UserId}",
                    approved ? "approval" : "decline", request.UserId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending join request decision notification for request {RequestId}", ev.RequestId);
            }
        }
    }
}
