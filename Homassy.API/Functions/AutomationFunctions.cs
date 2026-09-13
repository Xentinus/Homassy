using Homassy.API.Context;
using Homassy.Data.Entities.Product;
using Homassy.Data.Enums;
using Homassy.Data.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Hubs;
using Homassy.API.Infrastructure;
using Homassy.API.Models.Automation;
using Homassy.Data.Models.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Homassy.Data.Context;
using Homassy.Data.Extensions;
using Homassy.Data.Functions;
using Homassy.Data.Models.Inventory;

namespace Homassy.API.Functions
{
    public class AutomationFunctions
    {
        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly ProductFunctions _productFunctions;
        private readonly UserFunctions _userFunctions;

        public AutomationFunctions(FunctionsRuntime runtime, ProductFunctions productFunctions, UserFunctions userFunctions)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
            _productFunctions = productFunctions;
            _userFunctions = userFunctions;
        }

        #region Schedule Validation

        /// <summary>
        /// Validates schedule configuration based on schedule type.
        /// </summary>
        public static void ValidateSchedule(ScheduleType scheduleType, int? intervalDays, DaysOfWeek? scheduledDaysOfWeek, int? scheduledDayOfMonth)
        {
            if (scheduleType == ScheduleType.Interval)
            {
                if (!intervalDays.HasValue || intervalDays.Value < 1)
                    throw new AutomationInvalidScheduleException("Interval schedule requires IntervalDays >= 1");
            }
            else // FixedDate
            {
                var hasDays = scheduledDaysOfWeek.HasValue && scheduledDaysOfWeek.Value != DaysOfWeek.None;
                if (!hasDays && !scheduledDayOfMonth.HasValue)
                    throw new AutomationInvalidScheduleException("Fixed date schedule requires either ScheduledDaysOfWeek or ScheduledDayOfMonth");
            }
        }

