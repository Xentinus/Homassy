using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Notifications.Models;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Endpoints;

public static class TestEmailEndpoint
{
    public static async Task<IResult> HandleAsync(
        TestEmailRequest request,
        HomassyDbContext context,
        InventoryExpirationService inventoryService,
        EmailServiceClient emailClient,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.UserId && !u.IsDeleted)
            .Select(u => new
            {
                u.Id,
                u.FamilyId,
                u.Email,
                u.Name,
                Language = u.Profile != null ? u.Profile.DefaultLanguage : Language.Hungarian
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Results.NotFound($"User {request.UserId} not found.");

        var allItems = await inventoryService.GetExpiringItemsForUserAsync(user.Id, user.FamilyId, cancellationToken);
        var expiredItems = allItems.Where(i => i.IsExpired).ToList();
        var expiringSoonItems = allItems.Where(i => !i.IsExpired).ToList();

        var success = await emailClient.SendWeeklySummaryAsync(
            user.Email,
            user.Language,
            user.Name,
            expiredItems,
            expiringSoonItems,
            cancellationToken);

        if (!success)
            return Results.Problem("Failed to send test email.", statusCode: StatusCodes.Status502BadGateway);

        Log.Information("Test email summary sent to user {UserId} ({Email})", user.Id, user.Email);
        return Results.Ok();
    }
}

public sealed record TestEmailRequest(int UserId);
