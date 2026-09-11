using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Notification;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Services;

/// <summary>
/// Shared delivery helper for the notification workers. Resolves who should receive a
/// notification and delivers it on both channels: a Web Push to each of their devices, and a
/// <c>UserNotification</c> row for the in-app notification centre (#116).
/// </summary>
/// <remarks>
/// Both channels are driven from the same <see cref="NotificationEnvelope"/> list, in the same
/// method, in the same unit of work. That is the point: before #116 the workers rendered
/// <c>(title, body)</c> pairs and handed them straight to the push service, so anything not
/// delivered as a push simply did not exist - miss the push and the information was gone. Adding
/// the record anywhere other than here would have been a second code path to keep in step, and
/// the two would have drifted the first time a worker changed.
/// </remarks>
public sealed class FamilyPushNotifier
{
    private readonly IWebPushService _webPushService;

    public FamilyPushNotifier(IWebPushService webPushService)
    {
        _webPushService = webPushService;
    }

    /// <summary>
    /// Resolves the family members eligible to receive a notification on <em>either</em> channel:
    /// active members of the family, excluding the users who performed the change, who have push
    /// enabled with at least one live subscription, or who have the in-app notification centre
    /// enabled.
    /// </summary>
    /// <remarks>
    /// The "or" is what #116 changed. This used to require push enabled <em>and</em> a live
    /// subscription, which meant a member who had turned push off was not a recipient at all - so
    /// giving them an inbox while leaving that filter in place would have produced a permanently
    /// empty one. Each returned recipient carries which channels apply to them, and
    /// <see cref="DispatchAsync"/> honours that per recipient.
    /// </remarks>
    public async Task<List<RecipientInfo>> GetRecipientsAsync(
        HomassyDbContext context,
        int familyId,
        IReadOnlyCollection<int> excludeUserIds,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(u => u.FamilyId == familyId
                && !u.IsDeleted
                && !excludeUserIds.Contains(u.Id)
                && u.NotificationPreferences != null)
            .Select(u => new
            {
                u.Id,
                Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian,
                TimeZone = u.Profile != null ? u.Profile.DefaultTimeZone : UserTimeZone.CentralEuropeStandardTime,
                PushEnabled = u.NotificationPreferences!.PushNotificationsEnabled
                    && context.UserPushSubscriptions.Any(s => s.UserId == u.Id && !s.IsDeleted),
                InAppEnabled = u.NotificationPreferences.InAppNotificationsEnabled
            })
            .Where(u => u.PushEnabled || u.InAppEnabled)
            .Select(u => new RecipientInfo(u.Id, u.Language, u.TimeZone, u.PushEnabled, u.InAppEnabled))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves a single user as a notification recipient, or null when neither channel applies to
    /// them. Used to notify the requester of a join request decision.
    /// </summary>
    public async Task<RecipientInfo?> GetRecipientAsync(
        HomassyDbContext context,
        int userId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted && u.NotificationPreferences != null)
            .Select(u => new
            {
                u.Id,
                Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian,
                TimeZone = u.Profile != null ? u.Profile.DefaultTimeZone : UserTimeZone.CentralEuropeStandardTime,
                PushEnabled = u.NotificationPreferences!.PushNotificationsEnabled
                    && context.UserPushSubscriptions.Any(s => s.UserId == u.Id && !s.IsDeleted),
                InAppEnabled = u.NotificationPreferences.InAppNotificationsEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null || (!user.PushEnabled && !user.InAppEnabled))
            return null;

        return new RecipientInfo(user.Id, user.Language, user.TimeZone, user.PushEnabled, user.InAppEnabled);
    }

    /// <summary>
    /// Delivers <paramref name="notifications"/> to every recipient on whichever channels apply to
    /// them: a push per device (rendered in the recipient's own language) and an inbox row per
    /// notification. Also cleans up any subscription the push service reports as invalid.
    /// </summary>
    /// <remarks>
    /// One <c>SaveChangesAsync</c> for everything this call writes - the inbox rows and the
    /// subscription cleanup - so a batch cannot half-commit.
    /// <para>
    /// Inbox rows are added <em>before</em> the pushes are attempted, but committed after. That
    /// ordering matters: a push send is a network call that can take seconds per device, and rows
    /// added first all share one <see cref="DateTime.UtcNow"/>, so a batch that belongs to a
    /// single event groups under one timestamp in the inbox instead of being smeared across
    /// however long the sends took.
    /// </para>
    /// </remarks>
    /// <param name="badgeCounts">
    /// What each recipient's app icon should show, keyed by user id, or null to leave every
    /// device's badge alone. A caller that knows the number passes it (see <c>AppBadgeCount</c>);
    /// one that does not must not guess, because the service worker writes whatever arrives and an
    /// invented number would sit on the icon until the app is next opened.
    /// </param>
    public async Task DispatchAsync(
        HomassyDbContext context,
        IReadOnlyList<RecipientInfo> recipients,
        IReadOnlyList<NotificationEnvelope> notifications,
        string url,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<int, int>? badgeCounts = null)
    {
        if (recipients.Count == 0 || notifications.Count == 0)
            return;

        var hasChanges = false;

        var inboxRecipients = recipients.Where(r => r.InAppEnabled).Select(r => r.Id).ToList();
        if (inboxRecipients.Count > 0)
        {
            NotificationFunctions.Record(context, inboxRecipients, notifications, url, DateTime.UtcNow);
            hasChanges = true;
        }

        foreach (var recipient in recipients)
        {
            if (!recipient.PushEnabled)
                continue;

            var actionTitle = GetActionTitle(recipient.Language);
            int? badgeCount = badgeCounts != null && badgeCounts.TryGetValue(recipient.Id, out var count)
                ? count
                : null;

            var subscriptions = await context.UserPushSubscriptions
                .Where(s => s.UserId == recipient.Id && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                foreach (var envelope in notifications)
                {
                    var (title, body) = NotificationContentRenderer.Render(envelope, recipient.Language);

                    var success = await _webPushService.SendNotificationAsync(
                        subscription, title, body, url, actionTitle, badgeCount, cancellationToken);

                    if (!success)
                    {
                        subscription.DeleteRecord();
                        hasChanges = true;
                        Log.Information(
                            "Removed invalid push subscription {Endpoint} for user {UserId}",
                            subscription.Endpoint, recipient.Id);
                        break; // subscription is dead – skip its remaining notifications
                    }
                }
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
}

/// <summary>
/// A member eligible for a notification, and on which channels.
/// </summary>
/// <remarks>
/// <paramref name="TimeZone"/> defaults to the app-wide fallback so notifiers that do not schedule
/// anything need not supply one. The two channel flags default to push-only, which is what every
/// caller that constructs a recipient itself (rather than resolving one here) wants: those are
/// paths that already established a live subscription.
/// </remarks>
public readonly record struct RecipientInfo(
    int Id,
    Language Language,
    UserTimeZone TimeZone = UserTimeZone.CentralEuropeStandardTime,
    bool PushEnabled = true,
    bool InAppEnabled = false);
