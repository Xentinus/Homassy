using Homassy.API.Entities.User;

namespace Homassy.API.Services
{
    public interface IWebPushService
    {
        Task<bool> SendNotificationAsync(
            UserPushSubscription subscription,
            string title,
            string body,
            string? url = null,
            string? actionTitle = null,
            CancellationToken cancellationToken = default);
        string GetVapidPublicKey();
    }
}
