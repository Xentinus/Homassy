using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Enums;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Notifies family members about chat messages they have not seen (#149).
///
/// The rule is simply: if the recipient is not actively watching the chat, they get a
/// notification. Everything below is what that costs to do without being annoying.
///
/// <para><b>Bursts are held, not sent.</b> Messages are accumulated per (family, sender) and only
/// dispatched once that sender has been quiet for the grace window. That gives one push saying
/// "3 new messages" instead of three pushes, and it is also what keeps the phone quiet when you
/// open the chat two seconds after a message lands - by the time the burst flushes, you are
/// active and the notification is dropped rather than cancelled.</para>
///
/// <para><b>Eligibility is decided at flush time, never at arrival.</b> A recipient is skipped if
/// they sent it, if any of their devices is actively watching, if their read marker has already
/// passed the message, or if they muted chat notifications. All four can change during the grace
/// window, which is the point of deciding late.</para>
///
/// <para><b>The watermark is in memory</b>, like the other activity monitors here: it starts at
/// "now" on boot, so a restart forgets pending bursts rather than replaying an evening of
/// messages into everybody's lock screen. The cost is that a restart mid-burst drops that
/// notification - the messages themselves are safe in the database, and the unread badge still
/// shows them.</para>
/// </summary>
public sealed class FamilyChatNotificationService : BackgroundService
{
    /// <summary>How often new messages are collected and ripe bursts flushed.</summary>
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How quiet a sender has to go before their burst is sent.
    /// </summary>
    /// <remarks>
    /// Short enough that a message you are not there for reaches you promptly, long enough to
    /// collapse a run of "wait" / "actually" / "never mind" into one notification - and long enough
    /// that opening the chat right after a message arrives suppresses it entirely.
    /// </remarks>
    private static readonly TimeSpan GraceWindow = TimeSpan.FromSeconds(15);

    /// <summary>Longest message preview a notification body carries.</summary>
    private const int PreviewLength = 120;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;
    private readonly FamilyChatActivityClient _activityClient;

    /// <summary>Pending bursts, keyed by (family, sender). In memory - see the class remarks.</summary>
    private readonly Dictionary<(int FamilyId, int SenderUserId), PendingBurst> _pending = [];

    private DateTime _watermark;

    public FamilyChatNotificationService(
        IServiceScopeFactory scopeFactory,
        FamilyPushNotifier notifier,
        FamilyChatActivityClient activityClient)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _activityClient = activityClient;
        _watermark = DateTime.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Family chat notification service started");

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
                Log.Error(ex, "Error in family chat notification service");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Family chat notification service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        await CollectAsync(context, now, cancellationToken);
        await FlushRipeAsync(context, now, cancellationToken);
    }

    /// <summary>Folds messages sent since the last tick into their sender's pending burst.</summary>
    private async Task CollectAsync(HomassyDbContext context, DateTime now, CancellationToken cancellationToken)
    {
        var since = _watermark;
        _watermark = now;

        var messages = await context.Set<FamilyChatMessage>()
            .AsNoTracking()
            .Where(m => m.SentAt >= since && m.SentAt < now)
            .OrderBy(m => m.SentAt)
            .Select(m => new { m.FamilyId, m.SenderUserId, m.Kind, m.Body, m.SentAt })
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            var key = (message.FamilyId, message.SenderUserId);

            if (!_pending.TryGetValue(key, out var burst))
            {
                burst = new PendingBurst();
                _pending[key] = burst;
            }

            burst.Count++;
            burst.LastSentAt = message.SentAt;
            // The newest message is the one previewed - it is what a reader opening the chat will
            // see at the bottom, so it is the one the notification should be about.
            burst.LastPreview = message.Kind == FamilyChatMessageKind.Image
                ? string.Empty
                : Truncate(message.Body);
        }
    }

    /// <summary>Sends every burst whose sender has been quiet for the grace window.</summary>
    private async Task FlushRipeAsync(HomassyDbContext context, DateTime now, CancellationToken cancellationToken)
    {
        var ripe = _pending
            .Where(entry => now - entry.Value.LastSentAt >= GraceWindow)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var key in ripe)
        {
            var burst = _pending[key];
            _pending.Remove(key);

            try
            {
                await NotifyAsync(context, key.FamilyId, key.SenderUserId, burst, cancellationToken);
            }
            catch (Exception ex)
            {
                // One family's notification failing must not hold up the others, and the burst is
                // already out of the map - a retry would re-notify about messages that may since
                // have been read.
                Log.Error(ex, "Failed to notify family {FamilyId} about chat messages", key.FamilyId);
            }
        }
    }

    private async Task NotifyAsync(
        HomassyDbContext context,
        int familyId,
        int senderUserId,
        PendingBurst burst,
        CancellationToken cancellationToken)
    {
        var recipients = await _notifier.GetRecipientsAsync(context, familyId, [senderUserId], cancellationToken);
        if (recipients.Count == 0) return;

        var senderName = await GetDisplayNameAsync(context, senderUserId, cancellationToken);

        // Muted chat notifications: the flag is chat-specific on purpose, so a family that talks
        // all evening can silence it without losing expiration alerts.
        var mutedUserIds = await context.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => !p.PushFamilyChatEnabled)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var muted = mutedUserIds.ToHashSet();

        // Already read elsewhere - on another device, or in the app while the burst was being
        // held. The marker is what makes "they have seen it" knowable without asking the client.
        var readMarkers = await context.Set<FamilyChatReadState>()
            .AsNoTracking()
            .Where(r => r.FamilyId == familyId)
            .Select(r => new { r.UserId, r.LastReadAt })
            .ToListAsync(cancellationToken);

        var readAt = readMarkers.ToDictionary(r => r.UserId, r => r.LastReadAt);

        var candidates = recipients
            .Where(r => !muted.Contains(r.Id))
            .Where(r => !readAt.TryGetValue(r.Id, out var marker) || marker < burst.LastSentAt)
            .ToList();

        if (candidates.Count == 0) return;

        // Asked last, and about this exact set: it is the only check that costs a network call,
        // and it is the one most likely to have changed during the grace window.
        var active = await _activityClient.GetActiveUserIdsAsync(
            candidates.Select(c => c.Id).ToList(), cancellationToken);

        var targets = candidates.Where(c => !active.Contains(c.Id)).ToList();
        if (targets.Count == 0)
        {
            Log.Debug("Chat burst in family {FamilyId} notified nobody - every recipient was active or had read it", familyId);
            return;
        }

        var envelope = NotificationEnvelopes.FamilyChatMessages(senderName, burst.Count, burst.LastPreview);

        // What each target's app icon should say once this push lands. Computed per recipient and
        // read from the database, never derived from `burst.Count`: the burst is what this sender
        // just wrote, while the badge is everything still waiting for that reader - including
        // messages from somebody else and the shopping list's own deadlines.
        var badgeCounts = new Dictionary<int, int>(targets.Count);
        foreach (var target in targets)
        {
            badgeCounts[target.Id] = await AppBadgeCount.ForUserAsync(context, target.Id, familyId, cancellationToken);
        }

        // The chat is a panel, not a route, so the link lands on a real page and asks the layout
        // to open the panel over it. `/calendar` is where an authenticated session lands anyway;
        // `/` would redirect there and drop the query on the way.
        await _notifier.DispatchAsync(
            context, targets, [envelope], "/calendar?action=open-chat", cancellationToken, badgeCounts);

        Log.Information(
            "Notified {Count} member(s) of family {FamilyId} about {Messages} chat message(s)",
            targets.Count, familyId, burst.Count);
    }

    private static async Task<string> GetDisplayNameAsync(HomassyDbContext context, int userId, CancellationToken cancellationToken)
    {
        var name = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Name, DisplayName = u.Profile != null ? u.Profile.DisplayName : null })
            .FirstOrDefaultAsync(cancellationToken);

        if (name == null) return string.Empty;
        return string.IsNullOrWhiteSpace(name.DisplayName) ? name.Name : name.DisplayName!;
    }

    private static string Truncate(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        var trimmed = body.Trim();
        return trimmed.Length <= PreviewLength ? trimmed : trimmed[..PreviewLength] + "…";
    }

    /// <summary>One sender's un-notified messages in one family, waiting for them to stop typing.</summary>
    private sealed class PendingBurst
    {
        public int Count { get; set; }
        public DateTime LastSentAt { get; set; }
        public string LastPreview { get; set; } = string.Empty;
    }
}
