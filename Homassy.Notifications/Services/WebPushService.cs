using Homassy.API.Entities.User;
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

    public async Task<bool> SendNotificationAsync(
        UserPushSubscription subscription,
        string title,
        string body,
        string? url = null,
        string? actionTitle = null,
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
                actionTitle
            });

            await _client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                           ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Log.Warning("Push subscription expired/invalid for endpoint: {Endpoint}", subscription.Endpoint);
            return false;
        }
        catch (WebPushException ex)
        {
            Log.Error(ex, "Web Push failed with status {StatusCode} for endpoint: {Endpoint}", ex.StatusCode, subscription.Endpoint);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error sending push notification to: {Endpoint}", subscription.Endpoint);
            return false;
        }
    }
}
