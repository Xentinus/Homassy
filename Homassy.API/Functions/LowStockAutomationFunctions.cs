using Homassy.API.Context;
using Homassy.API.Infrastructure;
using Homassy.Data.Context;
using Homassy.Data.Entities.Product;
using Homassy.Data.Enums;
using Homassy.Data.Functions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    /// <summary>
    /// The low-stock automation check that runs after every inventory change.
    /// </summary>
    /// <remarks>
    /// Its own class because it was the other half of a constructor cycle (#133):
    /// <c>ProductFunctions</c> called it after every write, and <c>AutomationFunctions</c> - where
    /// it used to live - reads products through <c>ProductFunctions</c>. Neither could take the
    /// other, so both used <c>new</c>.
    /// <para>
    /// Lifting it costs nothing, because it never needed <c>AutomationFunctions</c> in the first
    /// place: it reads its automations straight from a context of its own, and the only thing it
    /// reaches for besides that is the scope factory for the fire-and-forget notification. That
    /// makes it a leaf, and the cycle is gone rather than deferred behind a <c>Lazy&lt;T&gt;</c>.
    /// </para>
    /// </remarks>
    public class LowStockAutomationFunctions
    {
        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public LowStockAutomationFunctions(FunctionsRuntime runtime)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
        }
        /// <summary>
        /// Checks all enabled LowStock automations for the given product and triggers or re-arms them.
        /// Called after every inventory change. Must never throw — wrapped in try-catch.
        /// </summary>
        public async Task CheckLowStockForProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = SessionInfo.GetUserId();
                var familyId = SessionInfo.GetFamilyId();

                await using var context = _contextFactory.CreateDbContext();

                var automations = await context.ItemAutomations
                    .Include(a => a.Product)
                    .Include(a => a.ShoppingList)
                    .Where(a => a.IsEnabled
                             && a.ActionType == AutomationActionType.LowStockAddToShoppingList
                             && a.ProductId == productId)
                    .ToListAsync(cancellationToken);

                if (automations.Count == 0) return;

                foreach (var automation in automations)
                {
                    try
                    {
                        if (!automation.ThresholdQuantity.HasValue) continue;

                        var product = automation.Product;
                        var shoppingList = automation.ShoppingList;

                        if (product == null || product.IsDeleted || shoppingList == null || shoppingList.IsDeleted)
                            continue;

                        // Sum stock scoped by family or user
                        var stockQuery = context.ProductInventoryItems
                            .Where(i => i.Product.Id == productId && !i.IsFullyConsumed);

                        if (automation.FamilyId.HasValue)
                            stockQuery = stockQuery.Where(i => i.FamilyId == automation.FamilyId.Value);
                        else if (automation.UserId.HasValue)
                            stockQuery = stockQuery.Where(i => i.UserId == automation.UserId.Value);

                        var totalStock = await stockQuery.SumAsync(i => i.CurrentQuantity, cancellationToken);

                        if (!automation.IsTriggered && totalStock < automation.ThresholdQuantity.Value)
                        {
                            // TRIGGER: stock below threshold
                            var quantity = automation.AddQuantity ?? 1;
                            var unit = automation.AddUnit ?? Unit.Piece;

                            var shoppingListItem = new Homassy.Data.Entities.ShoppingList.ShoppingListItem
                            {
                                ShoppingListId = shoppingList.Id,
                                ProductId = product.Id,
                                Quantity = quantity,
                                Unit = unit
                            };
                            context.ShoppingListItems.Add(shoppingListItem);

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

                            Log.Information("Low-stock automation {Id}: stock {Stock} < threshold {Threshold} — triggered for product {Product}",
                                automation.Id, totalStock, automation.ThresholdQuantity.Value, product.Name);

                            // Record activity
                            try
                            {
                                await ActivityRecorder.RecordAsync(
                                    _contextFactory,
                                    automation.CreatedByUserId, automation.FamilyId,
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

                            // Fire-and-forget notification. It outlives the request, so it takes a
                            // scope of its own rather than borrowing one that is about to be
                            // disposed underneath it.
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using var scope = _runtime.ScopeFactory.CreateScope();
                                    var notificationsClient = scope.ServiceProvider.GetRequiredService<NotificationsServiceClient>();
                                    await notificationsClient.SendLowStockNotificationAsync(
                                        automation.CreatedByUserId, product.Name, totalStock,
                                        automation.ThresholdQuantity.Value, quantity, unit.ToString(),
                                        shoppingList.Name);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to send low-stock notification for automation {Id}", automation.Id);
                                }
                            }, CancellationToken.None);
                        }
                        else if (automation.IsTriggered && totalStock >= automation.ThresholdQuantity.Value)
                        {
                            // RE-ARM: stock back above threshold
                            automation.IsTriggered = false;
                            await context.SaveChangesAsync(cancellationToken);

                            Log.Information("Low-stock automation {Id} re-armed: stock {Stock} >= threshold {Threshold}",
                                automation.Id, totalStock, automation.ThresholdQuantity.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error checking low-stock automation {Id} for product {ProductId}", automation.Id, productId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in CheckLowStockForProductAsync for product {ProductId}", productId);
            }
        }
    }
}
