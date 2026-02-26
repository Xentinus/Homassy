using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Models;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Endpoints;

public static class TestPushEndpoint
{
    public static async Task<IResult> HandleAsync(
        TestPushRequest request,
        HomassyDbContext context,
        IWebPushService webPushService,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.UserId && !u.IsDeleted)
            .Select(u => new { u.Id, Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Results.NotFound($"User {request.UserId} not found.");

        var subscriptions = await context.UserPushSubscriptions
            .Where(s => s.UserId == request.UserId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
            return Results.Problem("No active push subscription found for user.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var (title, body, actionTitle) = PushNotificationContentService.GetTestNotificationContent(user.Language);
        var anySent = false;
        var hasChanges = false;

        foreach (var subscription in subscriptions)
        {
            var success = await webPushService.SendNotificationAsync(
                subscription, title, body, "/", actionTitle, cancellationToken);

            if (success)
            {
                anySent = true;
            }
            else
            {
                subscription.DeleteRecord(request.UserId);
                hasChanges = true;
            }
        }

        if (hasChanges)
            await context.SaveChangesAsync(cancellationToken);

        if (!anySent)
            return Results.Problem("No valid push subscription found. Please re-enable push notifications.", statusCode: StatusCodes.Status422UnprocessableEntity);

        Log.Information("Test push notification sent to user {UserId}", request.UserId);
        return Results.Ok();
    }
}
