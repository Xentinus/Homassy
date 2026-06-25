using Homassy.API.Context;
using Homassy.API.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Services;

/// <summary>
/// Shared helper for the family activity monitors. Resolves the family members eligible to receive a
/// push notification and dispatches one or more localized notifications to them, cleaning up any
/// subscription the push service reports as invalid.
/// </summary>
public sealed class FamilyPushNotifier
{
    private readonly IWebPushService _webPushService;

    public FamilyPushNotifier(IWebPushService webPushService)
    {
        _webPushService = webPushService;
    }

    /// <summary>
    /// Resolves the family members eligible to receive a notification: active members of the family
    /// who have push notifications enabled and at least one active subscription, excluding the users
    /// who performed the change.
    /// </summary>
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
                && u.NotificationPreferences != null
                && u.NotificationPreferences.PushNotificationsEnabled)
            .Where(u => context.UserPushSubscriptions.Any(s => s.UserId == u.Id && !s.IsDeleted))
            .Select(u => new RecipientInfo(
                u.Id,
                u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sends one or more notifications (built per recipient language) to every recipient, cleaning up
    /// any subscription the push service reports as invalid.
    /// </summary>
    public async Task DispatchAsync(
        HomassyDbContext context,
        IReadOnlyList<RecipientInfo> recipients,
        Func<Language, IReadOnlyList<(string Title, string Body)>> contentFactory,
        string url,
        CancellationToken cancellationToken)
    {
        var hasChanges = false;

        foreach (var recipient in recipients)
        {
            var notifications = contentFactory(recipient.Language);
            if (notifications.Count == 0)
                continue;

            var actionTitle = GetActionTitle(recipient.Language);

            var subscriptions = await context.UserPushSubscriptions
                .Where(s => s.UserId == recipient.Id && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                foreach (var (title, body) in notifications)
                {
                    var success = await _webPushService.SendNotificationAsync(
                        subscription, title, body, url, actionTitle, cancellationToken);

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

public readonly record struct RecipientInfo(int Id, Language Language);
