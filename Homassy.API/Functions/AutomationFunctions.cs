using Homassy.API.Context;
using Homassy.API.Entities.Product;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Automation;
using Homassy.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    public class AutomationFunctions
    {
        #region NextExecutionAt Calculation

        /// <summary>
        /// Calculates the next execution time in UTC based on the automation schedule and user's timezone.
        /// </summary>
        public static DateTime? CalculateNextExecutionAt(
            ScheduleType scheduleType,
            TimeOnly scheduledTime,
            int? intervalDays,
            DaysOfWeek? scheduledDaysOfWeek,
            int? scheduledDayOfMonth,
            UserTimeZone userTimeZone,
            DateTime? lastExecutedAtUtc = null)
        {
            var tzId = userTimeZone.ToTimeZoneId();
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            DateTime nextLocal;

            if (scheduleType == ScheduleType.Interval)
            {
                if (!intervalDays.HasValue || intervalDays.Value < 1)
                    return null;

                if (lastExecutedAtUtc.HasValue)
                {
                    var lastLocal = TimeZoneInfo.ConvertTimeFromUtc(lastExecutedAtUtc.Value, tz);
                    var nextDate = lastLocal.Date.AddDays(intervalDays.Value);
                    nextLocal = nextDate.Add(scheduledTime.ToTimeSpan());

                    // If calculated time is in the past, advance forward
                    while (nextLocal <= nowLocal)
                    {
                        nextLocal = nextLocal.AddDays(intervalDays.Value);
                    }
                }
                else
                {
                    // First execution: schedule for today at the specified time, or tomorrow if past
                    nextLocal = nowLocal.Date.Add(scheduledTime.ToTimeSpan());
                    if (nextLocal <= nowLocal)
                    {
                        nextLocal = nextLocal.AddDays(intervalDays.Value);
                    }
                }
            }
            else // FixedDate
            {
                if (scheduledDaysOfWeek.HasValue && scheduledDaysOfWeek.Value != DaysOfWeek.None)
                {
                    // Weekly schedule: find next occurrence among selected days
                    var selectedDays = GetSelectedDays(scheduledDaysOfWeek.Value);
                    if (selectedDays.Count == 0)
                        return null;

                    // Find the nearest upcoming day from the set
                    DateTime? earliest = null;
                    foreach (var day in selectedDays)
                    {
                        var daysUntil = ((int)day - (int)nowLocal.DayOfWeek + 7) % 7;
                        var candidate = nowLocal.Date.AddDays(daysUntil).Add(scheduledTime.ToTimeSpan());

                        // If it's today but the time has passed, go to next week
                        if (candidate <= nowLocal)
                        {
                            candidate = candidate.AddDays(7);
                        }

                        if (!earliest.HasValue || candidate < earliest.Value)
                        {
                            earliest = candidate;
                        }
                    }

                    nextLocal = earliest!.Value;
                }
                else if (scheduledDayOfMonth.HasValue)
                {
                    // Monthly schedule: find next occurrence of the specified day of month
                    var day = Math.Min(scheduledDayOfMonth.Value, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month));
                    nextLocal = new DateTime(nowLocal.Year, nowLocal.Month, day).Add(scheduledTime.ToTimeSpan());

                    if (nextLocal <= nowLocal)
                    {
                        // Move to next month
                        var nextMonth = nowLocal.AddMonths(1);
                        day = Math.Min(scheduledDayOfMonth.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                        nextLocal = new DateTime(nextMonth.Year, nextMonth.Month, day).Add(scheduledTime.ToTimeSpan());
                    }
                }
                else
                {
                    return null;
                }
            }

            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), tz);
        }

        /// <summary>
        /// Converts DaysOfWeek flags to a list of DayOfWeek values.
        /// </summary>
        internal static List<DayOfWeek> GetSelectedDays(DaysOfWeek daysOfWeek)
        {
            var days = new List<DayOfWeek>();
            if (daysOfWeek.HasFlag(DaysOfWeek.Monday)) days.Add(DayOfWeek.Monday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Tuesday)) days.Add(DayOfWeek.Tuesday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Wednesday)) days.Add(DayOfWeek.Wednesday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Thursday)) days.Add(DayOfWeek.Thursday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Friday)) days.Add(DayOfWeek.Friday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Saturday)) days.Add(DayOfWeek.Saturday);
            if (daysOfWeek.HasFlag(DaysOfWeek.Sunday)) days.Add(DayOfWeek.Sunday);
            return days;
        }

        #endregion

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
        /// Validates that auto-consume rules have quantity and unit set,
        /// and AddToShoppingList rules have the required fields.
        /// </summary>
        public static void ValidateActionType(AutomationActionType actionType, decimal? consumeQuantity, Unit? consumeUnit,
            Guid? shoppingListPublicId = null, Guid? productPublicId = null, decimal? addQuantity = null, Unit? addUnit = null)
        {
            if (actionType == AutomationActionType.AutoConsume)
            {
                if (!consumeQuantity.HasValue || consumeQuantity.Value <= 0)
                    throw new AutomationInvalidScheduleException("AutoConsume action requires ConsumeQuantity > 0");
                if (!consumeUnit.HasValue)
                    throw new AutomationInvalidScheduleException("AutoConsume action requires ConsumeUnit");
            }
            else if (actionType == AutomationActionType.AddToShoppingList)
            {
                if (!shoppingListPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires ShoppingListPublicId");
                if (!productPublicId.HasValue)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires ProductPublicId");
                if (!addQuantity.HasValue || addQuantity.Value <= 0)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires AddQuantity > 0");
                if (!addUnit.HasValue)
                    throw new AutomationInvalidScheduleException("AddToShoppingList action requires AddUnit");
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

            var context = new HomassyDbContext();
            var automations = await context.ItemAutomations
                .Where(a => a.UserId == userId.Value || (familyId.HasValue && a.FamilyId == familyId.Value))
                .OrderByDescending(a => a.IsEnabled)
                .ThenBy(a => a.NextExecutionAt)
                .ToListAsync(cancellationToken);

            var productFunctions = new ProductFunctions();
            var responses = new List<AutomationResponse>();

            foreach (var automation in automations)
            {
                var inventoryItem = automation.ProductInventoryItemId.HasValue
                    ? productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                    : null;
                var product = inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null;

                // For AddToShoppingList, resolve product directly
                if (product == null && automation.ProductId.HasValue)
                    product = productFunctions.GetProductById(automation.ProductId.Value);

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

            var context = new HomassyDbContext();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            var productFunctions = new ProductFunctions();
            var inventoryItem = automation.ProductInventoryItemId.HasValue
                ? productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                : null;
            var product = inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null;

            if (product == null && automation.ProductId.HasValue)
                product = productFunctions.GetProductById(automation.ProductId.Value);

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

            // Validate schedule
            ValidateSchedule(request.ScheduleType, request.IntervalDays, request.ScheduledDaysOfWeek, request.ScheduledDayOfMonth);
            ValidateActionType(request.ActionType, request.ConsumeQuantity, request.ConsumeUnit,
                request.ShoppingListPublicId, request.ProductPublicId, request.AddQuantity, request.AddUnit);

            var productFunctions = new ProductFunctions();
            var context = new HomassyDbContext();
            ProductInventoryItem? inventoryItem = null;
            Entities.Product.Product? productEntity = null;
            int? shoppingListId = null;
            int? productId = null;

            if (request.ActionType == AutomationActionType.AddToShoppingList)
            {
                // Resolve product
                var product = productFunctions.GetProductByPublicId(request.ProductPublicId!.Value);
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

                inventoryItem = productFunctions.GetInventoryItemByPublicId(request.InventoryItemPublicId.Value);
                if (inventoryItem == null)
                    throw new ProductInventoryItemNotFoundException();

                // Verify access
                if (inventoryItem.UserId != userId.Value &&
                    (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
                    throw new AutomationAccessDeniedException();
            }

            // Get user timezone for scheduling
            var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);
            var userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;

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
                ConsumeUnit = request.ConsumeUnit,
                AddQuantity = request.AddQuantity,
                AddUnit = request.AddUnit,
                IsEnabled = true
            };

            // Calculate next execution
            automation.NextExecutionAt = CalculateNextExecutionAt(
                automation.ScheduleType,
                automation.ScheduledTime,
                automation.IntervalDays,
                automation.ScheduledDaysOfWeek,
                automation.ScheduledDayOfMonth,
                userTimeZone);

            context.ItemAutomations.Add(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"User {userId} created automation {automation.PublicId} for action type {automation.ActionType}");

            // Record activity
            try
            {
                var product = productEntity ?? (inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null);
                await new ActivityFunctions().RecordActivityAsync(
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

            var finalProduct = productEntity ?? (inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null);
            return MapToResponse(automation, inventoryItem, finalProduct);
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

            var context = new HomassyDbContext();
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
            var consumeUnit = request.ConsumeUnit ?? automation.ConsumeUnit;
            var addQuantity = request.AddQuantity ?? automation.AddQuantity;
            var addUnit = request.AddUnit ?? automation.AddUnit;

            // Validate after merge
            ValidateSchedule(scheduleType, intervalDays, scheduledDaysOfWeek, scheduledDayOfMonth);

            // Only validate action type fields when action-type-related properties are being changed
            if (request.ActionType.HasValue || request.ConsumeQuantity.HasValue || request.ConsumeUnit.HasValue ||
                request.AddQuantity.HasValue || request.AddUnit.HasValue ||
                request.ShoppingListPublicId.HasValue || request.ProductPublicId.HasValue)
            {
                // For update, use existing entity IDs if not provided in request
                var shoppingListPublicId = request.ShoppingListPublicId ?? (automation.ShoppingListId.HasValue ? Guid.Empty : (Guid?)null);
                var productPublicId = request.ProductPublicId ?? (automation.ProductId.HasValue ? Guid.Empty : (Guid?)null);
                ValidateActionType(actionType, consumeQuantity, consumeUnit, shoppingListPublicId, productPublicId, addQuantity, addUnit);
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
                var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);
                var userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;

                automation.NextExecutionAt = CalculateNextExecutionAt(
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
                var productFunctions = new ProductFunctions();
                var inventoryItem = automation.ProductInventoryItemId.HasValue
                    ? productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                    : null;
                var product = inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null;
                if (product == null && automation.ProductId.HasValue)
                    product = productFunctions.GetProductById(automation.ProductId.Value);
                await new ActivityFunctions().RecordActivityAsync(
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

            var pf = new ProductFunctions();
            var item = automation.ProductInventoryItemId.HasValue
                ? pf.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                : null;
            var prod = item != null ? pf.GetProductById(item.ProductId) : null;
            if (prod == null && automation.ProductId.HasValue)
                prod = pf.GetProductById(automation.ProductId.Value);
            return MapToResponse(automation, item, prod);
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

            var context = new HomassyDbContext();
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

            // Record activity
            try
            {
                var productFunctions = new ProductFunctions();
                var inventoryItem = automation.ProductInventoryItemId.HasValue
                    ? productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value)
                    : null;
                var product = inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null;
                if (product == null && automation.ProductId.HasValue)
                    product = productFunctions.GetProductById(automation.ProductId.Value);
                await new ActivityFunctions().RecordActivityAsync(
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

            var context = new HomassyDbContext();
            var automation = await context.ItemAutomations
                .FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

            if (automation == null)
                throw new AutomationNotFoundException();

            if (automation.UserId != userId.Value &&
                (!familyId.HasValue || automation.FamilyId != familyId.Value))
                throw new AutomationAccessDeniedException();

            ItemAutomationExecution execution;
            var productFunctions = new ProductFunctions();

            if (automation.ActionType == AutomationActionType.AddToShoppingList)
            {
                execution = await ExecuteAddToShoppingListManualAsync(context, automation, userId.Value, familyId, request.Notes, cancellationToken);
            }
            else
            {
                if (!automation.ProductInventoryItemId.HasValue)
                    throw new AutomationInvalidScheduleException("Manual execution requires an inventory item");

                var inventoryItem = productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value);
                if (inventoryItem == null)
                    throw new ProductInventoryItemNotFoundException();

                if (inventoryItem.IsFullyConsumed)
                    throw new AutomationItemFullyConsumedException();

                execution = await ExecuteConsumptionAsync(context, automation, inventoryItem, userId.Value, familyId, request.Notes, cancellationToken);
            }

            // Recalculate next execution
            var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);
            var userTimeZone = userProfile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;

            automation.LastExecutedAt = DateTime.UtcNow;
            automation.NextExecutionAt = automation.IsEnabled
                ? CalculateNextExecutionAt(
                    automation.ScheduleType,
                    automation.ScheduledTime,
                    automation.IntervalDays,
                    automation.ScheduledDaysOfWeek,
                    automation.ScheduledDayOfMonth,
                    userTimeZone,
                    automation.LastExecutedAt)
                : null;

            context.ItemAutomations.Update(automation);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information($"User {userId} manually executed automation {automation.PublicId}");

            // Record activity
            try
            {
                string productName;
                Unit? unit;
                decimal? quantity;

                if (automation.ActionType == AutomationActionType.AddToShoppingList)
                {
                    var product = automation.ProductId.HasValue ? productFunctions.GetProductById(automation.ProductId.Value) : null;
                    productName = product?.Name ?? "Unknown";
                    unit = automation.AddUnit;
                    quantity = automation.AddQuantity;
                }
                else
                {
                    var inventoryItem = automation.ProductInventoryItemId.HasValue
                        ? productFunctions.GetInventoryItemById(automation.ProductInventoryItemId.Value) : null;
                    var product = inventoryItem != null ? productFunctions.GetProductById(inventoryItem.ProductId) : null;
                    productName = product?.Name ?? "Unknown";
                    unit = automation.ConsumeUnit;
                    quantity = execution.ConsumedQuantity;
                }

                await new ActivityFunctions().RecordActivityAsync(
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

            var productFunctions = new ProductFunctions();
            var product = productFunctions.GetProductById(automation.ProductId.Value);
            if (product == null)
                throw new AutomationProductNotFoundException();

            var quantity = automation.AddQuantity ?? 1;
            var unit = automation.AddUnit ?? Unit.Piece;

            // Create shopping list item
            var shoppingListItem = new Entities.ShoppingList.ShoppingListItem
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
        public async Task<List<AutomationExecutionResponse>> GetExecutionHistoryAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UserNotFoundException("User not found");

            var familyId = SessionInfo.GetFamilyId();

            var context = new HomassyDbContext();
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
                .Take(50)
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

            return execution;
        }

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
                IsEnabled = automation.IsEnabled,
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
    }
}
