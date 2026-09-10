using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Services;
using Homassy.API.Services.Background;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Homassy.Tests.Unit;

/// <summary>
/// The write half of #127's last-seen tracking: the flush service turns the tracker's in-memory
/// stamps into <c>UserProfiles.LastSeenAt</c> values.
/// </summary>
/// <remarks>
/// Driven directly rather than through the hosted-service timer - the interval is five minutes, and
/// a test that waited for it would be a test of <c>Task.Delay</c>. The database is the same local
/// PostgreSQL the integration tests use, because <c>ExecuteUpdateAsync</c> is the thing under test
/// and it has nothing to prove against a fake.
/// </remarks>
public class LastSeenFlushServiceTests
{
    private static ServiceProvider BuildProvider()
    {
        var connectionString = TestConfiguration.Configuration.GetConnectionString("DefaultConnection");
        var services = new ServiceCollection();
        services.AddDbContextFactory<HomassyDbContext>(options => options.UseNpgsql(connectionString));
        return services.BuildServiceProvider();
    }

    private sealed record SeededUser(int UserId, int ProfileId);

    private static async Task<SeededUser> SeedUserAsync(string namePrefix)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        var user = new Homassy.API.Entities.User.User
        {
            Email = $"{namePrefix}-{Guid.NewGuid():N}@test.homassy.local",
            Name = $"{namePrefix} User",
            KratosIdentityId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Status = UserStatus.Active
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var profile = new Homassy.API.Entities.User.UserProfile
        {
            UserId = user.Id,
            DisplayName = $"{namePrefix} User",
            DefaultCurrency = Currency.Huf,
            DefaultLanguage = Language.Hungarian,
            DefaultTimeZone = UserTimeZone.CentralEuropeStandardTime
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        return new SeededUser(user.Id, profile.Id);
    }

    private static async Task<DateTime?> ReadLastSeenAsync(int userId)
    {
        using var context = TestConfiguration.DbContextFactory.CreateForReading();
        return await context.UserProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.LastSeenAt)
            .FirstOrDefaultAsync();
    }

    private static async Task CleanupAsync(params SeededUser[] users)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        foreach (var user in users)
        {
            var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId);
            if (profile != null) context.UserProfiles.Remove(profile);

            var row = await context.Users.FirstOrDefaultAsync(u => u.Id == user.UserId);
            if (row != null) context.Users.Remove(row);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Two users stamped at two different instants each get their own value written - the reason the
    /// flush groups by timestamp rather than writing one representative time per batch.
    /// </summary>
    [Fact]
    public async Task FlushAsync_WritesEachUsersOwnStamp()
    {
        SeededUser? first = null;
        SeededUser? second = null;
        try
        {
            first = await SeedUserAsync("lastseen-first");
            second = await SeedUserAsync("lastseen-second");

            // Whole seconds: PostgreSQL stores microseconds and .NET ticks are finer, so a
            // sub-second stamp would come back very slightly different and make the assertion about
            // precision rather than about the flush.
            var firstSeenAt = new DateTime(2026, 9, 10, 8, 30, 0, DateTimeKind.Utc);
            var secondSeenAt = new DateTime(2026, 9, 10, 9, 45, 0, DateTimeKind.Utc);

            var tracker = new LastSeenTracker();
            tracker.Stamp(first.UserId, firstSeenAt);
            tracker.Stamp(second.UserId, secondSeenAt);

            using var provider = BuildProvider();
            var service = new LastSeenFlushService(provider.GetRequiredService<IServiceScopeFactory>(), tracker);

            await service.FlushAsync(CancellationToken.None);

            Assert.Equal(firstSeenAt, await ReadLastSeenAsync(first.UserId));
            Assert.Equal(secondSeenAt, await ReadLastSeenAsync(second.UserId));
        }
        finally
        {
            await CleanupAsync(new[] { first, second }.Where(u => u != null).Select(u => u!).ToArray());
        }
    }

    /// <summary>
    /// The flush drains, so a second flush with nothing new writes nothing - and specifically does
    /// not re-write (or blank) the value it wrote a moment ago.
    /// </summary>
    [Fact]
    public async Task FlushAsync_CalledTwice_DoesNotRewriteTheSameStamp()
    {
        SeededUser? user = null;
        try
        {
            user = await SeedUserAsync("lastseen-drain");
            var seenAt = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            var tracker = new LastSeenTracker();
            tracker.Stamp(user.UserId, seenAt);

            using var provider = BuildProvider();
            var service = new LastSeenFlushService(provider.GetRequiredService<IServiceScopeFactory>(), tracker);

            await service.FlushAsync(CancellationToken.None);
            await service.FlushAsync(CancellationToken.None);

            Assert.Equal(seenAt, await ReadLastSeenAsync(user.UserId));
        }
        finally
        {
            await CleanupAsync(new[] { user }.Where(u => u != null).Select(u => u!).ToArray());
        }
    }

    /// <summary>
    /// A later stamp moves the stored value forward on the next flush - the ordinary case, and the
    /// one that proves the column is not written once and forgotten.
    /// </summary>
    [Fact]
    public async Task FlushAsync_AfterALaterStamp_MovesTheStoredValueForward()
    {
        SeededUser? user = null;
        try
        {
            user = await SeedUserAsync("lastseen-forward");
            var firstSeenAt = new DateTime(2026, 9, 10, 11, 0, 0, DateTimeKind.Utc);
            var laterSeenAt = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

            var tracker = new LastSeenTracker();
            using var provider = BuildProvider();
            var service = new LastSeenFlushService(provider.GetRequiredService<IServiceScopeFactory>(), tracker);

            tracker.Stamp(user.UserId, firstSeenAt);
            await service.FlushAsync(CancellationToken.None);

            tracker.Stamp(user.UserId, laterSeenAt);
            await service.FlushAsync(CancellationToken.None);

            Assert.Equal(laterSeenAt, await ReadLastSeenAsync(user.UserId));
        }
        finally
        {
            await CleanupAsync(new[] { user }.Where(u => u != null).Select(u => u!).ToArray());
        }
    }

    /// <summary>
    /// A stamp for a user with no profile row cannot fail the flush: <c>ExecuteUpdateAsync</c>
    /// simply matches nothing. Worth pinning, because the tracker takes a user id from the request
    /// path and a profile is optional on <c>User</c>.
    /// </summary>
    [Fact]
    public async Task FlushAsync_StampForAUserWithNoProfile_IsHarmless()
    {
        var tracker = new LastSeenTracker();
        tracker.Stamp(int.MaxValue, DateTime.UtcNow);

        using var provider = BuildProvider();
        var service = new LastSeenFlushService(provider.GetRequiredService<IServiceScopeFactory>(), tracker);

        var exception = await Record.ExceptionAsync(() => service.FlushAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_WithNothingPending_DoesNotEvenOpenAContext()
    {
        var tracker = new LastSeenTracker();
        // A provider with no DbContext registered at all: if the flush tried to resolve one with
        // nothing pending, this would throw instead of returning.
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var service = new LastSeenFlushService(emptyProvider.GetRequiredService<IServiceScopeFactory>(), tracker);

        var exception = await Record.ExceptionAsync(() => service.FlushAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
