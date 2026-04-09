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
                await ProcessLowStockAutomationsAsync(stoppingToken);
                await RearmLowStockAutomationsAsync(stoppingToken);
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
                .ThenInclude(i => i!.Product)
            .Include(a => a.Product)
            .Include(a => a.ShoppingList)
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
        // AddToShoppingList automations don't require an inventory item
        if (automation.ActionType == AutomationActionType.AddToShoppingList)
        {
            await ExecuteAddToShoppingListAsync(context, emailClient, automation, cancellationToken);

            automation.LastExecutedAt = DateTime.UtcNow;
            RecalculateNextExecution(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information("Automation {AutomationId} (AddToShoppingList) executed successfully. Next execution at: {NextExecution}",
                automation.Id, automation.NextExecutionAt);
            return;
        }

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

        // Record activity — use the automation creator for automatic execution
        try
        {
            var userId = automation.CreatedByUserId;
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

    private async Task ExecuteAddToShoppingListAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        CancellationToken cancellationToken)
    {
        var shoppingList = automation.ShoppingList;
        var product = automation.Product;

        // Validate shopping list still exists
        if (shoppingList == null || shoppingList.IsDeleted)
        {
            Log.Warning("Automation {AutomationId}: target shopping list not found or deleted", automation.Id);

            var skipped = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = "Target shopping list not found or deleted"
            };
            context.ItemAutomationExecutions.Add(skipped);

            automation.IsEnabled = false;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        // Validate product still exists
        if (product == null || product.IsDeleted)
        {
            Log.Warning("Automation {AutomationId}: target product not found or deleted", automation.Id);

            var skipped = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.Skipped,
                Notes = "Target product not found or deleted"
            };
            context.ItemAutomationExecutions.Add(skipped);

            automation.IsEnabled = false;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var quantity = automation.AddQuantity ?? 1;
        var unit = automation.AddUnit ?? API.Enums.Unit.Piece;

        // Create shopping list item
        var shoppingListItem = new API.Entities.ShoppingList.ShoppingListItem
        {
            ShoppingListId = shoppingList.Id,
            ProductId = product.Id,
            Quantity = quantity,
            Unit = unit
        };
        context.ShoppingListItems.Add(shoppingListItem);

        // Record execution
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = automation.Id,
            Status = AutomationExecutionStatus.AddedToShoppingList,
            Notes = $"Added {quantity} {unit} of \"{product.Name}\" to \"{shoppingList.Name}\" shopping list"
        };
        context.ItemAutomationExecutions.Add(execution);

        Log.Information("Automation {AutomationId}: added {Quantity} {Unit} of {Product} to shopping list {ShoppingList}",
            automation.Id, quantity, unit, product.Name, shoppingList.Name);

        // Record activity — use the automation creator for automatic execution
        try
        {
            var userId = automation.CreatedByUserId;
            var familyId = automation.FamilyId;
            await new ActivityFunctions().RecordActivityAsync(
                userId, familyId,
                ActivityType.AutomationExecute,
                automation.Id,
                product.Name,
                unit,
                quantity,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to record activity for automation {AutomationId}", automation.Id);
        }

        // Send push notification
        await SendShoppingListNotificationAsync(context, emailClient, automation, product.Name, quantity, unit.ToString(), shoppingList.Name, cancellationToken);
    }

    private async Task SendShoppingListNotificationAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        string productName,
        decimal quantity,
        string unit,
        string shoppingListName,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await GetOwnerUserIdAsync(context, automation, cancellationToken);
        if (ownerUserId == null) return;

        var language = await GetUserLanguageAsync(context, ownerUserId.Value, cancellationToken);
        var subscriptions = await GetUserPushSubscriptionsAsync(context, ownerUserId.Value, cancellationToken);

        if (subscriptions.Count > 0)
        {
            var (title, body) = PushNotificationContentService.GetShoppingListAutomationContent(
                language, productName, quantity, unit, shoppingListName);
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
        await SendEmailNotificationAsync(context, emailClient, ownerUserId.Value, language, productName,
            "add_to_shopping_list", quantity, unit, cancellationToken);
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
            automation.ScheduledDaysOfWeek,
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

    #region Low Stock Automation

    /// <summary>
    /// Checks all enabled LowStockAddToShoppingList automations that have not yet triggered.
    /// If the total stock for the product drops below the threshold, adds the product to the shopping list.
    /// </summary>
    private async Task ProcessLowStockAutomationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var emailClient = scope.ServiceProvider.GetRequiredService<EmailServiceClient>();

        var automations = await context.ItemAutomations
            .Include(a => a.Product)
            .Include(a => a.ShoppingList)
            .Where(a => a.IsEnabled
                     && a.ActionType == AutomationActionType.LowStockAddToShoppingList
                     && !a.IsTriggered)
            .ToListAsync(cancellationToken);

        if (automations.Count == 0) return;

        Log.Information("Processing {Count} low-stock automations", automations.Count);

        foreach (var automation in automations)
        {
            try
            {
                if (!automation.ProductId.HasValue || !automation.ThresholdQuantity.HasValue)
                {
                    Log.Warning("Low-stock automation {Id} missing ProductId or ThresholdQuantity", automation.Id);
                    continue;
                }

                var product = automation.Product;
                var shoppingList = automation.ShoppingList;

                if (product == null || product.IsDeleted)
                {
                    Log.Warning("Low-stock automation {Id}: product not found or deleted — disabling", automation.Id);
                    automation.IsEnabled = false;
                    await context.SaveChangesAsync(cancellationToken);
                    continue;
                }

                if (shoppingList == null || shoppingList.IsDeleted)
                {
                    Log.Warning("Low-stock automation {Id}: shopping list not found or deleted — disabling", automation.Id);
                    automation.IsEnabled = false;
                    await context.SaveChangesAsync(cancellationToken);
                    continue;
                }

                // Sum all non-fully-consumed inventory for this product, scoped to user or family
                var totalStock = await GetTotalStockForProductAsync(context, automation, cancellationToken);

                if (totalStock >= automation.ThresholdQuantity.Value)
                    continue;

                // Stock is below threshold — trigger!
                var quantity = automation.AddQuantity ?? 1;
                var unit = automation.AddUnit ?? API.Enums.Unit.Piece;

                // Create shopping list item
                var shoppingListItem = new API.Entities.ShoppingList.ShoppingListItem
                {
                    ShoppingListId = shoppingList.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    Unit = unit
                };
                context.ShoppingListItems.Add(shoppingListItem);

                // Record execution
                var execution = new ItemAutomationExecution
                {
                    ItemAutomationId = automation.Id,
                    Status = AutomationExecutionStatus.AddedToShoppingList,
                    Notes = $"Low stock triggered: total stock {totalStock} below threshold {automation.ThresholdQuantity.Value}. " +
                            $"Added {quantity} {unit} of \"{product.Name}\" to \"{shoppingList.Name}\""
                };
                context.ItemAutomationExecutions.Add(execution);

                automation.IsTriggered = true;
                automation.LastExecutedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);

                Log.Information("Low-stock automation {Id}: total stock {Stock} < threshold {Threshold} — added {Qty} {Unit} of {Product} to {List}",
                    automation.Id, totalStock, automation.ThresholdQuantity.Value, quantity, unit, product.Name, shoppingList.Name);

                // Record activity
                try
                {
                    var userId = automation.CreatedByUserId;
                    var familyId = automation.FamilyId;
                    await new ActivityFunctions().RecordActivityAsync(
                        userId, familyId,
                        ActivityType.AutomationExecute,
                        automation.Id,
                        product.Name,
                        unit,
                        quantity,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to record activity for low-stock automation {Id}", automation.Id);
                }

                // Send push notification
                await SendLowStockNotificationAsync(context, emailClient, automation, product.Name, totalStock,
                    automation.ThresholdQuantity.Value, quantity, unit.ToString(), shoppingList.Name, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing low-stock automation {Id} ({PublicId})", automation.Id, automation.PublicId);
            }
        }
    }

    /// <summary>
    /// Re-arms triggered LowStockAddToShoppingList automations whose stock has been replenished above the threshold.
    /// </summary>
    private async Task RearmLowStockAutomationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var triggeredAutomations = await context.ItemAutomations
            .Where(a => a.IsEnabled
                     && a.ActionType == AutomationActionType.LowStockAddToShoppingList
                     && a.IsTriggered)
            .ToListAsync(cancellationToken);

        if (triggeredAutomations.Count == 0) return;

        foreach (var automation in triggeredAutomations)
        {
            try
            {
                if (!automation.ProductId.HasValue || !automation.ThresholdQuantity.HasValue)
                    continue;

                var totalStock = await GetTotalStockForProductAsync(context, automation, cancellationToken);

                if (totalStock >= automation.ThresholdQuantity.Value)
                {
                    automation.IsTriggered = false;
                    Log.Information("Low-stock automation {Id} re-armed: stock {Stock} >= threshold {Threshold}",
                        automation.Id, totalStock, automation.ThresholdQuantity.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error re-arming low-stock automation {Id}", automation.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sums CurrentQuantity of all non-fully-consumed inventory items for the automation's product,
    /// scoped to the automation's user or family.
    /// </summary>
    private static async Task<decimal> GetTotalStockForProductAsync(
        HomassyDbContext context, ItemAutomation automation, CancellationToken cancellationToken)
    {
        var query = context.ProductInventoryItems
            .Where(i => i.Product.Id == automation.ProductId!.Value && !i.IsFullyConsumed);

        // Scope to user or family
        if (automation.FamilyId.HasValue)
            query = query.Where(i => i.FamilyId == automation.FamilyId.Value);
        else if (automation.UserId.HasValue)
            query = query.Where(i => i.UserId == automation.UserId.Value);

        return await query.SumAsync(i => i.CurrentQuantity, cancellationToken);
    }

    private async Task SendLowStockNotificationAsync(
        HomassyDbContext context,
        EmailServiceClient emailClient,
        ItemAutomation automation,
        string productName,
        decimal totalStock,
        decimal thresholdQuantity,
        decimal addQuantity,
        string unit,
        string shoppingListName,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await GetOwnerUserIdAsync(context, automation, cancellationToken);
        if (ownerUserId == null) return;

        var language = await GetUserLanguageAsync(context, ownerUserId.Value, cancellationToken);
        var subscriptions = await GetUserPushSubscriptionsAsync(context, ownerUserId.Value, cancellationToken);

        if (subscriptions.Count > 0)
        {
            var (title, body) = PushNotificationContentService.GetLowStockNotificationContent(
                language, productName, totalStock, thresholdQuantity, shoppingListName);
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
        await SendEmailNotificationAsync(context, emailClient, ownerUserId.Value, language, productName,
            "low_stock_add_to_shopping_list", addQuantity, unit, cancellationToken);
    }

    #endregion
}
