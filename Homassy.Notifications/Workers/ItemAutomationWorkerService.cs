using Homassy.API.Context;
using Homassy.API.Entities.Product;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Background service that evaluates item automation rules every 5 minutes.
/// For each due rule it either auto-consumes inventory or sends a notification reminder,
/// then recalculates the next execution time.
/// </summary>
public sealed class ItemAutomationWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebPushService _webPushService;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public ItemAutomationWorkerService(IServiceScopeFactory scopeFactory, IWebPushService webPushService)
    {
        _scopeFactory = scopeFactory;
        _webPushService = webPushService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Item automation worker service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueAutomationsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in item automation worker service");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Item automation worker service stopped");
    }

    private async Task ProcessDueAutomationsAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        Log.Information("Item automation check started at UTC: {UtcNow}", utcNow);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var emailClient = scope.ServiceProvider.GetRequiredService<EmailServiceClient>();

        var dueAutomations = await context.ItemAutomations
            .Include(a => a.ProductInventoryItem)
                .ThenInclude(i => i.Product)
            .Where(a => a.IsEnabled && a.NextExecutionAt != null && a.NextExecutionAt <= utcNow)
            .ToListAsync(cancellationToken);

        if (dueAutomations.Count == 0)
        {
            Log.Information("No due automation rules found");
            return;
        }

        Log.Information("Processing {Count} due automation rules", dueAutomations.Count);

        foreach (var automation in dueAutomations)
        {
            try
            {
                await ProcessSingleAutomationAsync(context, emailClient, automation, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing automation {AutomationId} ({PublicId})",
                    automation.Id, automation.PublicId);

                // Record failure but don't block other rules
                try
                {
                    var failedExecution = new ItemAutomationExecution
                    {
                        ItemAutomationId = automation.Id,
                        Status = AutomationExecutionStatus.Failed,
                        Notes = $"Worker error: {ex.Message}"
                    };
                    context.ItemAutomationExecutions.Add(failedExecution);
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception logEx)
                {
                    Log.Error(logEx, "Failed to record failure execution for automation {AutomationId}", automation.Id);
                }
            }
        }
    }

    private async Task ProcessSingleAutomationAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        CancellationToken cancellationToken)
    {
        var inventoryItem = automation.ProductInventoryItem;

        // Skip if inventory item was not loaded (e.g. soft-deleted by global query filter)
        if (inventoryItem == null)
        {
            Log.Information("Skipping automation {AutomationId} — inventory item not found (possibly deleted)",
                automation.Id);

            var deletedExecution = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = "Inventory item not found (deleted)"
            };
            context.ItemAutomationExecutions.Add(deletedExecution);

            automation.IsEnabled = false;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var product = inventoryItem.Product;
        var productName = product?.Name ?? "Unknown";

        // Skip if product or inventory item has been deleted (soft-deleted)
        if (inventoryItem.IsDeleted || (product != null && product.IsDeleted))
        {
            Log.Information("Skipping automation {AutomationId} — item or product has been deleted",
                automation.Id);

            var deletedExecution = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = "Item or product has been deleted"
            };
            context.ItemAutomationExecutions.Add(deletedExecution);

            // Disable the automation since the item is gone
            automation.IsEnabled = false;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        // Skip if item is fully consumed
        if (inventoryItem.IsFullyConsumed)
        {
            Log.Information("Skipping automation {AutomationId} — item {ItemId} is fully consumed",
                automation.Id, inventoryItem.Id);

            var skippedExecution = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = "Item is fully consumed"
            };
            context.ItemAutomationExecutions.Add(skippedExecution);

            // Still recalculate next execution
            RecalculateNextExecution(automation);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (automation.ActionType == AutomationActionType.AutoConsume)
        {
            await ExecuteAutoConsumeAsync(context, emailClient, automation, inventoryItem, productName, cancellationToken);
        }
        else
        {
            await ExecuteNotifyOnlyAsync(context, emailClient, automation, productName, cancellationToken);
        }

        // Recalculate next execution time
        automation.LastExecutedAt = DateTime.UtcNow;
        RecalculateNextExecution(automation);
        await context.SaveChangesAsync(cancellationToken);

        Log.Information("Automation {AutomationId} executed successfully. Next execution at: {NextExecution}",
            automation.Id, automation.NextExecutionAt);
    }

    private async Task ExecuteAutoConsumeAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        ProductInventoryItem inventoryItem,
        string productName,
        CancellationToken cancellationToken)
    {
        var consumeQuantity = automation.ConsumeQuantity ?? 0;

        if (consumeQuantity <= 0)
        {
            Log.Warning("Automation {AutomationId} has AutoConsume action but no quantity set", automation.Id);
            var skipped = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = "AutoConsume action with no quantity configured"
            };
            context.ItemAutomationExecutions.Add(skipped);
            return;
        }

        // Check if there's enough quantity
        if (consumeQuantity > inventoryItem.CurrentQuantity)
        {
            Log.Warning("Automation {AutomationId}: insufficient quantity ({Current} < {Required})",
                automation.Id, inventoryItem.CurrentQuantity, consumeQuantity);

            var insufficientExecution = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = $"Insufficient quantity: {inventoryItem.CurrentQuantity} available, {consumeQuantity} required"
            };
            context.ItemAutomationExecutions.Add(insufficientExecution);

            // Send a notification about the insufficient quantity
            await SendNotificationToOwnerAsync(context, emailClient, automation, productName,
                "insufficient_quantity", cancellationToken);
            return;
        }

        var remainingQuantity = inventoryItem.CurrentQuantity - consumeQuantity;

        // Create consumption log
        var consumptionLog = new ProductConsumptionLog
        {
            ProductInventoryItemId = inventoryItem.Id,
            UserId = automation.UserId ?? 0,
            ConsumedQuantity = consumeQuantity,
            RemainingQuantity = remainingQuantity
        };
        context.ProductConsumptionLogs.Add(consumptionLog);

        // Update inventory
        inventoryItem.CurrentQuantity = remainingQuantity;
        if (remainingQuantity <= 0)
        {
            inventoryItem.IsFullyConsumed = true;
            inventoryItem.FullyConsumedAt = DateTime.UtcNow;
        }

        // Create execution record
        var isFullyConsumed = remainingQuantity <= 0;
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = automation.Id,
            Status = AutomationExecutionStatus.AutoConsumed,
            ConsumedQuantity = consumeQuantity,
            Notes = isFullyConsumed
                ? "Item fully consumed"
                : $"Consumed {consumeQuantity}, remaining: {remainingQuantity}"
        };
        context.ItemAutomationExecutions.Add(execution);

        Log.Information("Automation {AutomationId}: consumed {Quantity} of {Product}, remaining: {Remaining}",
            automation.Id, consumeQuantity, productName, remainingQuantity);

        // Record activity
        try
        {
            var userId = automation.UserId ?? 0;
            var familyId = automation.FamilyId;
            await new ActivityFunctions().RecordActivityAsync(
                userId, familyId,
                ActivityType.AutomationExecute,
                automation.Id,
                productName,
                automation.ConsumeUnit,
                consumeQuantity,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to record activity for automation {AutomationId}", automation.Id);
        }

        // Send notification confirming the auto-consumption
        var unitStr = automation.ConsumeUnit?.ToString() ?? "";
        await SendAutoConsumeNotificationAsync(context, emailClient, automation, productName, consumeQuantity, unitStr, cancellationToken);
    }

    private async Task ExecuteNotifyOnlyAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        string productName,
        CancellationToken cancellationToken)
    {
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = automation.Id,
            Status = AutomationExecutionStatus.NotificationSent,
            Notes = $"Reminder notification sent for {productName}"
        };
        context.ItemAutomationExecutions.Add(execution);

        Log.Information("Automation {AutomationId}: sending reminder for {Product}", automation.Id, productName);

        await SendNotificationToOwnerAsync(context, emailClient, automation, productName,
            "notify_only", cancellationToken);
    }

    private async Task SendAutoConsumeNotificationAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        string productName,
        decimal quantity,
        string unit,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await GetOwnerUserIdAsync(context, automation, cancellationToken);
        if (ownerUserId == null) return;

        var language = await GetUserLanguageAsync(context, ownerUserId.Value, cancellationToken);
        var subscriptions = await GetUserPushSubscriptionsAsync(context, ownerUserId.Value, cancellationToken);

        if (subscriptions.Count == 0) return;

        var (title, body) = PushNotificationContentService.GetAutomationNotificationContent(language, productName, quantity, unit);
        var actionTitle = GetActionTitle(language);

        foreach (var subscription in subscriptions)
        {
            var success = await _webPushService.SendNotificationAsync(
                subscription, title, body, "/profile/automation", actionTitle, cancellationToken);

            if (!success)
            {
                subscription.DeleteRecord();
                Log.Information("Removed invalid push subscription {Endpoint} for user {UserId}",
                    subscription.Endpoint, ownerUserId.Value);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Send email notification
        await SendEmailNotificationAsync(context, emailClient, ownerUserId.Value, language, productName, "auto_consume", quantity, unit, cancellationToken);
    }

    private async Task SendNotificationToOwnerAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        string productName,
        string emailActionType,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await GetOwnerUserIdAsync(context, automation, cancellationToken);
        if (ownerUserId == null) return;

        var language = await GetUserLanguageAsync(context, ownerUserId.Value, cancellationToken);
        var subscriptions = await GetUserPushSubscriptionsAsync(context, ownerUserId.Value, cancellationToken);

        if (subscriptions.Count > 0)
        {
            var (title, body) = PushNotificationContentService.GetAutomationReminderContent(language, productName);
            var actionTitle = GetActionTitle(language);

            foreach (var subscription in subscriptions)
            {
                var success = await _webPushService.SendNotificationAsync(
                    subscription, title, body, "/profile/automation", actionTitle, cancellationToken);

                if (!success)
                {
                    subscription.DeleteRecord();
                    Log.Information("Removed invalid push subscription {Endpoint} for user {UserId}",
                        subscription.Endpoint, ownerUserId.Value);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // Send email notification
        await SendEmailNotificationAsync(context, emailClient, ownerUserId.Value, language, productName, emailActionType, null, null, cancellationToken);
    }

    private static async Task SendEmailNotificationAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        int userId,
        Language language,
        string productName,
        string actionType,
        decimal? quantity,
        string? unit,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Email, u.Name, EmailEnabled = u.NotificationPreferences != null && u.NotificationPreferences.EmailNotificationsEnabled })
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null || !user.EmailEnabled || string.IsNullOrWhiteSpace(user.Email))
                return;

            await emailClient.SendAutomationNotificationAsync(
                user.Email, language, user.Name, productName, actionType, quantity, unit, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send automation email for user {UserId}, product {Product}", userId, productName);
        }
    }

    private void RecalculateNextExecution(ItemAutomation automation)
    {
        // Get the user's timezone to calculate the next execution
        UserTimeZone userTimeZone;
        try
        {
            var userProfile = automation.UserId.HasValue
                ? new UserFunctions().GetUserProfileByUserId(automation.UserId.Value)
                : null;

            // If family-owned, try to find the first family member's timezone
            if (userProfile == null && automation.FamilyId.HasValue)
            {
                var context = new HomassyDbContext();
                userProfile = context.UserProfiles
                    .FirstOrDefault(p => context.Users
                        .Any(u => u.Id == p.UserId && u.FamilyId == automation.FamilyId.Value && !u.IsDeleted));
            }

            userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;
        }
        catch
        {
            userTimeZone = UserTimeZone.CentralEuropeStandardTime;
        }

        automation.NextExecutionAt = AutomationFunctions.CalculateNextExecutionAt(
            automation.ScheduleType,
            automation.ScheduledTime,
            automation.IntervalDays,
            automation.ScheduledDayOfWeek,
            automation.ScheduledDayOfMonth,
            userTimeZone,
            automation.LastExecutedAt);
    }

    private static async Task<int?> GetOwnerUserIdAsync(
        HomassyDbContext context,
        ItemAutomation automation,
        CancellationToken cancellationToken)
    {
        if (automation.UserId.HasValue)
            return automation.UserId.Value;

        // Family-owned: find the first active family member
        if (automation.FamilyId.HasValue)
        {
            return await context.Users
                .Where(u => u.FamilyId == automation.FamilyId.Value && !u.IsDeleted)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private static async Task<Language> GetUserLanguageAsync(
        HomassyDbContext context,
        int userId,
        CancellationToken cancellationToken)
    {
        var profile = await context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return profile?.DefaultLanguage ?? Language.Hungarian;
    }

    private static async Task<List<API.Entities.User.UserPushSubscription>> GetUserPushSubscriptionsAsync(
        HomassyDbContext context,
        int userId,
        CancellationToken cancellationToken)
    {
        return await context.UserPushSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    private static string GetActionTitle(Language language) => language switch
    {
        Language.German => "Homassy öffnen",
        Language.English => "Open Homassy",
        _ => "Homassy megnyitása"
    };
}
