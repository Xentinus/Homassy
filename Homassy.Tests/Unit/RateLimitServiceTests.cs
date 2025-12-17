using Homassy.API.Services;

namespace Homassy.Tests.Unit;

public class RateLimitServiceTests : IDisposable
{
    private readonly string _testKeyPrefix = $"test_{Guid.NewGuid():N}_";

    public void Dispose()
    {
        for (int i = 0; i < 10; i++)
        {
            RateLimitService.ResetAttempts($"{_testKeyPrefix}{i}");
        }
        GC.SuppressFinalize(this);
    }

    private string GetUniqueKey(int index = 0) => $"{_testKeyPrefix}{index}";

    #region IsRateLimited Tests

    [Fact]
    public void IsRateLimited_WhenFirstAttempt_ReturnsFalse()
    {
        var key = GetUniqueKey();
        var maxAttempts = 5;
        var window = TimeSpan.FromMinutes(1);

        var result = RateLimitService.IsRateLimited(key, maxAttempts, window);

        Assert.False(result);
    }

    [Fact]
    public void IsRateLimited_WhenUnderLimit_ReturnsFalse()
    {
        var key = GetUniqueKey();
        var maxAttempts = 5;
        var window = TimeSpan.FromMinutes(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            var result = RateLimitService.IsRateLimited(key, maxAttempts, window);
            Assert.False(result);
        }
    }

    [Fact]
    public void IsRateLimited_WhenLimitExceeded_ReturnsTrue()
    {
        var key = GetUniqueKey();
        var maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            RateLimitService.IsRateLimited(key, maxAttempts, window);
        }

        var result = RateLimitService.IsRateLimited(key, maxAttempts, window);

        Assert.True(result);
    }

    [Fact]
    public void IsRateLimited_WhenExactlyAtLimit_ReturnsFalse()
    {
        var key = GetUniqueKey();
        var maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        for (int i = 0; i < maxAttempts - 1; i++)
        {
            RateLimitService.IsRateLimited(key, maxAttempts, window);
        }

        var result = RateLimitService.IsRateLimited(key, maxAttempts, window);

        Assert.False(result);
    }

    #endregion

    #region GetRateLimitStatus Tests

    [Fact]
    public void GetRateLimitStatus_WhenNoAttempts_ReturnsFullRemaining()
    {
        var key = GetUniqueKey();
        var maxAttempts = 10;
        var window = TimeSpan.FromMinutes(1);

        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(maxAttempts, status.Limit);
        Assert.Equal(maxAttempts, status.Remaining);
        Assert.Null(status.RetryAfterSeconds);
    }

    [Fact]
    public void GetRateLimitStatus_AfterOneAttempt_ReturnsDecrementedRemaining()
    {
        var key = GetUniqueKey();
        var maxAttempts = 10;
        var window = TimeSpan.FromMinutes(1);

        RateLimitService.IsRateLimited(key, maxAttempts, window);

        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(maxAttempts, status.Limit);
        Assert.Equal(maxAttempts - 1, status.Remaining);
    }

    [Fact]
    public void GetRateLimitStatus_WhenLimitExceeded_ReturnsZeroRemaining()
    {
        var key = GetUniqueKey();
        var maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        for (int i = 0; i <= maxAttempts; i++)
        {
            RateLimitService.IsRateLimited(key, maxAttempts, window);
        }

        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(maxAttempts, status.Limit);
        Assert.Equal(0, status.Remaining);
        Assert.NotNull(status.RetryAfterSeconds);
        Assert.True(status.RetryAfterSeconds > 0);
    }

    [Fact]
    public void GetRateLimitStatus_ReturnsValidResetTimestamp()
    {
        var key = GetUniqueKey();
        var maxAttempts = 10;
        var window = TimeSpan.FromMinutes(1);
        var beforeCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        RateLimitService.IsRateLimited(key, maxAttempts, window);
        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        var afterCall = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

        Assert.True(status.ResetTimestamp >= beforeCall);
        Assert.True(status.ResetTimestamp <= afterCall + 1);
    }

    [Fact]
    public void GetRateLimitStatus_WhenZeroRemaining_RetryAfterIsPositive()
    {
        var key = GetUniqueKey();
        var maxAttempts = 1;
        var window = TimeSpan.FromMinutes(1);

        RateLimitService.IsRateLimited(key, maxAttempts, window);
        RateLimitService.IsRateLimited(key, maxAttempts, window);

        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(0, status.Remaining);
        Assert.NotNull(status.RetryAfterSeconds);
        Assert.True(status.RetryAfterSeconds >= 1);
    }

    [Fact]
    public void GetRateLimitStatus_LimitMatchesMaxAttempts()
    {
        var key = GetUniqueKey();
        var maxAttempts = 42;
        var window = TimeSpan.FromMinutes(1);

        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(maxAttempts, status.Limit);
    }

    #endregion

    #region ResetAttempts Tests

    [Fact]
    public void ResetAttempts_ClearsRateLimitCounter()
    {
        var key = GetUniqueKey();
        var maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        for (int i = 0; i <= maxAttempts; i++)
        {
            RateLimitService.IsRateLimited(key, maxAttempts, window);
        }
        Assert.True(RateLimitService.IsRateLimited(key, maxAttempts, window));

        RateLimitService.ResetAttempts(key);

        var result = RateLimitService.IsRateLimited(key, maxAttempts, window);
        Assert.False(result);
    }

    [Fact]
    public void ResetAttempts_AfterReset_StatusShowsFullRemaining()
    {
        var key = GetUniqueKey();
        var maxAttempts = 10;
        var window = TimeSpan.FromMinutes(1);

        for (int i = 0; i < 5; i++)
        {
            RateLimitService.IsRateLimited(key, maxAttempts, window);
        }

        RateLimitService.ResetAttempts(key);
        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(maxAttempts, status.Remaining);
    }

    #endregion

    #region GetLockoutRemaining Tests

    [Fact]
    public void GetLockoutRemaining_WhenNoAttempts_ReturnsNull()
    {
        var key = GetUniqueKey();
        var window = TimeSpan.FromMinutes(1);

        var remaining = RateLimitService.GetLockoutRemaining(key, window);

        Assert.Null(remaining);
    }

    [Fact]
    public void GetLockoutRemaining_WhenWithinWindow_ReturnsPositiveTimeSpan()
    {
        var key = GetUniqueKey();
        var maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        RateLimitService.IsRateLimited(key, maxAttempts, window);

        var remaining = RateLimitService.GetLockoutRemaining(key, window);

        Assert.NotNull(remaining);
        Assert.True(remaining.Value.TotalSeconds > 0);
    }

    #endregion
}
