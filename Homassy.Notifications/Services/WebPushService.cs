using Homassy.Data.Entities.User;
using Serilog;
using System.Text.Json;
using WebPush;

namespace Homassy.Notifications.Services;

public sealed class WebPushService : IWebPushService
{
    private readonly WebPushClient _client;
    private readonly string _vapidPublicKey;
    private readonly string _vapidPrivateKey;
    private readonly string _vapidSubject;

    public WebPushService(IConfiguration configuration)
    {
        _vapidPublicKey = configuration["WebPush:VapidPublicKey"] ?? throw new InvalidOperationException("WebPush:VapidPublicKey is not configured");
        _vapidPrivateKey = configuration["WebPush:VapidPrivateKey"] ?? throw new InvalidOperationException("WebPush:VapidPrivateKey is not configured");
        _vapidSubject = configuration["WebPush:VapidSubject"] ?? throw new InvalidOperationException("WebPush:VapidSubject is not configured");

        _client = new WebPushClient();
    }

    public string GetVapidPublicKey() => _vapidPublicKey;

    public async Task<PushSendResult> SendNotificationAsync(
        UserPushSubscription subscription,
        string title,
        string body,
        string? url = null,
        string? actionTitle = null,
        int? badgeCount = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pushSubscription = new PushSubscription(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth);

            var vapidDetails = new VapidDetails(
                _vapidSubject,
                _vapidPublicKey,
                _vapidPrivateKey);

            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                icon = "/apple-touch-icon-180x180.png",
                badge = "/favicon-32x32.png",
                url = url ?? "/",
                actionTitle,
                // `badge` above is the monochrome status-bar glyph; this is the number on the
                // app icon (#130). Absent from the payload unless the sender knows a count, so
                // the service worker can tell "set it to zero" from "leave it alone".
                badgeCount
            });

            await _client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            return PushSendResult.Delivered;
        }
        catch (WebPushException ex)
        {
            var result = Classify(ex);

            if (result == PushSendResult.SubscriptionGone)
                Log.Warning("Push subscription expired/invalid for endpoint: {Endpoint}", subscription.Endpoint);
            else
                Log.Error(ex, "Web Push failed with status {StatusCode} for endpoint: {Endpoint}", ex.StatusCode, subscription.Endpoint);

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error sending push notification to: {Endpoint}", subscription.Endpoint);
            return Classify(ex);
        }
    }

    /// <summary>
    /// Decides what a failed send says about the subscription.
    /// </summary>
    /// <remarks>
    /// Its own method so the rule can be tested without a push service: getting it wrong in either
    /// direction is expensive. Too eager and a live device is unsubscribed over a rate limit; too
    /// cautious and dead endpoints accumulate, each costing a request on every notification.
    /// <para>
    /// 404 and 410 are the two statuses RFC 8030 gives for an endpoint that no longer exists, and
    /// they are the only ones that mean it. Every other status - 429, 5xx - and every non-HTTP
    /// fault is about the moment, not the subscription.
    /// </para>
    /// </remarks>
    public static PushSendResult Classify(Exception exception) =>
        exception is WebPushException { StatusCode: System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound }
            ? PushSendResult.SubscriptionGone
            : PushSendResult.TransientFailure;
}
