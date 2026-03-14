using Homassy.API.Constants;
using Homassy.API.Entities.Product;
using Homassy.API.Enums;
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
        Assert.Null(automation.ScheduledDayOfWeek);
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
            ScheduledDayOfWeek = DayOfWeek.Monday,
            ScheduledDayOfMonth = 15,
            ScheduledTime = new TimeOnly(7, 0),
            ActionType = AutomationActionType.NotifyOnly
        };

        // Assert
        Assert.Equal(ScheduleType.FixedDate, automation.ScheduleType);
        Assert.Equal(DayOfWeek.Monday, automation.ScheduledDayOfWeek);
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
    }

    [Fact]
    public void AutomationExecutionStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)AutomationExecutionStatus.AutoConsumed);
        Assert.Equal(1, (int)AutomationExecutionStatus.NotificationSent);
        Assert.Equal(2, (int)AutomationExecutionStatus.ManuallyConfirmed);
        Assert.Equal(3, (int)AutomationExecutionStatus.Skipped);
        Assert.Equal(4, (int)AutomationExecutionStatus.Failed);
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
    }

    [Fact]
    public void ErrorCodeDescriptions_HasAutomationDescriptions()
    {
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationNotFound));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationInvalidSchedule));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationItemFullyConsumed));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationAccessDenied));
        Assert.True(ErrorCodeDescriptions.Descriptions.ContainsKey(ErrorCodes.AutomationInsufficientQuantity));
    }

    [Fact]
    public void ErrorCodeDescriptions_AutomationDescriptions_AreNotEmpty()
    {
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationNotFound));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationInvalidSchedule));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationItemFullyConsumed));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationAccessDenied));
        Assert.NotEmpty(ErrorCodeDescriptions.GetDescription(ErrorCodes.AutomationInsufficientQuantity));
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
        Assert.Null(request.ScheduledDayOfWeek);
        Assert.Null(request.ScheduledDayOfMonth);
        Assert.Null(request.ConsumeQuantity);
        Assert.Null(request.ConsumeUnit);
    }

    [Fact]
    public void UpdateAutomationRequest_AllNullable()
    {
        // Act
        var request = new UpdateAutomationRequest();

        // Assert
        Assert.Null(request.ScheduleType);
        Assert.Null(request.IntervalDays);
        Assert.Null(request.ScheduledDayOfWeek);
        Assert.Null(request.ScheduledDayOfMonth);
        Assert.Null(request.ScheduledTime);
        Assert.Null(request.ActionType);
        Assert.Null(request.ConsumeQuantity);
        Assert.Null(request.ConsumeUnit);
        Assert.Null(request.IsEnabled);
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
}
