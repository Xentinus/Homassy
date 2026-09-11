using Homassy.API.Entities.User;

namespace Homassy.Notifications.Services;

public interface IWebPushService
{
    /// <param name="badgeCount">
    /// Optional count for the installed app's icon badge (#130). Sent in the payload for the
    /// service worker to apply, so the icon is right without the app being opened. Only pass it
    /// from a sender that actually knows the number the app itself would show; leaving it null
    /// means "do not touch the badge", which is what every notification that is not about a
    /// countable backlog should do.
    /// </param>
    Task<bool> SendNotificationAsync(
        UserPushSubscription subscription,
        string title,
        string body,
        string? url = null,
        string? actionTitle = null,
        int? badgeCount = null,
        CancellationToken cancellationToken = default);
    string GetVapidPublicKey();
}
