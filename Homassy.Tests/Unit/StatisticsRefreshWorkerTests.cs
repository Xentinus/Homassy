using Homassy.API.Context;
using Homassy.API.Entities.Activity;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.Insights;
using Homassy.API.Services;
using Homassy.API.Services.Background;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Homassy.Tests.Unit;

/// <summary>
/// The nightly pre-warm pass (#109, Task 21). The global-statistics half of this worker is
/// unchanged and covered by the statistics endpoint's own tests; what is new - and what these tests
/// are about - is that the pass warms a real cache entry for every <em>active</em> family and
/// spends nothing on the inactive ones.
/// </summary>
/// <remarks>
/// Driven through a purpose-built service provider rather than the whole test host: the pass needs
/// exactly a context factory, <see cref="FamilyInsightsCache"/> and <see cref="InsightFunctions"/>,
/// and calling it directly is what lets the test observe one pass instead of waiting on a 24-hour
/// timer. The database is the same local PostgreSQL every integration test uses (via
/// <see cref="TestConfiguration"/>), because the query being tested - "which families have recent
/// activity" - is the whole point and has nothing to observe against a fake.
/// </remarks>
public class StatisticsRefreshWorkerTests
{
    private readonly ITestOutputHelper _output;

    public StatisticsRefreshWorkerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The window the pass pre-warms, and the key it lands under. Mirrors the worker's own
    /// constants; if either moves, this is where the test says so.
    /// </summary>
    private const int PreWarmedDays = 30;

    /// <summary>
    /// The cache key <c>InsightFunctions</c> builds for a scoreboard. The timezone is the caller's
    /// own - and a user inserted straight into the database has whatever profile it was given, or
    /// the same Central European default the aggregation falls back to, so both paths agree here.
    /// </summary>
    private static string ScoreboardKey() =>
        $"scoreboard:{PreWarmedDays}:{UserTimeZone.CentralEuropeStandardTime.ToTimeZoneId()}";

    private static ServiceProvider BuildProvider(FamilyInsightsCache cache)
    {
        var connectionString = TestConfiguration.Configuration.GetConnectionString("DefaultConnection");
        var services = new ServiceCollection();

        services.AddDbContextFactory<HomassyDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(cache);
        services.AddScoped<InsightFunctions>();

        return services.BuildServiceProvider();
    }

    private sealed record SeededFamily(int FamilyId, int UserId, string Email);

    /// <summary>
    /// Inserts a family with one member (and their profile), plus a single activity row at the
    /// given age - the only thing that decides whether the pass considers the family active.
    /// </summary>
    private static async Task<SeededFamily> SeedFamilyWithActivityAsync(string namePrefix, int activityAgeDays)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        var family = new Homassy.API.Entities.Family.Family { Name = $"{namePrefix} Family" };
        context.Families.Add(family);
        await context.SaveChangesAsync();

        var email = $"{namePrefix}-{Guid.NewGuid():N}@test.homassy.local";
        var user = new Homassy.API.Entities.User.User
        {
            Email = email,
            Name = $"{namePrefix} Member",
            KratosIdentityId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Status = UserStatus.Active,
            FamilyId = family.Id
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.UserProfiles.Add(new Homassy.API.Entities.User.UserProfile
        {
            UserId = user.Id,
            DisplayName = $"{namePrefix} Member",
            DefaultCurrency = Currency.Huf,
            DefaultLanguage = Language.Hungarian,
            DefaultTimeZone = UserTimeZone.CentralEuropeStandardTime
        });

        context.Activities.Add(new Activity
        {
            UserId = user.Id,
            FamilyId = family.Id,
            Timestamp = DateTime.UtcNow.AddDays(-activityAgeDays),
            ActivityType = ActivityType.ProductInventoryCreate,
            RecordId = 1,
            RecordName = "Pre-warm Test Record"
        });
        await context.SaveChangesAsync();

        return new SeededFamily(family.Id, user.Id, email);
    }

