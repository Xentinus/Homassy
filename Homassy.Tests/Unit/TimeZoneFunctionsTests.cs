using Homassy.API.Enums;
using Homassy.API.Functions;

namespace Homassy.Tests.Unit;

public class TimeZoneFunctionsTests
{
    #region GetTimeZoneId Tests
    [Theory]
    [InlineData(UserTimeZone.UTC, "UTC")]
    [InlineData(UserTimeZone.CentralEuropeStandardTime, "Central Europe Standard Time")]
    [InlineData(UserTimeZone.EasternStandardTime, "Eastern Standard Time")]
    [InlineData(UserTimeZone.PacificStandardTime, "Pacific Standard Time")]
    [InlineData(UserTimeZone.TokyoStandardTime, "Tokyo Standard Time")]
    public void GetTimeZoneId_ValidTimeZone_ReturnsCorrectId(UserTimeZone timeZone, string expectedId)
    {
        // Act
        var result = TimeZoneFunctions.GetTimeZoneId(timeZone);

        // Assert
        Assert.Equal(expectedId, result);
    }
    #endregion

    #region GetTimeZoneInfo Tests
    [Theory]
    [InlineData(UserTimeZone.UTC)]
    [InlineData(UserTimeZone.CentralEuropeStandardTime)]
    [InlineData(UserTimeZone.EasternStandardTime)]
    [InlineData(UserTimeZone.PacificStandardTime)]
    public void GetTimeZoneInfo_ValidTimeZone_ReturnsTimeZoneInfo(UserTimeZone timeZone)
    {
        // Act
        var result = TimeZoneFunctions.GetTimeZoneInfo(timeZone);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TimeZoneInfo>(result);
    }

    [Fact]
    public void GetTimeZoneInfo_UTC_ReturnsUtcTimeZone()
    {
        // Act
        var result = TimeZoneFunctions.GetTimeZoneInfo(UserTimeZone.UTC);

        // Assert
        Assert.Equal(TimeZoneInfo.Utc.Id, result.Id);
    }
    #endregion

    #region GetDisplayName Tests
    [Fact]
    public void GetDisplayName_ValidTimeZone_ReturnsNonEmptyString()
    {
        // Act
        var result = TimeZoneFunctions.GetDisplayName(UserTimeZone.CentralEuropeStandardTime);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData(UserTimeZone.UTC)]
    [InlineData(UserTimeZone.CentralEuropeStandardTime)]
    [InlineData(UserTimeZone.EasternStandardTime)]
    public void GetDisplayName_AllTimeZones_DoNotThrow(UserTimeZone timeZone)
    {
        // Act & Assert - should not throw
        var result = TimeZoneFunctions.GetDisplayName(timeZone);
        Assert.NotNull(result);
    }
    #endregion

    #region GetShortName Tests
    [Fact]
    public void GetShortName_ValidTimeZone_ReturnsNonEmptyString()
    {
        // Act
        var result = TimeZoneFunctions.GetShortName(UserTimeZone.CentralEuropeStandardTime);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
    #endregion

    #region ConvertUtcToUserTimeZone Tests
    [Fact]
    public void ConvertUtcToUserTimeZone_UTC_ReturnsSameTime()
    {
        // Arrange
        var utcTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = TimeZoneFunctions.ConvertUtcToUserTimeZone(utcTime, UserTimeZone.UTC);

        // Assert
        Assert.Equal(utcTime, result);
    }

    [Fact]
    public void ConvertUtcToUserTimeZone_CentralEurope_ReturnsOffsetTime()
    {
        // Arrange - Winter time (no DST)
        var utcTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = TimeZoneFunctions.ConvertUtcToUserTimeZone(utcTime, UserTimeZone.CentralEuropeStandardTime);

        // Assert - CET is UTC+1 in winter
        Assert.Equal(13, result.Hour);
    }

    [Fact]
    public void ConvertUtcToUserTimeZone_NoParameters_UsesCurrentUtcTime()
    {
        // Act
        var before = DateTime.UtcNow;
        var result = TimeZoneFunctions.ConvertUtcToUserTimeZone(UserTimeZone.UTC);
        var after = DateTime.UtcNow;

        // Assert - Result should be between before and after
        Assert.True(result >= before.AddSeconds(-1) && result <= after.AddSeconds(1));
    }

    [Fact]
    public void ConvertUtcToUserTimeZone_DifferentTimeZones_ReturnsDifferentResults()
    {
        // Arrange
        var utcTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var utcResult = TimeZoneFunctions.ConvertUtcToUserTimeZone(utcTime, UserTimeZone.UTC);
        var tokyoResult = TimeZoneFunctions.ConvertUtcToUserTimeZone(utcTime, UserTimeZone.TokyoStandardTime);
        var pacificResult = TimeZoneFunctions.ConvertUtcToUserTimeZone(utcTime, UserTimeZone.PacificStandardTime);

        // Assert - All should be different
        Assert.NotEqual(utcResult.Hour, tokyoResult.Hour);
        Assert.NotEqual(utcResult.Hour, pacificResult.Hour);
        Assert.NotEqual(tokyoResult.Hour, pacificResult.Hour);
    }
    #endregion

    #region All TimeZones Validation
    [Fact]
    public void AllUserTimeZones_HaveValidTimeZoneInfo()
    {
        // Arrange
        var allTimeZones = Enum.GetValues<UserTimeZone>();

        // Act & Assert
        foreach (var timeZone in allTimeZones)
        {
            var exception = Record.Exception(() => TimeZoneFunctions.GetTimeZoneInfo(timeZone));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void AllUserTimeZones_CanConvertTime()
    {
        // Arrange
        var utcTime = DateTime.UtcNow;
        var allTimeZones = Enum.GetValues<UserTimeZone>();

        // Act & Assert
        foreach (var timeZone in allTimeZones)
        {
            var exception = Record.Exception(() =>
                TimeZoneFunctions.ConvertUtcToUserTimeZone(utcTime, timeZone));
            Assert.Null(exception);
        }
    }
    #endregion
}
