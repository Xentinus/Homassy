using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Models;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Endpoints;

public static class LowStockPushEndpoint
{
    public static async Task<IResult> HandleAsync(
        LowStockPushRequest request,
        HomassyDbContext context,
        IWebPushService webPushService,
        EmailServiceClient emailClient,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.UserId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Email, u.Name, Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian, EmailEnabled = u.NotificationPreferences != null && u.NotificationPreferences.EmailNotificationsEnabled })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Results.NotFound($"User {request.UserId} not found.");

        var subscriptions = await context.UserPushSubscriptions
            .Where(s => s.UserId == request.UserId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count > 0)
        {
            var (title, body) = PushNotificationContentService.GetLowStockNotificationContent(
                user.Language, request.ProductName, request.TotalStock, request.ThresholdQuantity, request.ShoppingListName);

            var actionTitle = user.Language switch
            {
                Language.German => "Homassy öffnen",
                Language.English => "Open Homassy",
                _ => "Homassy megnyitása"
            };

            var hasChanges = false;

            foreach (var subscription in subscriptions)
            {
                var success = await webPushService.SendNotificationAsync(
                    subscription, title, body, "/profile/automation", actionTitle, cancellationToken);

                if (!success)
                {
                    subscription.DeleteRecord(request.UserId);
                    hasChanges = true;
                }
            }

            if (hasChanges)
                await context.SaveChangesAsync(cancellationToken);
        }

        // Send email notification
        try
        {
            if (user.EmailEnabled && !string.IsNullOrWhiteSpace(user.Email))
            {
                await emailClient.SendAutomationNotificationAsync(
                    user.Email, user.Language, user.Name, request.ProductName,
                    "low_stock_add_to_shopping_list", request.AddQuantity, request.Unit, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send low-stock email for user {UserId}, product {Product}", request.UserId, request.ProductName);
        }

        Log.Information("Low-stock notification sent for user {UserId}, product {Product}", request.UserId, request.ProductName);
        return Results.Ok();
    }
}
