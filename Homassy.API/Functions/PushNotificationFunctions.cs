using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    public class PushNotificationFunctions
    {
        private const int EXPIRING_SOON_THRESHOLD_DAYS = 14;

        public async Task<UserPushSubscription> SubscribeAsync(string endpoint, string p256dh, string auth, string? userAgent, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var context = new HomassyDbContext();

            // Check if subscription already exists (upsert)
            var existing = await context.UserPushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint, cancellationToken);

            if (existing != null)
            {
                // Update existing subscription
                existing.P256dh = p256dh;
                existing.Auth = auth;
                existing.UserAgent = userAgent;
                existing.UserId = userId.Value;
                existing.IsDeleted = false;
                await context.SaveChangesAsync(cancellationToken);
                return existing;
            }

            var subscription = new UserPushSubscription
            {
                UserId = userId.Value,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent
            };

            context.UserPushSubscriptions.Add(subscription);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information("Push subscription created for user {UserId}", userId.Value);
            return subscription;
        }

        public async Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var context = new HomassyDbContext();
            var subscription = await context.UserPushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId.Value, cancellationToken);

            if (subscription != null)
            {
                subscription.DeleteRecord(userId.Value);
                await context.SaveChangesAsync(cancellationToken);
                Log.Information("Push subscription removed for user {UserId}", userId.Value);
            }
        }

        public async Task SendTestNotificationAsync(IWebPushService webPushService, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var language = SessionInfo.GetLanguage();
            var (title, body, actionTitle) = GetTestNotificationContent(language);

            var context = new HomassyDbContext();
            var subscriptions = await context.UserPushSubscriptions
                .Where(s => s.UserId == userId.Value && !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            if (subscriptions.Count == 0)
            {
                throw new InvalidOperationException("No active push subscription found for user");
            }

            var anySent = false;

            foreach (var subscription in subscriptions)
            {
                var success = await webPushService.SendNotificationAsync(
                    subscription,
                    title,
                    body,
                    "/",
                    actionTitle,
                    cancellationToken);

                if (success)
                {
                    anySent = true;
                }
                else
                {
                    // Subscription is no longer valid, clean it up so the user will re-subscribe
                    subscription.DeleteRecord(userId.Value);
                }
            }

            if (!anySent)
            {
                await context.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("No valid push subscription found. Please re-enable push notifications.");
            }

            await context.SaveChangesAsync(cancellationToken);
            Log.Information("Test push notification sent to user {UserId}", userId.Value);
        }

        private static (string Title, string Body, string ActionTitle) GetTestNotificationContent(Language language)
        {
            return language switch
            {
                Language.German => ("Homassy Testbenachrichtigung", "Dies ist eine Test-Push-Benachrichtigung von Homassy.", "Homassy öffnen"),
                Language.English => ("Homassy test notification", "This is a test push notification from Homassy.", "Open Homassy"),
                _ => ("Homassy teszt értesítés", "Ez egy teszt push értesítés a Homassytól.", "Homassy megnyitása")
            };
        }

        public static async Task<int> GetExpiringCountForUserAsync(int userId, int? familyId, HomassyDbContext context, CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;
            var expiringThreshold = today.AddDays(EXPIRING_SOON_THRESHOLD_DAYS);

            var query = context.ProductInventoryItems
                .AsNoTracking()
                .Where(i => !i.IsFullyConsumed && i.ExpirationAt.HasValue);

            if (familyId.HasValue)
            {
                query = query.Where(i => i.UserId == userId || i.FamilyId == familyId);
            }
            else
            {
                query = query.Where(i => i.UserId == userId);
            }

            return await query.CountAsync(i => i.ExpirationAt!.Value.Date <= expiringThreshold, cancellationToken);
        }
    }
}
