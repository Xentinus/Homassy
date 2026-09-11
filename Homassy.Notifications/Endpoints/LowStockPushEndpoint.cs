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
        FamilyPushNotifier notifier,
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

        // Both channels through the shared notifier (#116): the push, and the row the in-app
        // notification centre reads. This handler used to loop the subscriptions itself, render
        // the text and delete the dead ones - a fourth copy of code that now lives in one place.
        //
        // It also used to push without consulting `PushNotificationsEnabled`, so a user who had
        // switched push off still got low-stock pushes. The notifier resolves recipients by
        // preference, so that is fixed on the way past.
        if (await notifier.GetRecipientAsync(context, request.UserId, cancellationToken) is { } recipient)
        {
            await notifier.DispatchAsync(context, [recipient],
                [NotificationEnvelopes.LowStock(
                    request.ProductName, request.TotalStock, request.ThresholdQuantity, request.ShoppingListName)],
                "/profile/automation", cancellationToken);
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
