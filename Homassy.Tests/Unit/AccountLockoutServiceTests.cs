using Homassy.API.Models.ApplicationSettings;
using Homassy.API.Services;

namespace Homassy.Tests.Unit;

public class AccountLockoutServiceTests
{
    private static AccountLockoutSettings CreateDefaultSettings() => new()
    {
        MaxFailedAttempts = 5,
        LockoutDurationMinutes = 15,
        EnableProgressiveLockout = true,
        ProgressiveMultiplier = 2,
        MaxLockoutDurationMinutes = 60
    };

    [Fact]
    public void IsLockedOut_WhenLockedOutUntilIsNull_ReturnsFalse()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());

        var result = service.IsLockedOut(null);

        Assert.False(result);
    }

    [Fact]
    public void IsLockedOut_WhenLockedOutUntilIsInThePast_ReturnsFalse()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());
        var pastTime = DateTime.UtcNow.AddMinutes(-1);

        var result = service.IsLockedOut(pastTime);

        Assert.False(result);
    }

    [Fact]
    public void IsLockedOut_WhenLockedOutUntilIsInTheFuture_ReturnsTrue()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());
        var futureTime = DateTime.UtcNow.AddMinutes(10);

        var result = service.IsLockedOut(futureTime);

        Assert.True(result);
    }

    [Fact]
    public void ShouldLockout_WhenFailedAttemptsLessThanMax_ReturnsFalse()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());

        var result = service.ShouldLockout(4);

        Assert.False(result);
    }

    [Fact]
    public void ShouldLockout_WhenFailedAttemptsEqualToMax_ReturnsTrue()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());

        var result = service.ShouldLockout(5);

        Assert.True(result);
    }

    [Fact]
    public void ShouldLockout_WhenFailedAttemptsGreaterThanMax_ReturnsTrue()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());

        var result = service.ShouldLockout(10);

        Assert.True(result);
    }

    [Fact]
    public void CalculateLockoutExpiry_WhenBelowThreshold_ReturnsNull()
    {
        var service = new AccountLockoutService(CreateDefaultSettings());

        var result = service.CalculateLockoutExpiry(3);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateLockoutExpiry_WhenAtThreshold_ReturnsBaseDuration()
    {
        var settings = CreateDefaultSettings();
        var service = new AccountLockoutService(settings);
        var before = DateTime.UtcNow;

        var result = service.CalculateLockoutExpiry(5);

        Assert.NotNull(result);
        var expectedMin = before.AddMinutes(settings.LockoutDurationMinutes);
        var expectedMax = DateTime.UtcNow.AddMinutes(settings.LockoutDurationMinutes).AddSeconds(1);
        Assert.InRange(result.Value, expectedMin, expectedMax);
    }

    [Fact]
    public void CalculateLockoutExpiry_WhenProgressiveLockoutEnabled_IncreasesDuration()
    {
        var settings = CreateDefaultSettings();
        var service = new AccountLockoutService(settings);
        var before = DateTime.UtcNow;

        var result = service.CalculateLockoutExpiry(10);

        Assert.NotNull(result);
        var expectedMinMinutes = settings.LockoutDurationMinutes * settings.ProgressiveMultiplier;
        var expectedMin = before.AddMinutes(expectedMinMinutes);
        Assert.True(result.Value >= expectedMin.AddSeconds(-1));
    }

    [Fact]
    public void CalculateLockoutExpiry_WhenProgressiveLockoutDisabled_UsesBaseDuration()
    {
        var settings = CreateDefaultSettings();
        settings.EnableProgressiveLockout = false;
        var service = new AccountLockoutService(settings);
        var before = DateTime.UtcNow;

        var result = service.CalculateLockoutExpiry(15);

        Assert.NotNull(result);
        var expectedMin = before.AddMinutes(settings.LockoutDurationMinutes);
        var expectedMax = DateTime.UtcNow.AddMinutes(settings.LockoutDurationMinutes).AddSeconds(1);
        Assert.InRange(result.Value, expectedMin, expectedMax);
    }

    [Fact]
    public void CalculateLockoutExpiry_DoesNotExceedMaxLockoutDuration()
    {
        var settings = CreateDefaultSettings();
        settings.MaxLockoutDurationMinutes = 30;
        var service = new AccountLockoutService(settings);
        var before = DateTime.UtcNow;

        var result = service.CalculateLockoutExpiry(100);

        Assert.NotNull(result);
        var maxExpected = before.AddMinutes(settings.MaxLockoutDurationMinutes).AddSeconds(1);
        Assert.True(result.Value <= maxExpected);
    }

    [Fact]
    public void GetMaxFailedAttempts_ReturnsConfiguredValue()
    {
        var settings = CreateDefaultSettings();
        settings.MaxFailedAttempts = 10;
        var service = new AccountLockoutService(settings);

        var result = service.GetMaxFailedAttempts();

        Assert.Equal(10, result);
    }

    [Fact]
    public void GetRemainingAttempts_WhenNoFailedAttempts_ReturnsMaxAttempts()
    {
        var settings = CreateDefaultSettings();
        var service = new AccountLockoutService(settings);

        var result = service.GetRemainingAttempts(0);

        Assert.Equal(settings.MaxFailedAttempts, result);
    }

    [Fact]
    public void GetRemainingAttempts_WhenSomeFailedAttempts_ReturnsCorrectValue()
    {
        var settings = CreateDefaultSettings();
        var service = new AccountLockoutService(settings);

        var result = service.GetRemainingAttempts(3);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetRemainingAttempts_WhenExceedsMax_ReturnsZero()
    {
        var settings = CreateDefaultSettings();
        var service = new AccountLockoutService(settings);

        var result = service.GetRemainingAttempts(10);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void ShouldLockout_VariousAttempts_ReturnsExpectedResult(int attempts, bool expectedResult)
    {
        var service = new AccountLockoutService(CreateDefaultSettings());

        var result = service.ShouldLockout(attempts);

        Assert.Equal(expectedResult, result);
    }
}
