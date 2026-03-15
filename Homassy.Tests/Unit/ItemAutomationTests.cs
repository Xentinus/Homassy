using Homassy.API.Constants;
using Homassy.API.Entities.Product;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.Automation;
using ApiUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Unit;

public class ItemAutomationTests
{
    #region Entity Creation Tests

    [Fact]
    public void ItemAutomation_Creation_SetsDefaultValues()
    {
        // Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume
        };

        // Assert
        Assert.True(automation.IsEnabled);
        Assert.Null(automation.NextExecutionAt);
        Assert.Null(automation.LastExecutedAt);
        Assert.Null(automation.IntervalDays);
        Assert.Null(automation.ScheduledDaysOfWeek);
        Assert.Null(automation.ScheduledDayOfMonth);
        Assert.Null(automation.ConsumeQuantity);
        Assert.Null(automation.ConsumeUnit);
        Assert.Null(automation.FamilyId);
        Assert.Null(automation.UserId);
        Assert.False(automation.IsDeleted);
    }

    [Fact]
    public void ItemAutomation_IntervalSchedule_SetsProperties()
    {
        // Arrange & Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            IntervalDays = 7,
            ScheduledTime = new TimeOnly(9, 30),
            ActionType = AutomationActionType.AutoConsume,
            ConsumeQuantity = 1.5m,
            ConsumeUnit = ApiUnit.Piece,
            UserId = 10,
            FamilyId = 5
        };

        // Assert
        Assert.Equal(ScheduleType.Interval, automation.ScheduleType);
        Assert.Equal(7, automation.IntervalDays);
        Assert.Equal(new TimeOnly(9, 30), automation.ScheduledTime);
        Assert.Equal(AutomationActionType.AutoConsume, automation.ActionType);
        Assert.Equal(1.5m, automation.ConsumeQuantity);
        Assert.Equal(ApiUnit.Piece, automation.ConsumeUnit);
        Assert.Equal(10, automation.UserId);
        Assert.Equal(5, automation.FamilyId);
    }

    [Fact]
    public void ItemAutomation_FixedDateSchedule_SetsProperties()
    {
        // Arrange & Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 2,
            ScheduleType = ScheduleType.FixedDate,
            ScheduledDaysOfWeek = DaysOfWeek.Monday,
            ScheduledDayOfMonth = 15,
            ScheduledTime = new TimeOnly(7, 0),
            ActionType = AutomationActionType.NotifyOnly
        };

        // Assert
        Assert.Equal(ScheduleType.FixedDate, automation.ScheduleType);
        Assert.Equal(DaysOfWeek.Monday, automation.ScheduledDaysOfWeek);
        Assert.Equal(15, automation.ScheduledDayOfMonth);
        Assert.Equal(AutomationActionType.NotifyOnly, automation.ActionType);
    }

    [Fact]
    public void ItemAutomation_NotifyOnlyMode_NoConsumeQuantityRequired()
    {
        // Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            IntervalDays = 14,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.NotifyOnly
        };

        // Assert
        Assert.Equal(AutomationActionType.NotifyOnly, automation.ActionType);
        Assert.Null(automation.ConsumeQuantity);
        Assert.Null(automation.ConsumeUnit);
    }

    [Fact]
    public void ItemAutomation_SoftDelete_WorksCorrectly()
    {
        // Arrange
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume
        };

        // Act
        automation.DeleteRecord(5);

        // Assert
        Assert.True(automation.IsDeleted);
    }

    [Fact]
    public void ItemAutomation_NextExecutionAt_CanBeSet()
    {
        // Arrange
        var nextExecution = DateTime.UtcNow.AddDays(7);

        // Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume,
            NextExecutionAt = nextExecution
        };

        // Assert
        Assert.Equal(nextExecution, automation.NextExecutionAt);
    }

    [Fact]
    public void ItemAutomation_LastExecutedAt_CanBeSet()
    {
        // Arrange
        var lastExecution = DateTime.UtcNow.AddDays(-1);

        // Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume,
            LastExecutedAt = lastExecution
        };

        // Assert
        Assert.Equal(lastExecution, automation.LastExecutedAt);
    }

    #endregion

    #region ItemAutomationExecution Tests

    [Fact]
    public void ItemAutomationExecution_Creation_SetsDefaultValues()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.AutoConsumed
        };

        // Assert
        Assert.NotEqual(default, execution.ExecutedAt);
        Assert.Null(execution.ConsumedQuantity);
        Assert.Null(execution.Notes);
        Assert.Null(execution.TriggeredByUserId);
        Assert.False(execution.IsDeleted);
    }

    [Fact]
    public void ItemAutomationExecution_AutoConsumed_SetsProperties()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.AutoConsumed,
            ConsumedQuantity = 2.5m,
            Notes = "Item fully consumed"
        };

        // Assert
        Assert.Equal(AutomationExecutionStatus.AutoConsumed, execution.Status);
        Assert.Equal(2.5m, execution.ConsumedQuantity);
        Assert.Equal("Item fully consumed", execution.Notes);
    }

    [Fact]
    public void ItemAutomationExecution_NotificationSent_SetsProperties()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.NotificationSent,
            Notes = "Notification sent"
        };

        // Assert
        Assert.Equal(AutomationExecutionStatus.NotificationSent, execution.Status);
        Assert.Null(execution.ConsumedQuantity);
    }

    [Fact]
    public void ItemAutomationExecution_ManuallyConfirmed_SetsTriggeredBy()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.ManuallyConfirmed,
            ConsumedQuantity = 1m,
            TriggeredByUserId = 42
        };

        // Assert
        Assert.Equal(AutomationExecutionStatus.ManuallyConfirmed, execution.Status);
        Assert.Equal(42, execution.TriggeredByUserId);
    }

    [Fact]
    public void ItemAutomationExecution_Skipped_SetsProperties()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.Skipped,
            Notes = "Item already fully consumed"
        };

        // Assert
        Assert.Equal(AutomationExecutionStatus.Skipped, execution.Status);
    }

    [Fact]
    public void ItemAutomationExecution_Failed_SetsProperties()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.Failed,
            Notes = "Error: item not found"
        };

        // Assert
        Assert.Equal(AutomationExecutionStatus.Failed, execution.Status);
    }

    [Fact]
    public void ItemAutomationExecution_SoftDelete_WorksCorrectly()
    {
        // Arrange
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.AutoConsumed
        };

        // Act
        execution.DeleteRecord(5);

        // Assert
        Assert.True(execution.IsDeleted);
    }

    #endregion

    #region Enum Value Tests

    [Fact]
    public void ScheduleType_HasExpectedValues()
    {
        Assert.Equal(0, (int)ScheduleType.Interval);
        Assert.Equal(1, (int)ScheduleType.FixedDate);
    }

    [Fact]
    public void AutomationActionType_HasExpectedValues()
    {
        Assert.Equal(0, (int)AutomationActionType.AutoConsume);
        Assert.Equal(1, (int)AutomationActionType.NotifyOnly);
        Assert.Equal(2, (int)AutomationActionType.AddToShoppingList);
    }

    [Fact]
    public void AutomationExecutionStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)AutomationExecutionStatus.AutoConsumed);
        Assert.Equal(1, (int)AutomationExecutionStatus.NotificationSent);
        Assert.Equal(2, (int)AutomationExecutionStatus.ManuallyConfirmed);
        Assert.Equal(3, (int)AutomationExecutionStatus.Skipped);
        Assert.Equal(4, (int)AutomationExecutionStatus.Failed);
        Assert.Equal(5, (int)AutomationExecutionStatus.AddedToShoppingList);
    }

    [Fact]
    public void ActivityType_HasAutomationValues()
    {
        Assert.Equal(23, (int)ActivityType.AutomationCreate);
        Assert.Equal(24, (int)ActivityType.AutomationUpdate);
        Assert.Equal(25, (int)ActivityType.AutomationDelete);
        Assert.Equal(26, (int)ActivityType.AutomationExecute);
    }

    #endregion

    #region TableNames Constants Tests

    [Fact]
    public void TableNames_HasAutomationTables()
    {
        Assert.Equal("ItemAutomations", TableNames.ItemAutomations);
        Assert.Equal("ItemAutomationExecutions", TableNames.ItemAutomationExecutions);
    }

    #endregion

    #region Error Codes Tests

    [Fact]
    public void ErrorCodes_HasAutomationCodes()
    {
        Assert.Equal("AUTOMATION-0001", ErrorCodes.AutomationNotFound);
        Assert.Equal("AUTOMATION-0002", ErrorCodes.AutomationInvalidSchedule);
        Assert.Equal("AUTOMATION-0003", ErrorCodes.AutomationItemFullyConsumed);
        Assert.Equal("AUTOMATION-0004", ErrorCodes.AutomationAccessDenied);
        Assert.Equal("AUTOMATION-0005", ErrorCodes.AutomationInsufficientQuantity);
        Assert.Equal("AUTOMATION-0006", ErrorCodes.AutomationShoppingListNotFound);
        Assert.Equal("AUTOMATION-0007", ErrorCodes.AutomationProductNotFound);
    }

    [Fact]
    public void ErrorCodeDescriptions_HasAutomationDescriptions()
    {
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationNotFound));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationInvalidSchedule));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationItemFullyConsumed));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationAccessDenied));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationInsufficientQuantity));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationShoppingListNotFound));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationProductNotFound));
    }

    [Fact]
    public void ErrorCodeDescriptions_AutomationDescriptions_AreNotEmpty()
    {
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationNotFound));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationInvalidSchedule));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationItemFullyConsumed));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationAccessDenied));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationInsufficientQuantity));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationShoppingListNotFound));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationProductNotFound));
    }

    #endregion

    #region DTO/Model Tests

    [Fact]
    public void CreateAutomationRequest_IntervalSchedule_SetsProperties()
    {
        // Act
        var request = new CreateAutomationRequest
        {
            InventoryItemPublicId = Guid.NewGuid(),
            ScheduleType = ScheduleType.Interval,
            IntervalDays = 7,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume,
            ConsumeQuantity = 1.0m,
            ConsumeUnit = ApiUnit.Piece
        };

        // Assert
        Assert.NotEqual(Guid.Empty, request.InventoryItemPublicId);
        Assert.Equal(ScheduleType.Interval, request.ScheduleType);
        Assert.Equal(7, request.IntervalDays);
        Assert.Equal(AutomationActionType.AutoConsume, request.ActionType);
        Assert.Equal(1.0m, request.ConsumeQuantity);
        Assert.Equal(ApiUnit.Piece, request.ConsumeUnit);
        Assert.False(request.IsSharedWithFamily);
    }

    [Fact]
    public void CreateAutomationRequest_DefaultValues()
    {
        // Act
        var request = new CreateAutomationRequest
        {
            InventoryItemPublicId = Guid.NewGuid(),
            ScheduleType = ScheduleType.Interval,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.NotifyOnly
        };

        // Assert
        Assert.False(request.IsSharedWithFamily);
        Assert.Null(request.IntervalDays);
        Assert.Null(request.ScheduledDaysOfWeek);
        Assert.Null(request.ScheduledDayOfMonth);
        Assert.Null(request.ConsumeQuantity);
        Assert.Null(request.ConsumeUnit);
        Assert.Null(request.ProductPublicId);
        Assert.Null(request.ShoppingListPublicId);
        Assert.Null(request.AddQuantity);
        Assert.Null(request.AddUnit);
    }

    [Fact]
    public void UpdateAutomationRequest_AllNullable()
    {
        // Act
        var request = new UpdateAutomationRequest();

        // Assert
        Assert.Null(request.ScheduleType);
        Assert.Null(request.IntervalDays);
        Assert.Null(request.ScheduledDaysOfWeek);
        Assert.Null(request.ScheduledDayOfMonth);
        Assert.Null(request.ScheduledTime);
        Assert.Null(request.ActionType);
        Assert.Null(request.ConsumeQuantity);
        Assert.Null(request.ConsumeUnit);
        Assert.Null(request.IsEnabled);
        Assert.Null(request.ProductPublicId);
        Assert.Null(request.ShoppingListPublicId);
        Assert.Null(request.AddQuantity);
        Assert.Null(request.AddUnit);
    }

    [Fact]
    public void UpdateAutomationRequest_PartialUpdate()
    {
        // Act
        var request = new UpdateAutomationRequest
        {
            IsEnabled = false,
            IntervalDays = 30
        };

        // Assert
        Assert.False(request.IsEnabled);
        Assert.Equal(30, request.IntervalDays);
        Assert.Null(request.ScheduleType);
        Assert.Null(request.ActionType);
    }

    [Fact]
    public void AutomationResponse_SetsProperties()
    {
        // Arrange
        var publicId = Guid.NewGuid();
        var inventoryPublicId = Guid.NewGuid();
        var nextExecution = DateTime.UtcNow.AddDays(7);

        // Act
        var response = new AutomationResponse
        {
            PublicId = publicId,
            InventoryItemPublicId = inventoryPublicId,
            ProductName = "Vitamin D",
            ProductBrand = "Nature's Way",
            ScheduleType = ScheduleType.Interval,
            IntervalDays = 1,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume,
            ConsumeQuantity = 1m,
            ConsumeUnit = ApiUnit.Piece,
            IsEnabled = true,
            NextExecutionAt = nextExecution
        };

        // Assert
        Assert.Equal(publicId, response.PublicId);
        Assert.Equal(inventoryPublicId, response.InventoryItemPublicId);
        Assert.Equal("Vitamin D", response.ProductName);
        Assert.Equal("Nature's Way", response.ProductBrand);
        Assert.Equal(nextExecution, response.NextExecutionAt);
    }

    [Fact]
    public void AutomationExecutionResponse_SetsProperties()
    {
        // Arrange
        var publicId = Guid.NewGuid();
        var executedAt = DateTime.UtcNow;

        // Act
        var response = new AutomationExecutionResponse
        {
            PublicId = publicId,
            ExecutedAt = executedAt,
            Status = AutomationExecutionStatus.AutoConsumed,
            ConsumedQuantity = 2.5m,
            Notes = "Item consumed successfully"
        };

        // Assert
        Assert.Equal(publicId, response.PublicId);
        Assert.Equal(executedAt, response.ExecutedAt);
        Assert.Equal(AutomationExecutionStatus.AutoConsumed, response.Status);
        Assert.Equal(2.5m, response.ConsumedQuantity);
        Assert.Equal("Item consumed successfully", response.Notes);
    }

    [Fact]
    public void ExecuteAutomationRequest_SetsProperties()
    {
        // Act
        var request = new ExecuteAutomationRequest
        {
            Notes = "Manual trigger by user"
        };

        // Assert
        Assert.Equal("Manual trigger by user", request.Notes);
    }

    [Fact]
    public void ExecuteAutomationRequest_DefaultValues()
    {
        // Act
        var request = new ExecuteAutomationRequest();

        // Assert
        Assert.Null(request.Notes);
    }

    #endregion

    #region Navigation Property Tests

    [Fact]
    public void ItemAutomation_NavigationProperties_DefaultValues()
    {
        // Act
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.Interval,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume
        };

        // Assert - navigation properties should be null by default (loaded by EF)
        Assert.Null(automation.Executions);
    }

    [Fact]
    public void ItemAutomationExecution_NavigationProperties_DefaultValues()
    {
        // Act
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.AutoConsumed
        };

        // Assert - navigation property initialized with null! is null before EF loads it
        Assert.Null(execution.TriggeredByUserId);
    }

    [Fact]
    public void ProductInventoryItem_HasAutomationsNavigation()
    {
        // Act
        var inventoryItem = new ProductInventoryItem
        {
            ProductId = 1,
            CurrentQuantity = 10,
            Unit = ApiUnit.Piece
        };

        // Assert
        Assert.Null(inventoryItem.Automations);
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void AutomationNotFoundException_HasCorrectErrorCode()
    {
        var ex = new AutomationNotFoundException();
        Assert.Equal("AUTOMATION-0001", ex.ErrorCode);
        Assert.Equal("Automation rule not found", ex.Message);
    }

    [Fact]
    public void AutomationNotFoundException_CustomMessage()
    {
        var ex = new AutomationNotFoundException("Custom message");
        Assert.Equal("AUTOMATION-0001", ex.ErrorCode);
        Assert.Equal("Custom message", ex.Message);
    }

    [Fact]
    public void AutomationAccessDeniedException_HasCorrectErrorCode()
    {
        var ex = new AutomationAccessDeniedException();
        Assert.Equal("AUTOMATION-0004", ex.ErrorCode);
        Assert.Equal("Access denied to this automation rule", ex.Message);
    }

    [Fact]
    public void AutomationInvalidScheduleException_HasCorrectErrorCode()
    {
        var ex = new AutomationInvalidScheduleException();
        Assert.Equal("AUTOMATION-0002", ex.ErrorCode);
        Assert.Equal("Invalid automation schedule configuration", ex.Message);
    }

    [Fact]
    public void AutomationInvalidScheduleException_CustomMessage()
    {
        var ex = new AutomationInvalidScheduleException("Interval must be positive");
        Assert.Equal("AUTOMATION-0002", ex.ErrorCode);
        Assert.Equal("Interval must be positive", ex.Message);
    }

    [Fact]
    public void AutomationItemFullyConsumedException_HasCorrectErrorCode()
    {
        var ex = new AutomationItemFullyConsumedException();
        Assert.Equal("AUTOMATION-0003", ex.ErrorCode);
        Assert.Equal("Cannot execute automation: item is fully consumed", ex.Message);
    }

    [Fact]
    public void AutomationInsufficientQuantityException_HasCorrectErrorCode()
    {
        var ex = new AutomationInsufficientQuantityException();
        Assert.Equal("AUTOMATION-0005", ex.ErrorCode);
        Assert.Equal("Insufficient quantity to execute automation", ex.Message);
    }

    [Fact]
    public void AutomationShoppingListNotFoundException_HasCorrectErrorCode()
    {
        var ex = new AutomationShoppingListNotFoundException();
        Assert.Equal("AUTOMATION-0006", ex.ErrorCode);
        Assert.Equal("Shopping list not found for automation", ex.Message);
    }

    [Fact]
    public void AutomationShoppingListNotFoundException_CustomMessage()
    {
        var ex = new AutomationShoppingListNotFoundException("Custom shopping list error");
        Assert.Equal("AUTOMATION-0006", ex.ErrorCode);
        Assert.Equal("Custom shopping list error", ex.Message);
    }

    [Fact]
    public void AutomationProductNotFoundException_HasCorrectErrorCode()
    {
        var ex = new AutomationProductNotFoundException();
        Assert.Equal("AUTOMATION-0007", ex.ErrorCode);
        Assert.Equal("Product not found for automation", ex.Message);
    }

    [Fact]
    public void AutomationProductNotFoundException_CustomMessage()
    {
        var ex = new AutomationProductNotFoundException("Custom product error");
        Assert.Equal("AUTOMATION-0007", ex.ErrorCode);
        Assert.Equal("Custom product error", ex.Message);
    }

    #endregion

    #region DaysOfWeek Flags Enum Tests

    [Fact]
    public void DaysOfWeek_FlagsValues_AreCorrect()
    {
        Assert.Equal(0, (int)DaysOfWeek.None);
        Assert.Equal(1, (int)DaysOfWeek.Monday);
        Assert.Equal(2, (int)DaysOfWeek.Tuesday);
        Assert.Equal(4, (int)DaysOfWeek.Wednesday);
        Assert.Equal(8, (int)DaysOfWeek.Thursday);
        Assert.Equal(16, (int)DaysOfWeek.Friday);
        Assert.Equal(32, (int)DaysOfWeek.Saturday);
        Assert.Equal(64, (int)DaysOfWeek.Sunday);
    }

    [Fact]
    public void DaysOfWeek_CombinedFlags_WorkCorrectly()
    {
        var monWedFri = DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday;
        Assert.True(monWedFri.HasFlag(DaysOfWeek.Monday));
        Assert.True(monWedFri.HasFlag(DaysOfWeek.Wednesday));
        Assert.True(monWedFri.HasFlag(DaysOfWeek.Friday));
        Assert.False(monWedFri.HasFlag(DaysOfWeek.Tuesday));
        Assert.False(monWedFri.HasFlag(DaysOfWeek.Thursday));
        Assert.False(monWedFri.HasFlag(DaysOfWeek.Saturday));
        Assert.False(monWedFri.HasFlag(DaysOfWeek.Sunday));
    }

    [Fact]
    public void DaysOfWeek_SingleDay_WorksAsFlag()
    {
        var monday = DaysOfWeek.Monday;
        Assert.True(monday.HasFlag(DaysOfWeek.Monday));
        Assert.False(monday.HasFlag(DaysOfWeek.Tuesday));
    }

    [Fact]
    public void DaysOfWeek_AllDays_CombinedValue()
    {
        var allDays = DaysOfWeek.Monday | DaysOfWeek.Tuesday | DaysOfWeek.Wednesday
                      | DaysOfWeek.Thursday | DaysOfWeek.Friday | DaysOfWeek.Saturday | DaysOfWeek.Sunday;
        Assert.Equal(127, (int)allDays);
    }

    #endregion

    #region AddToShoppingList Entity Tests

    [Fact]
    public void ItemAutomation_AddToShoppingList_NullableInventoryItem()
    {
        var automation = new ItemAutomation
        {
            ScheduleType = ScheduleType.Interval,
            IntervalDays = 7,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AddToShoppingList,
            ProductId = 1,
            ShoppingListId = 2,
            AddQuantity = 2.0m,
            AddUnit = ApiUnit.Piece
        };

        Assert.Null(automation.ProductInventoryItemId);
        Assert.Equal(1, automation.ProductId);
        Assert.Equal(2, automation.ShoppingListId);
        Assert.Equal(2.0m, automation.AddQuantity);
        Assert.Equal(ApiUnit.Piece, automation.AddUnit);
        Assert.Equal(AutomationActionType.AddToShoppingList, automation.ActionType);
    }

    [Fact]
    public void ItemAutomation_MultiDaySchedule_SetsProperties()
    {
        var automation = new ItemAutomation
        {
            ProductInventoryItemId = 1,
            ScheduleType = ScheduleType.FixedDate,
            ScheduledDaysOfWeek = DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday,
            ScheduledTime = new TimeOnly(8, 0),
            ActionType = AutomationActionType.AutoConsume,
            ConsumeQuantity = 1m,
            ConsumeUnit = ApiUnit.Piece
        };

        Assert.Equal(DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday, automation.ScheduledDaysOfWeek);
    }

    #endregion

    #region AddToShoppingList DTO Tests

    [Fact]
    public void CreateAutomationRequest_AddToShoppingList_SetsProperties()
    {
        var productPublicId = Guid.NewGuid();
        var shoppingListPublicId = Guid.NewGuid();

        var request = new CreateAutomationRequest
        {
            ProductPublicId = productPublicId,
            ShoppingListPublicId = shoppingListPublicId,
            ScheduleType = ScheduleType.FixedDate,
            ScheduledDaysOfWeek = DaysOfWeek.Monday | DaysOfWeek.Thursday,
            ScheduledTime = new TimeOnly(7, 0),
            ActionType = AutomationActionType.AddToShoppingList,
            AddQuantity = 3m,
            AddUnit = ApiUnit.Liter
        };

        Assert.Null(request.InventoryItemPublicId);
        Assert.Equal(productPublicId, request.ProductPublicId);
        Assert.Equal(shoppingListPublicId, request.ShoppingListPublicId);
        Assert.Equal(DaysOfWeek.Monday | DaysOfWeek.Thursday, request.ScheduledDaysOfWeek);
        Assert.Equal(AutomationActionType.AddToShoppingList, request.ActionType);
        Assert.Equal(3m, request.AddQuantity);
        Assert.Equal(ApiUnit.Liter, request.AddUnit);
    }

    [Fact]
    public void UpdateAutomationRequest_AddToShoppingList_PartialUpdate()
    {
        var shoppingListPublicId = Guid.NewGuid();

        var request = new UpdateAutomationRequest
        {
            ShoppingListPublicId = shoppingListPublicId,
            AddQuantity = 5m,
            AddUnit = ApiUnit.Kilogram
        };

        Assert.Equal(shoppingListPublicId, request.ShoppingListPublicId);
        Assert.Equal(5m, request.AddQuantity);
        Assert.Equal(ApiUnit.Kilogram, request.AddUnit);
        Assert.Null(request.ScheduleType);
        Assert.Null(request.ActionType);
    }

    [Fact]
    public void AutomationResponse_AddToShoppingList_SetsProperties()
    {
        var publicId = Guid.NewGuid();
        var productPublicId = Guid.NewGuid();
        var shoppingListPublicId = Guid.NewGuid();

        var response = new AutomationResponse
        {
            PublicId = publicId,
            ProductPublicId = productPublicId,
            ShoppingListPublicId = shoppingListPublicId,
            ShoppingListName = "Weekly Groceries",
            ProductName = "Milk",
            ScheduleType = ScheduleType.FixedDate,
            ScheduledDaysOfWeek = DaysOfWeek.Monday | DaysOfWeek.Thursday,
            ScheduledTime = new TimeOnly(7, 0),
            ActionType = AutomationActionType.AddToShoppingList,
            AddQuantity = 2m,
            AddUnit = ApiUnit.Liter,
            IsEnabled = true
        };

        Assert.Null(response.InventoryItemPublicId);
        Assert.Equal(productPublicId, response.ProductPublicId);
        Assert.Equal(shoppingListPublicId, response.ShoppingListPublicId);
        Assert.Equal("Weekly Groceries", response.ShoppingListName);
        Assert.Equal(DaysOfWeek.Monday | DaysOfWeek.Thursday, response.ScheduledDaysOfWeek);
        Assert.Equal(AutomationActionType.AddToShoppingList, response.ActionType);
        Assert.Equal(2m, response.AddQuantity);
        Assert.Equal(ApiUnit.Liter, response.AddUnit);
    }

    [Fact]
    public void ItemAutomationExecution_AddedToShoppingList_SetsProperties()
    {
        var execution = new ItemAutomationExecution
        {
            ItemAutomationId = 1,
            Status = AutomationExecutionStatus.AddedToShoppingList,
            Notes = "Added 2L Milk to Weekly Groceries"
        };

        Assert.Equal(AutomationExecutionStatus.AddedToShoppingList, execution.Status);
        Assert.Equal("Added 2L Milk to Weekly Groceries", execution.Notes);
    }

    #endregion
}
