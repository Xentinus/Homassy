using Homassy.API.Context;
using Homassy.API.Entities.User;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    public class PushNotificationFunctions
    {
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public PushNotificationFunctions(IDbContextFactory<HomassyDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<UserPushSubscription> SubscribeAsync(string endpoint, string p256dh, string auth, string? userAgent, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            using var context = _contextFactory.CreateDbContext();

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

            using var context = _contextFactory.CreateDbContext();
            var subscription = await context.UserPushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId.Value, cancellationToken);

            if (subscription != null)
            {
                subscription.DeleteRecord(userId.Value);
                await context.SaveChangesAsync(cancellationToken);
                Log.Information("Push subscription removed for user {UserId}", userId.Value);
            }
        }
    }
}