    private static async Task CleanupAsync(params SeededFamily[] families)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        foreach (var family in families)
        {
            var activities = await context.Activities.Where(a => a.UserId == family.UserId).ToListAsync();
            context.Activities.RemoveRange(activities);

            var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == family.UserId);
            if (profile != null) context.UserProfiles.Remove(profile);

            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == family.UserId);
            if (user != null) context.Users.Remove(user);

            var familyRow = await context.Families.FirstOrDefaultAsync(f => f.Id == family.FamilyId);
            if (familyRow != null) context.Families.Remove(familyRow);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Reads an entry back without computing it: the factory throws, so this returns the cached
    /// value on a hit and fails the test on a miss. <see cref="FamilyInsightsCache"/> has no
    /// "peek" of its own, and adding one for a test would widen its surface for nothing.
    /// </summary>
    private static async Task<FamilyScoreboardResponse> ReadCachedScoreboardAsync(FamilyInsightsCache cache, int familyId) =>
        await cache.GetOrAddAsync<FamilyScoreboardResponse>(
            familyId,
            ScoreboardKey(),
            TimeSpan.FromMinutes(15),
            _ => throw new InvalidOperationException($"no pre-warmed scoreboard for family {familyId}"),
            CancellationToken.None);

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var cache = new FamilyInsightsCache();
        using var provider = BuildProvider(cache);

        var exception = Record.Exception(() => new StatisticsRefreshWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new StatisticsService()));

        Assert.Null(exception);
    }

    /// <summary>
    /// Two families with recent activity each get a scoreboard entry warmed under the key a real
    /// request would look under - so the first member to open the page after the nightly pass gets
    /// a hit rather than paying for the aggregation.
    /// </summary>
    [Fact]
    public async Task PreWarmFamilyInsightsAsync_TwoActiveFamilies_WarmsAScoreboardForEach()
    {
        SeededFamily? first = null;
        SeededFamily? second = null;
        try
        {
            first = await SeedFamilyWithActivityAsync("prewarm-a", activityAgeDays: 1);
            second = await SeedFamilyWithActivityAsync("prewarm-b", activityAgeDays: 10);

            var cache = new FamilyInsightsCache();
            using var provider = BuildProvider(cache);
            var worker = new StatisticsRefreshWorker(provider.GetRequiredService<IServiceScopeFactory>(), new StatisticsService());

            await worker.PreWarmFamilyInsightsAsync(CancellationToken.None);

            var firstScoreboard = await ReadCachedScoreboardAsync(cache, first.FamilyId);
            var secondScoreboard = await ReadCachedScoreboardAsync(cache, second.FamilyId);

            // Warmed with the real aggregation, not a placeholder: each family's own single member
            // is in its own entry, with the activity that made it active counted.
            var firstMember = Assert.Single(firstScoreboard.Members);
            Assert.Equal(1, firstMember.ItemsAdded);

            var secondMember = Assert.Single(secondScoreboard.Members);
            Assert.Equal(1, secondMember.ItemsAdded);

            _output.WriteLine($"Warmed families {first.FamilyId} and {second.FamilyId}");
        }
        finally
        {
            await CleanupAsync(new[] { first, second }
                .Where(family => family != null)
                .Select(family => family!)
                .ToArray());
        }
    }

    /// <summary>
    /// The point of the active-family cutoff: a household that has done nothing for well over the
    /// window is skipped entirely, so the nightly cost scales with active families rather than with
    /// every family row ever created.
    /// </summary>
    [Fact]
    public async Task PreWarmFamilyInsightsAsync_FamilyWithNoRecentActivity_IsSkipped()
    {
        SeededFamily? active = null;
        SeededFamily? dormant = null;
        try
        {
            active = await SeedFamilyWithActivityAsync("prewarm-active", activityAgeDays: 2);
            dormant = await SeedFamilyWithActivityAsync("prewarm-dormant", activityAgeDays: 200);

            var cache = new FamilyInsightsCache();
            using var provider = BuildProvider(cache);
            var worker = new StatisticsRefreshWorker(provider.GetRequiredService<IServiceScopeFactory>(), new StatisticsService());

            await worker.PreWarmFamilyInsightsAsync(CancellationToken.None);

            // The active one is warmed...
            var warmed = await ReadCachedScoreboardAsync(cache, active.FamilyId);
            Assert.Single(warmed.Members);

            // ...and the dormant one was never computed, which is exactly the miss the throwing
            // factory above turns into an observable fact.
            var dormantFamilyId = dormant.FamilyId;
            await Assert.ThrowsAsync<InvalidOperationException>(() => ReadCachedScoreboardAsync(cache, dormantFamilyId));
        }
        finally
        {
            await CleanupAsync(new[] { active, dormant }
                .Where(family => family != null)
                .Select(family => family!)
                .ToArray());
        }
    }

    /// <summary>
    /// A pre-warm failure must never be able to take the worker down: the pass swallows and logs
    /// whatever it hits, so a provider that cannot even resolve its dependencies still returns
    /// normally. That is the "wrap the pass in its own try/catch" requirement, tested from the
    /// worst end of it.
    /// </summary>
    [Fact]
    public async Task PreWarmFamilyInsightsAsync_WhenItsDependenciesCannotBeResolved_DoesNotThrow()
    {
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var worker = new StatisticsRefreshWorker(
            emptyProvider.GetRequiredService<IServiceScopeFactory>(),
            new StatisticsService());

        var exception = await Record.ExceptionAsync(() => worker.PreWarmFamilyInsightsAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
