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

    #region Concurrency Tests

    /// <summary>
    /// Runs <paramref name="body"/> <paramref name="iterations"/> times across more threads than
    /// the machine has cores, all released from one barrier.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>Parallel.For</c>. Its partitioner is free to hand every iteration to
    /// one worker on a small runner, and a run that serialises proves nothing about a lost
    /// update — it would have passed against the broken code too. Real threads plus a barrier
    /// means the contention is there whatever the core count.
    /// </remarks>
    private static void RunContended(int iterations, Action body)
    {
        var threadCount = Math.Max(8, Environment.ProcessorCount * 2);
        var start = new Barrier(threadCount);
        var remaining = iterations;

        var threads = Enumerable.Range(0, threadCount).Select(_ => new Thread(() =>
        {
            start.SignalAndWait();

            while (Interlocked.Decrement(ref remaining) >= 0)
            {
                body();
            }
        })).ToList();

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// The counter used to be a mutable field incremented inside a <c>ConcurrentDictionary</c>
    /// update delegate, which the dictionary gives no exclusivity: two requests on the same key
    /// could both read 7 and both write 8, so the recorded count drifted below the real one and
    /// the effective limit ended up higher than configured — under exactly the parallel load the
    /// limiter exists for (#82).
    /// </summary>
    [Fact]
    public void RegisterAttempt_UnderParallelLoadOnOneKey_CountsEveryAttempt()
    {
        var key = GetUniqueKey();
        const int attempts = 1000;
        const int maxAttempts = attempts * 2;   // High enough that nothing is refused.
        var window = TimeSpan.FromMinutes(5);

        RunContended(attempts, () => RateLimitService.RegisterAttempt(key, maxAttempts, window));

        var status = RateLimitService.GetRateLimitStatus(key, maxAttempts, window);

        Assert.Equal(maxAttempts - attempts, status.Remaining);
    }

    /// <summary>
    /// With the limit inside the range of the parallel run, exactly the configured number of
    /// attempts must get through — one lost increment means one extra request served.
    /// </summary>
    [Fact]
    public void RegisterAttempt_UnderParallelLoad_RefusesEverythingOverTheLimit()
    {
        var key = GetUniqueKey(1);
        const int attempts = 1000;
        const int maxAttempts = 250;
        var window = TimeSpan.FromMinutes(5);

        var allowed = 0;

        RunContended(attempts, () =>
        {
            if (!RateLimitService.RegisterAttempt(key, maxAttempts, window).IsLimited)
            {
                Interlocked.Increment(ref allowed);
            }
        });

        Assert.Equal(maxAttempts, allowed);
    }

    /// <summary>
    /// Every field of the status comes from the snapshot the attempt produced, so the headers
    /// built from it cannot contradict each other.
    /// </summary>
    [Fact]
    public void RegisterAttempt_ReturnsAStatusConsistentWithTheAttemptThatProducedIt()
    {
        var key = GetUniqueKey(2);
        const int maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        Assert.Equal(2, RateLimitService.RegisterAttempt(key, maxAttempts, window).Remaining);
        Assert.Equal(1, RateLimitService.RegisterAttempt(key, maxAttempts, window).Remaining);

        var third = RateLimitService.RegisterAttempt(key, maxAttempts, window);
        Assert.Equal(0, third.Remaining);
        Assert.False(third.IsLimited);      // The third attempt is the last allowed one.
        Assert.NotNull(third.RetryAfterSeconds);

        var fourth = RateLimitService.RegisterAttempt(key, maxAttempts, window);
        Assert.True(fourth.IsLimited);
        Assert.Equal(0, fourth.Remaining);
        Assert.Equal(third.ResetTimestamp, fourth.ResetTimestamp);
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