        /// <summary>
        /// Validates that auto-consume rules have a quantity set, and
        /// AddToShoppingList / LowStockAddToShoppingList rules have the required fields.
        /// The unit is always inherited from the related product, so it is not validated here.
        /// </summary>
        public static void ValidateActionType(AutomationActionType actionType, decimal? consumeQuantity,
            Guid? shoppingListPublicId = null, Guid? productPublicId = null, decimal? addQuantity = null,
            decimal? thresholdQuantity = null)
        {
            if (actionType == AutomationActionType.AutoConsume)
            {
                if (!consumeQuantity.HasValue || consumeQuantity.Value <= 0)
                    throw new AutomationInvalidScheduleException("AutoConsume action requires ConsumeQuantity > 0");
            }
            else if (actionType == AutomationActionType.AddToShoppingList)
            {
                if (!shoppingListPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires ShoppingListPublicId");
                if (!productPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires ProductPublicId");
                if (!addQuantity.HasValue || addQuantity.Value <= 0)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires AddQuantity > 0");
            }
            else if (actionType == AutomationActionType.LowStockAddToShoppingList)
            {
                if (!thresholdQuantity.HasValue || thresholdQuantity.Value <= 0)
                    throw new AutomationInvalidScheduleException("LowStockAddToShoppingList action requires ThresholdQuantity > 0");
                if (!shoppingListPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("LowStockAddToShoppingList action requires ShoppingListPublicId");
                if (!productPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("LowStockAddToShoppingList action requires ProductPublicId");
                if (!addQuantity.HasValue || addQuantity.Value <= 0)
                    throw new AutomationInvalidScheduleException("LowStockAddToShoppingList action requires AddQuantity > 0");
            }
        }

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Gets all automation rules for the current user (and family).
        /// </summary>
        public async Task<List<AutomationResponse>> GetAutomationsAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            using var context = _contextFactory.CreateForReading();
            var automations = await context.ItemAutomations
                .Where(a => a.UserId == userId.Value || (familyId.HasValue && a.FamilyId == familyId.Value))
                // Manual order first; rules that have never been dragged all share position 0 and keep
                // the enabled-then-soonest order they have always had.
                .OrderBy(a => a.SortOrder)
                .ThenByDescending(a => a.IsEnabled)
                .ThenBy(a => a.NextExecutionAt)
                .ToListAsync(cancellationToken);

            var responses = new List<AutomationResponse>();

            foreach (var automation in automations)
            {
                var inventoryItem = automation.ProductInventoryItemId.HasValue
                    ? _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                    : null;
                var product = inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null;

                // For AddToShoppingList, resolve product directly
                if (product == null && automation.ProductId.HasValue)
                    product = _productFunctions.GetProductById(automation.ProductId.Value);

                responses.Add(MapToResponse(automation, inventoryItem, product));
            }

            return responses;
        }

        /// <summary>
        /// Gets a single automation rule by PublicId.
        /// </summary>
        public async Task<AutomationResponse> GetAutomationAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            using var context = _contextFactory.CreateForReading();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            var inventoryItem = automation.ProductInventoryItemId.HasValue
                ? _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                : null;
            var product = inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null;

            if (product == null && automation.ProductId.HasValue)
                product = _productFunctions.GetProductById(automation.ProductId.Value);

            return MapToResponse(automation, inventoryItem, product);
        }

        /// <summary>
        /// Creates a new automation rule.
        /// </summary>
        public async Task<AutomationResponse> CreateAutomationAsync(CreateAutomationRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            // Validate schedule (skip for low-stock — event-driven, not schedule-driven)
            if (request.ActionType != AutomationActionType.LowStockAddToShoppingList)
                ValidateSchedule(request.ScheduleType, request.IntervalDays, request.ScheduledDaysOfWeek, request.ScheduledDayOfMonth);
            ValidateActionType(request.ActionType, request.ConsumeQuantity,
                request.ShoppingListPublicId, request.ProductPublicId, request.AddQuantity,
                request.ThresholdQuantity);

            using var context = _contextFactory.CreateDbContext();
            ProductInventoryItem? inventoryItem = null;
            Homassy.Data.Entities.Product.Product? productEntity = null;
            int? shoppingListId = null;
            int? productId = null;

            if (request.ActionType == AutomationActionType.AddToShoppingList || request.ActionType == AutomationActionType.LowStockAddToShoppingList)
            {
                // Resolve product
                var product = _productFunctions.GetProductByPublicId(request.ProductPublicId!.Value);
                if (product == null)
                    throw new AutomationProductNotFoundException();
                productId = product.Id;
                productEntity = product;

                // Resolve shopping list
                var shoppingList = await context.ShoppingLists
                    .FirstOrDefaultAsync(sl => sl.PublicId == request.ShoppingListPublicId!.Value, cancellationToken);
                if (shoppingList == null)
                    throw new AutomationShoppingListNotFoundException();

                // Verify access to shopping list
                if (shoppingList.UserId != userId.Value &&
                    (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
                    throw new AutomationAccessDeniedException();

                shoppingListId = shoppingList.Id;
            }
            else
            {
                // AutoConsume or NotifyOnly — requires inventory item
                if (!request.InventoryItemPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("AutoConsume and NotifyOnly actions require InventoryItemPublicId");

                inventoryItem = _productFunctions.GetInventoryItemByPublicId(request.InventoryItemPublicId.Value);
                if (inventoryItem == null)
                    throw new ProductInventoryItemNotFoundException();

                // Verify access
                if (inventoryItem.UserId != userId.Value &&
                    (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
                    throw new AutomationAccessDeniedException();
            }

            // Get user timezone for scheduling
            var userProfile = _userFunctions.GetUserProfileByUserId(userId.Value);
            var userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;

            // The unit is always inherited from the related product (no longer supplied by the client).
            var derivedUnit = productEntity?.Unit
                ?? (inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId)?.Unit : null);
            var isAddAction = request.ActionType == AutomationActionType.AddToShoppingList
                || request.ActionType == AutomationActionType.LowStockAddToShoppingList;

            var automation = new ItemAutomation
            {
                ProductInventoryItemId = inventoryItem?.Id,
                ProductId = productId,
                ShoppingListId = shoppingListId,
                UserId = request.IsSharedWithFamily && familyId.HasValue ? null : userId.Value,
                FamilyId = request.IsSharedWithFamily && familyId.HasValue ? familyId.Value : null,
                CreatedByUserId = userId.Value,
                ScheduleType = request.ScheduleType,
                IntervalDays = request.IntervalDays,
                ScheduledDaysOfWeek = request.ScheduledDaysOfWeek,
                ScheduledDayOfMonth = request.ScheduledDayOfMonth,
                ScheduledTime = request.ScheduledTime,
                ActionType = request.ActionType,
                ConsumeQuantity = request.ConsumeQuantity,
                ConsumeUnit = isAddAction ? null : derivedUnit,
                AddQuantity = request.AddQuantity,
                AddUnit = isAddAction ? derivedUnit : null,
                ThresholdQuantity = request.ThresholdQuantity,
                IsEnabled = true
            };

            // Calculate next execution (skip for low-stock — event-driven)
            if (request.ActionType != AutomationActionType.LowStockAddToShoppingList)
            {
                automation.NextExecutionAt = AutomationSchedule.CalculateNextExecutionAt(
                    automation.ScheduleType,
                    automation.ScheduledTime,
                    automation.IntervalDays,
                    automation.ScheduledDaysOfWeek,
                    automation.ScheduledDayOfMonth,
                    userTimeZone);
            }

            // New rules append to the end of the manual order.
            automation.SortOrder = SparseOrdering.Append(await MaxAutomationSortOrderAsync(context, userId.Value, familyId, cancellationToken));

            context.ItemAutomations.Add(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"User {userId} created automation {automation.PublicId} for action type {automation.ActionType}");

            // Record activity
            try
            {
                var product = productEntity ?? (inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null);
                await ActivityRecorder.RecordAsync(
                    _contextFactory,
                    userId.Value,
                    familyId,
                    ActivityType.AutomationCreate,
                    automation.Id,
                    product?.Name ?? "Unknown",
                    automation.ConsumeUnit,
                    automation.ConsumeQuantity,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record AutomationCreate activity for automation {automation.PublicId}");
            }

            var finalProduct = productEntity ?? (inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null);
            var response = MapToResponse(automation, inventoryItem, finalProduct);

            // Realtime: add the new rule to the master-data automation list.
            await _runtime.MasterData.AutomationUpsertedAsync(automation.UserId ?? automation.CreatedByUserId, automation.FamilyId, response, cancellationToken);

            return response;
        }

        /// <summary>
        /// Updates an existing automation rule.
        /// </summary>
        public async Task<AutomationResponse> UpdateAutomationAsync(Guid publicId, UpdateAutomationRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            using var context = _contextFactory.CreateDbContext();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            // Apply partial updates
            var scheduleType = request.ScheduleType ?? automation.ScheduleType;
            var intervalDays = request.IntervalDays ?? automation.IntervalDays;
            var scheduledDaysOfWeek = request.ScheduledDaysOfWeek ?? automation.ScheduledDaysOfWeek;
            var scheduledDayOfMonth = request.ScheduledDayOfMonth ?? automation.ScheduledDayOfMonth;
            var actionType = request.ActionType ?? automation.ActionType;
            var consumeQuantity = request.ConsumeQuantity ?? automation.ConsumeQuantity;
            // Units are inherited from the product and not editable via the request.
            var consumeUnit = automation.ConsumeUnit;
            var addQuantity = request.AddQuantity ?? automation.AddQuantity;
            var addUnit = automation.AddUnit;
            var thresholdQuantity = request.ThresholdQuantity ?? automation.ThresholdQuantity;

            // Validate after merge (skip schedule validation for low-stock — event-driven)
            if (actionType != AutomationActionType.LowStockAddToShoppingList)
                ValidateSchedule(scheduleType, intervalDays, scheduledDaysOfWeek, scheduledDayOfMonth);

            // Only validate action type fields when action-type-related properties are being changed
            if (request.ActionType.HasValue || request.ConsumeQuantity.HasValue ||
                request.AddQuantity.HasValue ||
                request.ShoppingListPublicId.HasValue || request.ProductPublicId.HasValue ||
                request.ThresholdQuantity.HasValue)
            {
                // For update, use existing entity IDs if not provided in request
                var shoppingListPublicId = request.ShoppingListPublicId ?? (automation.ShoppingListId.HasValue ? Guid.Empty : (Guid?)null);
                var productPublicId = request.ProductPublicId ?? (automation.ProductId.HasValue ? Guid.Empty : (Guid?)null);
                ValidateActionType(actionType, consumeQuantity, shoppingListPublicId, productPublicId, addQuantity, thresholdQuantity);
            }

            automation.ScheduleType = scheduleType;
            automation.IntervalDays = intervalDays;
            automation.ScheduledDaysOfWeek = scheduledDaysOfWeek;
            automation.ScheduledDayOfMonth = scheduledDayOfMonth;
            automation.ActionType = actionType;
            automation.ConsumeQuantity = consumeQuantity;
            automation.ConsumeUnit = consumeUnit;
            automation.AddQuantity = addQuantity;
            automation.AddUnit = addUnit;
            automation.ThresholdQuantity = thresholdQuantity;

            // Reset IsTriggered when threshold changes so the automation re-evaluates
            if (request.ThresholdQuantity.HasValue)
                automation.IsTriggered = false;

            if (request.ScheduledTime.HasValue)
                automation.ScheduledTime = request.ScheduledTime.Value;

            if (request.IsEnabled.HasValue)
                automation.IsEnabled = request.IsEnabled.Value;

            // Recalculate next execution if schedule changed
            bool scheduleChanged = request.ScheduleType.HasValue || request.IntervalDays.HasValue ||
                                   request.ScheduledDaysOfWeek.HasValue || request.ScheduledDayOfMonth.HasValue ||
                                   request.ScheduledTime.HasValue || request.IsEnabled.HasValue;

            if (scheduleChanged && automation.IsEnabled)
            {
                var userProfile = _userFunctions.GetUserProfileByUserId(userId.Value);
                var userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;

                automation.NextExecutionAt = AutomationSchedule.CalculateNextExecutionAt(
                    automation.ScheduleType,
                    automation.ScheduledTime,
                    automation.IntervalDays,
                    automation.ScheduledDaysOfWeek,
                    automation.ScheduledDayOfMonth,
                    userTimeZone,
                    automation.LastExecutedAt);
            }

            if (request.IsEnabled.HasValue && !request.IsEnabled.Value)
            {
                automation.NextExecutionAt = null;
            }

            context.ItemAutomations.Update(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"User {userId} updated automation {automation.PublicId}");

            // Record activity
            try
            {
                var inventoryItem = automation.ProductInventoryItemId.HasValue
                    ? _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                    : null;
                var product = inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null;
                if (product == null && automation.ProductId.HasValue)
                    product = _productFunctions.GetProductById(automation.ProductId.Value);
                await ActivityRecorder.RecordAsync(
                    _contextFactory,
                    userId.Value,
                    familyId,
                    ActivityType.AutomationUpdate,
                    automation.Id,
                    product?.Name ?? "Unknown",
                    automation.ConsumeUnit,
                    automation.ConsumeQuantity,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record AutomationUpdate activity for automation {automation.PublicId}");
            }

            var item = automation.ProductInventoryItemId.HasValue
                ? _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                : null;
            var prod = item != null ? _productFunctions.GetProductById(item.ProductId) : null;
            if (prod == null && automation.ProductId.HasValue)
                prod = _productFunctions.GetProductById(automation.ProductId.Value);
            var response = MapToResponse(automation, item, prod);

            // Realtime: push the updated rule to the master-data automation list.
            await _runtime.MasterData.AutomationUpsertedAsync(automation.UserId ?? automation.CreatedByUserId, automation.FamilyId, response, cancellationToken);

            return response;
        }

        /// <summary>
        /// Soft-deletes an automation rule.
        /// </summary>
        public async Task DeleteAutomationAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            using var context = _contextFactory.CreateDbContext();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            automation.DeleteRecord(userId.Value);
            context.ItemAutomations.Update(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"User {userId} deleted automation {automation.PublicId}");

            // Realtime: remove the rule from the master-data automation list.
            await _runtime.MasterData.AutomationDeletedAsync(automation.UserId ?? automation.CreatedByUserId, automation.FamilyId, automation.PublicId, cancellationToken);

            // Record activity
            try
            {
                var inventoryItem = automation.ProductInventoryItemId.HasValue
                    ? _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                    : null;
                var product = inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null;
                if (product == null && automation.ProductId.HasValue)
                    product = _productFunctions.GetProductById(automation.ProductId.Value);
                await ActivityRecorder.RecordAsync(
                    _contextFactory,
                    userId.Value,
                    familyId,
                    ActivityType.AutomationDelete,
                    automation.Id,
                    product?.Name ?? "Unknown",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record AutomationDelete activity for automation {automation.PublicId}");
            }
        }

        #endregion

        #region Execution

        /// <summary>
        /// Manually executes an automation rule (used for manual confirmation from "Notify only" mode).
        /// </summary>
        public async Task<AutomationExecutionResponse> ExecuteAutomationAsync(Guid publicId, ExecuteAutomationRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            using var context = _contextFactory.CreateDbContext();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            // Low-stock automations cannot be manually executed
            if (automation.ActionType == AutomationActionType.LowStockAddToShoppingList)
                throw new AutomationInvalidScheduleException("Low-stock automations cannot be manually executed");

            ItemAutomationExecution execution;

            if (automation.ActionType == AutomationActionType.AddToShoppingList || automation.ActionType == AutomationActionType.LowStockAddToShoppingList)
            {
                execution = await ExecuteAddToShoppingListManualAsync(context, automation, userId.Value, familyId, request.Notes, cancellationToken);
            }
            else
            {
                if (!automation.ProductInventoryItemId.HasValue)
                    throw new AutomationInvalidScheduleException("Manual execution requires an inventory item");

                var inventoryItem = _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value);
                if (inventoryItem == null)
                    throw new ProductInventoryItemNotFoundException();

                if (inventoryItem.IsFullyConsumed)
                    throw new AutomationItemFullyConsumedException();

                execution = await ExecuteConsumptionAsync(context, automation, inventoryItem, userId.Value, familyId, request.Notes, cancellationToken);
            }

            // Recalculate next execution
            var userProfile = _userFunctions.GetUserProfileByUserId(userId.Value);
            var userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;

            automation.LastExecutedAt = DateTime.UtcNow;

            // Low-stock automations don't use schedule-based NextExecutionAt
            if (automation.ActionType != AutomationActionType.LowStockAddToShoppingList)
            {
                automation.NextExecutionAt = automation.IsEnabled
                    ? AutomationSchedule.CalculateNextExecutionAt(
                        automation.ScheduleType,
                        automation.ScheduledTime,
                        automation.IntervalDays,
                        automation.ScheduledDaysOfWeek,
                        automation.ScheduledDayOfMonth,
                        userTimeZone,
                        automation.LastExecutedAt)
                    : null;
            }

            context.ItemAutomations.Update(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"User {userId} manually executed automation {automation.PublicId}");

            // Record activity
            try
            {
                string productName;
                Unit? unit;
                decimal? quantity;

                if (automation.ActionType == AutomationActionType.AddToShoppingList || automation.ActionType == AutomationActionType.LowStockAddToShoppingList)
                {
                    var product = automation.ProductId.HasValue ? _productFunctions.GetProductById(automation.ProductId.Value) : null;
                    productName = product?.Name ?? "Unknown";
                    unit = automation.AddUnit;
                    quantity = automation.AddQuantity;
                }
                else
                {
                    var inventoryItem = automation.ProductInventoryItemId.HasValue
                        ? _productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value) : null;
                    var product = inventoryItem != null ? _productFunctions.GetProductById(inventoryItem.ProductId) : null;
                    productName = product?.Name ?? "Unknown";
                    unit = automation.ConsumeUnit;
                    quantity = execution.ConsumedQuantity;
                }

                await ActivityRecorder.RecordAsync(
                    _contextFactory,
                    userId.Value,
                    familyId,
                    ActivityType.AutomationExecute,
                    automation.Id,
                    productName,
                    unit,
                    quantity,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record AutomationExecute activity for automation {automation.PublicId}");
            }

            return MapToExecutionResponse(execution);
        }

        /// <summary>
        /// Manually executes an AddToShoppingList automation (adds the product to the shopping list).
        /// </summary>
        internal async Task<ItemAutomationExecution> ExecuteAddToShoppingListManualAsync(
            HomassyDbContext context,
            ItemAutomation automation,
            int userId,
            int? familyId,
            string? notes,
            CancellationToken cancellationToken)
        {
            // Resolve shopping list
            if (!automation.ShoppingListId.HasValue)
                throw new AutomationInvalidScheduleException("AddToShoppingList automation has no shopping list configured");

            var shoppingList = await context.ShoppingLists
                .FirstOrDefaultAsync(sl => sl.Id == automation.ShoppingListId.Value, cancellationToken);
            if (shoppingList == null)
                throw new AutomationShoppingListNotFoundException();

            // Resolve product
            if (!automation.ProductId.HasValue)
                throw new AutomationInvalidScheduleException("AddToShoppingList automation has no product configured");

            var product = _productFunctions.GetProductById(automation.ProductId.Value);
            if (product == null)
                throw new AutomationProductNotFoundException();

            var quantity = automation.AddQuantity ?? 1;
            var unit = automation.AddUnit ?? Unit.Piece;

            // Create shopping list item
            var shoppingListItem = new Homassy.Data.Entities.ShoppingList.ShoppingListItem
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
                Notes = notes ?? $"Added {quantity} {unit} of \"{product.Name}\" to \"{shoppingList.Name}\"",
                TriggeredByUserId = userId
            };
            context.ItemAutomationExecutions.Add(execution);

            await context.SaveChangesAsync(cancellationToken);

            Log.Information("User {UserId} manually executed AddToShoppingList automation {AutomationId}: added {Quantity} {Unit} of {Product} to {ShoppingList}",
                userId, automation.PublicId, quantity, unit, product.Name, shoppingList.Name);

            return execution;
        }

        /// <summary>
        /// Gets execution history for an automation rule.
        /// </summary>
        public async Task<List<AutomationExecutionResponse>> GetExecutionHistoryAsync(Guid publicId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            using var context = _contextFactory.CreateForReading();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            var executions = await context.ItemAutomationExecutions
                .Where(e => e.ItemAutomationId == automation.Id)
                .OrderByDescending(e => e.ExecutedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return executions.Select(MapToExecutionResponse).ToList();
        }

        /// <summary>
        /// Performs the actual consumption on an inventory item.
        /// </summary>
        internal async Task<ItemAutomationExecution> ExecuteConsumptionAsync(
            HomassyDbContext context,
            ItemAutomation automation,
            ProductInventoryItem inventoryItem,
            int userId,
            int? familyId,
            string? notes,
            CancellationToken cancellationToken)
        {
            var consumeQuantity = automation.ConsumeQuantity ?? 0;

            if (consumeQuantity <= 0)
            {
                // NotifyOnly or no quantity — just record the execution
                var notifyExecution = new ItemAutomationExecution
                {
                    ItemAutomationId = automation.Id,
                    Status = AutomationExecutionStatus.ManuallyConfirmed,
                    Notes = notes ?? "Manually confirmed by user",
                    TriggeredByUserId = userId
                };
                context.ItemAutomationExecutions.Add(notifyExecution);
                await context.SaveChangesAsync(cancellationToken);
                return notifyExecution;
            }

            // Check quantity
            if (consumeQuantity > inventoryItem.CurrentQuantity)
                throw new AutomationInsufficientQuantityException();

            var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
            if (trackedItem == null)
                throw new ProductInventoryItemNotFoundException();

            var remainingQuantity = trackedItem.CurrentQuantity - consumeQuantity;

            // Create consumption log
            var consumptionLog = new ProductConsumptionLog
            {
                ProductInventoryItemId = trackedItem.Id,
                UserId = userId,
                ConsumedQuantity = consumeQuantity,
                RemainingQuantity = remainingQuantity
            };
            context.ProductConsumptionLogs.Add(consumptionLog);

            // Update inventory
            trackedItem.CurrentQuantity = remainingQuantity;
            if (remainingQuantity <= 0)
            {
                trackedItem.IsFullyConsumed = true;
                trackedItem.FullyConsumedAt = DateTime.UtcNow;
            }

            // Create execution record
            var isFullyConsumed = remainingQuantity <= 0;
            var execution = new ItemAutomationExecution
            {
                ItemAutomationId = automation.Id,
                Status = AutomationExecutionStatus.AutoConsumed,
                ConsumedQuantity = consumeQuantity,
                Notes = notes ?? (isFullyConsumed ? "Item fully consumed" : $"Consumed {consumeQuantity}, remaining: {remainingQuantity}"),
                TriggeredByUserId = userId
            };
            context.ItemAutomationExecutions.Add(execution);

            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"Automation {automation.PublicId}: consumed {consumeQuantity} from inventory item {trackedItem.PublicId}, remaining: {remainingQuantity}");

            // Realtime: reflect the auto-consume on every grid showing this item. Scope from the item
            // itself (family-shared vs personal), not the automation.
            var broadcastProduct = _productFunctions.GetProductById(trackedItem.ProductId);
            if (broadcastProduct != null)
            {
                var itemUserId = trackedItem.UserId ?? userId;
                if (trackedItem.IsFullyConsumed)
                {
                    await _runtime.Inventory.InventoryDeletedAsync(
                        itemUserId, trackedItem.FamilyId, trackedItem.FamilyId.HasValue,
                        broadcastProduct.PublicId, trackedItem.PublicId, cancellationToken);
                }
                else
                {
                    await _runtime.Inventory.InventoryUpsertedAsync(
                        itemUserId, trackedItem.FamilyId,
                        InventoryGridProjection.BuildProduct(broadcastProduct),
                        InventoryGridProjection.BuildItem(
                            trackedItem,
                            broadcastProduct.PublicId,
                            _productFunctions.GetPurchaseInfoByInventoryItemId(trackedItem.Id)?.OriginalQuantity),
                        cancellationToken);
                }
            }

            return execution;
        }

        #endregion

        #region Low Stock Event-Driven Check

        #endregion

        #region Mapping

        private static AutomationResponse MapToResponse(ItemAutomation automation, ProductInventoryItem? inventoryItem, Product? product)
        {
            return new AutomationResponse
            {
                PublicId = automation.PublicId,
                InventoryItemPublicId = inventoryItem?.PublicId,
                ProductName = product?.Name ?? string.Empty,
                ProductBrand = product?.Brand ?? string.Empty,
                ProductPublicId = product?.PublicId,
                ShoppingListPublicId = automation.ShoppingList?.PublicId,
                ShoppingListName = automation.ShoppingList?.Name,
                ScheduleType = automation.ScheduleType,
                IntervalDays = automation.IntervalDays,
                ScheduledDaysOfWeek = automation.ScheduledDaysOfWeek,
                ScheduledDayOfMonth = automation.ScheduledDayOfMonth,
                ScheduledTime = automation.ScheduledTime,
                ActionType = automation.ActionType,
                ConsumeQuantity = automation.ConsumeQuantity,
                ConsumeUnit = automation.ConsumeUnit,
                AddQuantity = automation.AddQuantity,
                AddUnit = automation.AddUnit,
                ThresholdQuantity = automation.ThresholdQuantity,
                IsTriggered = automation.IsTriggered,
                IsEnabled = automation.IsEnabled,
                SortOrder = automation.SortOrder,
                NextExecutionAt = automation.NextExecutionAt,
                LastExecutedAt = automation.LastExecutedAt
            };
        }

        private static AutomationExecutionResponse MapToExecutionResponse(ItemAutomationExecution execution)
        {
            return new AutomationExecutionResponse
            {
                PublicId = execution.PublicId,
                ExecutedAt = execution.ExecutedAt,
                Status = execution.Status,
                ConsumedQuantity = execution.ConsumedQuantity,
                Notes = execution.Notes,
                TriggeredByUserId = execution.TriggeredByUserId
            };
        }

        #endregion

        #region Manual Ordering
        /// <summary>
        /// The highest manual position among the automation rules this caller can see — the same scope
        /// <see cref="GetAutomationsAsync"/> lists.
        /// </summary>
        private static async Task<int?> MaxAutomationSortOrderAsync(HomassyDbContext context, int userId, int? familyId, CancellationToken cancellationToken)
            => await context.ItemAutomations
                .Where(a => a.UserId == userId || (familyId.HasValue && a.FamilyId == familyId.Value))
                .MaxAsync(a => (int?)a.SortOrder, cancellationToken);

        /// <summary>
        /// Writes a new manual order for the caller's automation rules. See <see cref="SparseOrdering"/>
        /// for why the full ordered id list usually costs one row write.
        /// </summary>
        public async Task<List<ReorderedEntry>> ReorderAutomationsAsync(ReorderAutomationsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            if (request.AutomationPublicIds.Distinct().Count() != request.AutomationPublicIds.Count)
            {
                throw new BadRequestException("The requested order contains the same automation twice");
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var automations = await context.ItemAutomations
                    .Where(a => request.AutomationPublicIds.Contains(a.PublicId)
                        && (a.UserId == userId.Value || (familyId.HasValue && a.FamilyId == familyId.Value)))
                    .ToListAsync(cancellationToken);

                // An unknown or foreign id is rejected rather than skipped, so the client never renders
                // an order the server did not store.
                if (automations.Count != request.AutomationPublicIds.Count)
                {
                    throw new AutomationNotFoundException("One or more automations were not found");
                }

                var byPublicId = automations.ToDictionary(a => a.PublicId);
                var ordered = request.AutomationPublicIds.Select(id => byPublicId[id]).ToList();
                var changes = SparseOrdering.PlanReorder(ordered.Select(a => a.SortOrder).ToList());

                var moved = new List<ReorderedEntry>(changes.Count);
                foreach (var (index, sortOrder) in changes)
                {
                    ordered[index].SortOrder = sortOrder;
                    ordered[index].UpdateRecordChange(userId.Value);
                    moved.Add(new ReorderedEntry { PublicId = ordered[index].PublicId, SortOrder = sortOrder });
                }

                if (moved.Count > 0)
                {
                    await context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                Log.Information($"User {userId.Value} reordered automations: {moved.Count} of {ordered.Count} moved");

                if (moved.Count > 0)
                {
                    await _runtime.MasterData.AutomationsReorderedAsync(userId.Value, familyId, moved, cancellationToken);
                }

                return moved;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to reorder automations for user {userId.Value}");
                throw;
            }
        }
        #endregion
    }
}
