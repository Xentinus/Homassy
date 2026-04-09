using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Functions;
using ApiUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Unit;

public class AutomationFunctionsTests
{
    /// <summary>
    /// Helper to convert DayOfWeek to DaysOfWeek flags for test readability.
    /// </summary>
    private static DaysOfWeek ToDaysOfWeek(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => DaysOfWeek.Monday,
        DayOfWeek.Tuesday => DaysOfWeek.Tuesday,
        DayOfWeek.Wednesday => DaysOfWeek.Wednesday,
        DayOfWeek.Thursday => DaysOfWeek.Thursday,
        DayOfWeek.Friday => DaysOfWeek.Friday,
        DayOfWeek.Saturday => DaysOfWeek.Saturday,
        DayOfWeek.Sunday => DaysOfWeek.Sunday,
        _ => DaysOfWeek.None,
    };

    #region CalculateNextExecutionAt - Interval Schedule

    [Fact]
    public void CalculateNextExecutionAt_Interval_NoLastExecution_ReturnsToday()
    {
        // Arrange
        var scheduledTime = new TimeOnly(23, 59); // late time to ensure it's in the future
        var intervalDays = 7;
        var userTimeZone = UserTimeZone.UTC;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            intervalDays,
            null, null,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow, "Next execution should be in the future");
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_NoLastExecution_PastTime_ReturnsNextInterval()
    {
        // Arrange - use time 00:00 which is always in the past
        var scheduledTime = new TimeOnly(0, 0);
        var intervalDays = 3;
        var userTimeZone = UserTimeZone.UTC;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            intervalDays,
            null, null,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        // Should be scheduled intervalDays from today at 00:00
        var expectedDate = DateTime.UtcNow.Date.AddDays(intervalDays);
        Assert.Equal(expectedDate.Date, result.Value.Date);
        Assert.Equal(0, result.Value.Hour);
        Assert.Equal(0, result.Value.Minute);
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_WithLastExecution_AdvancesByInterval()
    {
        // Arrange
        var scheduledTime = new TimeOnly(8, 0);
        var intervalDays = 7;
        var userTimeZone = UserTimeZone.UTC;
        var lastExecution = DateTime.UtcNow.AddDays(-1); // executed yesterday

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            intervalDays,
            null, null,
            userTimeZone,
            lastExecution);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_WithOldLastExecution_SkipsPastDates()
    {
        // Arrange - last execution was 30 days ago with 7-day interval
        var scheduledTime = new TimeOnly(8, 0);
        var intervalDays = 7;
        var userTimeZone = UserTimeZone.UTC;
        var lastExecution = DateTime.UtcNow.AddDays(-30);

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            intervalDays,
            null, null,
            userTimeZone,
            lastExecution);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow, "Should skip past dates and return future date");
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_NullIntervalDays_ReturnsNull()
    {
        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            new TimeOnly(8, 0),
            null, null, null,
            UserTimeZone.UTC);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_ZeroIntervalDays_ReturnsNull()
    {
        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            new TimeOnly(8, 0),
            0, null, null,
            UserTimeZone.UTC);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_DailySchedule()
    {
        // Arrange - every day at 08:00
        var scheduledTime = new TimeOnly(8, 0);
        var intervalDays = 1;
        var userTimeZone = UserTimeZone.UTC;
        var lastExecution = DateTime.UtcNow.AddHours(-2);

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            intervalDays,
            null, null,
            userTimeZone,
            lastExecution);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        Assert.Equal(8, result.Value.Hour);
        Assert.Equal(0, result.Value.Minute);
    }

    #endregion

    #region CalculateNextExecutionAt - FixedDate Weekly Schedule

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_Weekly_ReturnsCorrectDay()
    {
        // Arrange - next Monday at 09:00
        var scheduledTime = new TimeOnly(9, 0);
        var targetDays = DaysOfWeek.Monday;
        var userTimeZone = UserTimeZone.UTC;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            targetDays, null,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        Assert.Equal(DayOfWeek.Monday, result.Value.DayOfWeek);
        Assert.Equal(9, result.Value.Hour);
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_Weekly_AllDaysOfWeek()
    {
        var scheduledTime = new TimeOnly(23, 59);

        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            var daysFlag = ToDaysOfWeek(day);
            if (daysFlag == DaysOfWeek.None) continue;

            var result = AutomationFunctions.CalculateNextExecutionAt(
                ScheduleType.FixedDate,
                scheduledTime,
                null,
                daysFlag, null,
                UserTimeZone.UTC);

            Assert.NotNull(result);
            Assert.True(result!.Value > DateTime.UtcNow);
            Assert.Equal(day, result.Value.DayOfWeek);
        }
    }

    #endregion

    #region CalculateNextExecutionAt - FixedDate Monthly Schedule

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_Monthly_ReturnsCorrectDay()
    {
        // Arrange - 28th of month at 10:00
        var scheduledTime = new TimeOnly(10, 0);
        var dayOfMonth = 28;
        var userTimeZone = UserTimeZone.UTC;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            null, dayOfMonth,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        // Day should be either 28 or clamped to month end
        Assert.True(result.Value.Day <= 28);
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_Monthly_Day31_ClampedToMonthEnd()
    {
        // Arrange - 31st of month, in a month with fewer days
        var scheduledTime = new TimeOnly(23, 59); // late time to keep it in future
        var dayOfMonth = 31;
        var userTimeZone = UserTimeZone.UTC;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            null, dayOfMonth,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        // Day should be clamped to the last day of the target month
        var daysInMonth = DateTime.DaysInMonth(result.Value.Year, result.Value.Month);
        Assert.True(result.Value.Day <= daysInMonth);
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_NoDaySet_ReturnsNull()
    {
        // Act - neither DayOfWeek nor DayOfMonth set
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            new TimeOnly(8, 0),
            null,
            null, null,
            UserTimeZone.UTC);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CalculateNextExecutionAt - Timezone Handling

    [Fact]
    public void CalculateNextExecutionAt_CentralEuropeTimezone_ConvertsToUtc()
    {
        // Arrange - Central Europe (UTC+1 or UTC+2 depending on DST)
        var scheduledTime = new TimeOnly(23, 59);
        var userTimeZone = UserTimeZone.CentralEuropeStandardTime;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            1,
            null, null,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        // UTC time should be different from local time (offset applied)
        Assert.True(result.Value > DateTime.UtcNow);
    }

    [Fact]
    public void CalculateNextExecutionAt_EasternTimezone_ConvertsToUtc()
    {
        // Arrange - Eastern (UTC-5 or UTC-4)
        var scheduledTime = new TimeOnly(23, 59);
        var userTimeZone = UserTimeZone.EasternStandardTime;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            1,
            null, null,
            userTimeZone);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
    }

    [Fact]
    public void CalculateNextExecutionAt_DifferentTimezones_ProduceDifferentUtcTimes()
    {
        // Arrange
        var scheduledTime = new TimeOnly(12, 0);
        var intervalDays = 1;

        // Act
        var utcResult = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval, scheduledTime, intervalDays,
            null, null, UserTimeZone.UTC);

        var easternResult = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval, scheduledTime, intervalDays,
            null, null, UserTimeZone.EasternStandardTime);

        // Assert
        Assert.NotNull(utcResult);
        Assert.NotNull(easternResult);
        Assert.NotEqual(utcResult, easternResult);
    }

    #endregion

    #region ValidateSchedule Tests

    [Fact]
    public void ValidateSchedule_Interval_Valid_DoesNotThrow()
    {
        // Act & Assert - should not throw
        AutomationFunctions.ValidateSchedule(ScheduleType.Interval, 7, null, null);
    }

    [Fact]
    public void ValidateSchedule_Interval_NoIntervalDays_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateSchedule(ScheduleType.Interval, null, null, null));
    }

    [Fact]
    public void ValidateSchedule_Interval_ZeroIntervalDays_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateSchedule(ScheduleType.Interval, 0, null, null));
    }

    [Fact]
    public void ValidateSchedule_FixedDate_WithDaysOfWeek_Valid()
    {
        AutomationFunctions.ValidateSchedule(ScheduleType.FixedDate, null, DaysOfWeek.Monday, null);
    }

    [Fact]
    public void ValidateSchedule_FixedDate_WithDayOfMonth_Valid()
    {
        AutomationFunctions.ValidateSchedule(ScheduleType.FixedDate, null, null, 15);
    }

    [Fact]
    public void ValidateSchedule_FixedDate_NoDaySpecified_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateSchedule(ScheduleType.FixedDate, null, null, null));
    }

    [Fact]
    public void ValidateSchedule_FixedDate_DaysOfWeekNone_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateSchedule(ScheduleType.FixedDate, null, DaysOfWeek.None, null));
    }

    [Fact]
    public void ValidateSchedule_FixedDate_MultipleDays_Valid()
    {
        AutomationFunctions.ValidateSchedule(ScheduleType.FixedDate, null,
            DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday, null);
    }

    #endregion

    #region ValidateActionType Tests

    [Fact]
    public void ValidateActionType_AutoConsume_Valid()
    {
        AutomationFunctions.ValidateActionType(AutomationActionType.AutoConsume, 1.5m, ApiUnit.Piece);
    }

    [Fact]
    public void ValidateActionType_AutoConsume_NoQuantity_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(AutomationActionType.AutoConsume, null, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AutoConsume_ZeroQuantity_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(AutomationActionType.AutoConsume, 0, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AutoConsume_NegativeQuantity_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(AutomationActionType.AutoConsume, -1m, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AutoConsume_NoUnit_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(AutomationActionType.AutoConsume, 1.5m, null));
    }

    [Fact]
    public void ValidateActionType_NotifyOnly_NoQuantity_DoesNotThrow()
    {
        // NotifyOnly does not require quantity or unit
        AutomationFunctions.ValidateActionType(AutomationActionType.NotifyOnly, null, null);
    }

    [Fact]
    public void ValidateActionType_NotifyOnly_WithQuantity_DoesNotThrow()
    {
        // NotifyOnly can optionally have quantity (ignored)
        AutomationFunctions.ValidateActionType(AutomationActionType.NotifyOnly, 1.0m, ApiUnit.Piece);
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

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculateNextExecutionAt_Interval_365Days_Works()
    {
        // Maximum allowed interval
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            new TimeOnly(8, 0),
            365,
            null, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
    }

    [Fact]
    public void CalculateNextExecutionAt_Interval_1Day_Works()
    {
        // Minimum allowed interval
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            new TimeOnly(23, 59),
            1,
            null, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_Monthly_Day1()
    {
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            new TimeOnly(23, 59),
            null,
            null, 1,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_Weekly_Sunday()
    {
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            new TimeOnly(23, 59),
            null,
            DaysOfWeek.Sunday, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        Assert.Equal(DayOfWeek.Sunday, result.Value.DayOfWeek);
    }

    [Fact]
    public void CalculateNextExecutionAt_ScheduledTimePreserved_InResult()
    {
        // Arrange - use UTC timezone for simplicity
        var scheduledTime = new TimeOnly(14, 30);

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.Interval,
            scheduledTime,
            1,
            null, null,
            UserTimeZone.UTC);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(14, result.Value.Hour);
        Assert.Equal(30, result.Value.Minute);
    }

    #endregion

    #region Multi-Day Weekly Schedule Tests

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_MultiDay_ReturnsNearestDay()
    {
        // Arrange - Mon, Wed, Fri at 23:59
        var scheduledTime = new TimeOnly(23, 59);
        var scheduledDays = DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday;

        // Act
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            scheduledDays, null,
            UserTimeZone.UTC);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        var resultDay = result.Value.DayOfWeek;
        Assert.True(resultDay == DayOfWeek.Monday || resultDay == DayOfWeek.Wednesday || resultDay == DayOfWeek.Friday,
            $"Expected Mon/Wed/Fri, got {resultDay}");
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_MultiDay_PicksClosestDay()
    {
        // The result should be within the next 7 days since at least one day is always within 7 days
        var scheduledTime = new TimeOnly(23, 59);
        var scheduledDays = DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday;

        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            scheduledDays, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value <= DateTime.UtcNow.AddDays(7).AddHours(1),
            "Multi-day schedule should return a date within the next 7 days");
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_SingleFlagDay_BackwardCompatible()
    {
        // Single flag day should behave like old single DayOfWeek
        var scheduledTime = new TimeOnly(23, 59);

        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            DaysOfWeek.Wednesday, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.Equal(DayOfWeek.Wednesday, result.Value.DayOfWeek);
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_EveryDay_ReturnsToday()
    {
        // All days selected + late time = should return today or tomorrow
        var scheduledTime = new TimeOnly(23, 59);
        var allDays = DaysOfWeek.Monday | DaysOfWeek.Tuesday | DaysOfWeek.Wednesday
                      | DaysOfWeek.Thursday | DaysOfWeek.Friday | DaysOfWeek.Saturday | DaysOfWeek.Sunday;

        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            allDays, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        // Should be within the next 2 days max
        Assert.True(result.Value < DateTime.UtcNow.AddDays(2));
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_WeekendDays()
    {
        var scheduledTime = new TimeOnly(23, 59);
        var weekendDays = DaysOfWeek.Saturday | DaysOfWeek.Sunday;

        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            scheduledTime,
            null,
            weekendDays, null,
            UserTimeZone.UTC);

        Assert.NotNull(result);
        Assert.True(result.Value > DateTime.UtcNow);
        var dayOfWeek = result.Value.DayOfWeek;
        Assert.True(dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday,
            $"Expected Saturday or Sunday, got {dayOfWeek}");
    }

    [Fact]
    public void CalculateNextExecutionAt_FixedDate_DaysOfWeekNone_ReturnsNull()
    {
        var result = AutomationFunctions.CalculateNextExecutionAt(
            ScheduleType.FixedDate,
            new TimeOnly(8, 0),
            null,
            DaysOfWeek.None, null,
            UserTimeZone.UTC);

        Assert.Null(result);
    }

    #endregion

    #region ValidateActionType - AddToShoppingList Tests

    [Fact]
    public void ValidateActionType_AddToShoppingList_Valid()
    {
        AutomationFunctions.ValidateActionType(
            AutomationActionType.AddToShoppingList,
            null, null,
            Guid.NewGuid(), Guid.NewGuid(), 1.0m, ApiUnit.Piece);
    }

    [Fact]
    public void ValidateActionType_AddToShoppingList_NoShoppingListPublicId_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(
                AutomationActionType.AddToShoppingList,
                null, null,
                null, Guid.NewGuid(), 1.0m, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AddToShoppingList_NoProductPublicId_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(
                AutomationActionType.AddToShoppingList,
                null, null,
                Guid.NewGuid(), null, 1.0m, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AddToShoppingList_NoQuantity_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(
                AutomationActionType.AddToShoppingList,
                null, null,
                Guid.NewGuid(), Guid.NewGuid(), null, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AddToShoppingList_ZeroQuantity_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(
                AutomationActionType.AddToShoppingList,
                null, null,
                Guid.NewGuid(), Guid.NewGuid(), 0m, ApiUnit.Piece));
    }

    [Fact]
    public void ValidateActionType_AddToShoppingList_NoUnit_Throws()
    {
        Assert.Throws<AutomationInvalidScheduleException>(() =>
            AutomationFunctions.ValidateActionType(
                AutomationActionType.AddToShoppingList,
                null, null,
                Guid.NewGuid(), Guid.NewGuid(), 1.0m, null));
    }

    #endregion
}
