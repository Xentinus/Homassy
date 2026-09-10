using System.Net;
using System.Net.Http.Json;
using Homassy.API.Constants;
using Homassy.API.Entities.Activity;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.Insights;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// Integration tests for <c>GET api/v1.0/insights/badges</c> - the #109 endpoint that evaluates the
/// badge catalog against a member's own history and records the unlocks.
/// </summary>
/// <remarks>
/// The test this file exists for is
/// <see cref="GetBadges_CalledTwice_ReportsJustUnlockedOnlyOnce"/>. Everything else about a badge
/// can be recomputed at will; "has this member already been told" cannot, and getting it wrong
/// means the unlock celebration fires on every single page load.
/// </remarks>
public class BadgeTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public BadgeTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Helpers
    /// <summary>
    /// The lowest-threshold badge for one metric - the one a test can cross without seeding
    /// hundreds of rows. Read from the catalog rather than hard-coded, so re-tiering the catalog
    /// re-points these tests instead of breaking them.
    /// </summary>
    private static BadgeDefinition FirstBadgeFor(BadgeMetric metric) =>
        BadgeCatalog.All.Where(badge => badge.Metric == metric).OrderBy(badge => badge.Threshold).First();

    /// <summary>
    /// Inserts <paramref name="count"/> activity rows for the user - the counters badges are
    /// evaluated against are lifetime activity counts, so this is the whole of "do the thing N
    /// times" without driving N real endpoint calls.
    /// </summary>
    private async Task AddActivitiesAsync(int userId, ActivityType activityType, int count)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        for (var i = 0; i < count; i++)
        {
            context.Activities.Add(new Activity
            {
                UserId = userId,
                FamilyId = null,
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                ActivityType = activityType,
                RecordId = 1,
                RecordName = "Badge Test Record"
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<int> CountBadgeRowsAsync(int userId, string badgeId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        return await context.UserBadges.CountAsync(badge => badge.UserId == userId && badge.BadgeId == badgeId);
    }

    /// <summary>
    /// Inserts a badge row directly, for the "id in the database that the catalog no longer
    /// defines" case - the catalog is code, so a retired id cannot be produced any other way.
    /// </summary>
    private async Task AddRawBadgeRowAsync(int userId, string badgeId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        context.UserBadges.Add(new Homassy.API.Entities.User.UserBadge
        {
            UserId = userId,
            BadgeId = badgeId,
            EarnedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private async Task<BadgeStateResponse> GetBadgesAsync()
    {
        var response = await _client.GetAsync("/api/v1.0/insights/badges");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<BadgeStateResponse>>();
        Assert.NotNull(content?.Data);
        return content.Data;
    }
    #endregion

    [Fact]
    public async Task GetBadges_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/insights/badges");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A brand-new member: every badge in the catalog is returned, all locked, each with a truthful
    /// (zero) progress and its threshold - a locked badge still says what it is and what it takes,
    /// because a mystery box tells the family nothing.
    /// </summary>
    [Fact]
    public async Task GetBadges_UserBelowEveryThreshold_ReturnsThemAllLockedWithTruthfulProgress()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("badges-locked");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var badges = await GetBadgesAsync();

            Assert.Equal(BadgeCatalog.All.Count, badges.Badges.Count);
            Assert.All(badges.Badges, badge =>
            {
                Assert.Null(badge.EarnedAt);
                Assert.False(badge.JustUnlocked);
                Assert.Equal(0, badge.Progress);
                Assert.True(badge.Threshold > 0);
                // The fallback text has to be on every badge, earned or not: it is what a client
                // with no translation for this badge actually renders.
                Assert.False(string.IsNullOrWhiteSpace(badge.FallbackTitle));
                Assert.False(string.IsNullOrWhiteSpace(badge.FallbackDescription));
            });
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Crossing a threshold earns the badge on the spot: the response that first observes it carries
    /// both <c>EarnedAt</c> and <c>JustUnlocked</c>, so the client can celebrate and render the
    /// earned state from the same payload.
    /// </summary>
    [Fact]
    public async Task GetBadges_CrossingAThreshold_ReturnsItEarnedAndJustUnlocked()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("badges-unlock");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);

            var target = FirstBadgeFor(BadgeMetric.ItemsAdded);
            await AddActivitiesAsync(userId!.Value, ActivityType.ProductInventoryCreate, target.Threshold);

            var badges = await GetBadgesAsync();

            var earned = Assert.Single(badges.Badges, badge => badge.Id == target.Id);
            Assert.NotNull(earned.EarnedAt);
            Assert.True(earned.JustUnlocked);
            Assert.Equal(target.Threshold, earned.Progress);

            // The next tier up is still locked, and its progress is capped at its own threshold
            // rather than reporting the raw counter - a progress ring must not overflow.
            var nextTier = BadgeCatalog.All
                .Where(badge => badge.Metric == BadgeMetric.ItemsAdded && badge.Threshold > target.Threshold)
                .OrderBy(badge => badge.Threshold)
                .FirstOrDefault();

            if (nextTier != null)
            {
                var locked = Assert.Single(badges.Badges, badge => badge.Id == nextTier.Id);
                Assert.Null(locked.EarnedAt);
                Assert.Equal(target.Threshold, locked.Progress);
                Assert.True(locked.Progress <= locked.Threshold);
            }
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The test that stops the confetti firing on every page load: a second call returns the same
    /// badge, still earned, with <c>JustUnlocked</c> back to <see langword="false"/>. And the row
    /// behind it is written exactly once no matter how many times the endpoint is called.
    /// </summary>
    [Fact]
    public async Task GetBadges_CalledTwice_ReportsJustUnlockedOnlyOnce()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("badges-once");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);

            var target = FirstBadgeFor(BadgeMetric.ItemsConsumed);
            await AddActivitiesAsync(userId!.Value, ActivityType.ProductInventoryDecrease, target.Threshold);

            var first = await GetBadgesAsync();
            var firstState = Assert.Single(first.Badges, badge => badge.Id == target.Id);
            Assert.True(firstState.JustUnlocked);
            Assert.NotNull(firstState.EarnedAt);

            var second = await GetBadgesAsync();
            var secondState = Assert.Single(second.Badges, badge => badge.Id == target.Id);
            Assert.False(secondState.JustUnlocked);
            Assert.NotNull(secondState.EarnedAt);
            // The same moment, not a fresh one - the badge was earned once and the date is history.
            Assert.Equal(firstState.EarnedAt!.Value, secondState.EarnedAt!.Value, TimeSpan.FromSeconds(1));

            // A third call, to make the row count the point rather than a side effect.
            await GetBadgesAsync();
            Assert.Equal(1, await CountBadgeRowsAsync(userId.Value, target.Id));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Badges are personal: one member crossing a threshold does not earn it for another, even when
    /// they share a family. The counters are keyed on the acting user, and this is what proves it.
    /// </summary>
    [Fact]
    public async Task GetBadges_TwoMembers_AreIndependent()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("badges-member-a");
            testEmailA = emailA;
            var userIdA = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userIdA);

            var (emailB, authB) = await _authHelper.CreateAndAuthenticateUserAsync("badges-member-b");
            testEmailB = emailB;
            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);

            var target = FirstBadgeFor(BadgeMetric.ItemsAdded);
            await AddActivitiesAsync(userIdA!.Value, ActivityType.ProductInventoryCreate, target.Threshold);

            _authHelper.SetAuthToken(authA.AccessToken);
            var badgesA = await GetBadgesAsync();
            Assert.NotNull(Assert.Single(badgesA.Badges, badge => badge.Id == target.Id).EarnedAt);

            _authHelper.SetAuthToken(authB.AccessToken);
            var badgesB = await GetBadgesAsync();
            var stateB = Assert.Single(badgesB.Badges, badge => badge.Id == target.Id);
            Assert.Null(stateB.EarnedAt);
            Assert.False(stateB.JustUnlocked);
            Assert.Equal(0, stateB.Progress);

            Assert.Equal(0, await CountBadgeRowsAsync(userIdB!.Value, target.Id));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailB != null)
                await _authHelper.CleanupUserAsync(testEmailB);
        }
    }

    /// <summary>
    /// A badge id the catalog no longer defines is ignored, not rendered and not thrown over: the
    /// response carries exactly the catalog's badges, and the orphaned row is simply not one of
    /// them. Retiring a badge must not be able to break the endpoint for whoever had earned it.
    /// </summary>
    [Fact]
    public async Task GetBadges_RowForAnIdNoLongerInTheCatalog_IsIgnored()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("badges-retired-id");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);

            await AddRawBadgeRowAsync(userId!.Value, "retired-badge-from-an-older-release");

            var badges = await GetBadgesAsync();

            Assert.Equal(BadgeCatalog.All.Count, badges.Badges.Count);
            Assert.DoesNotContain(badges.Badges, badge => badge.Id == "retired-badge-from-an-older-release");
            Assert.All(badges.Badges, badge => Assert.NotNull(BadgeCatalog.ById(badge.Id)));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The streak badges are household achievements, so a caller with no family cannot earn them -
    /// and, crucially, the endpoint still answers with the whole catalog rather than failing on the
    /// missing household.
    /// </summary>
    [Fact]
    public async Task GetBadges_UserWithNoFamily_HasNoStreakProgressButStillGetsTheCatalog()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("badges-no-family");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var badges = await GetBadgesAsync();

            Assert.Equal(BadgeCatalog.All.Count, badges.Badges.Count);

            var streakIds = BadgeCatalog.All
                .Where(badge => badge.Metric is BadgeMetric.NoExpiryStreakDays or BadgeMetric.ListClearedStreakDays)
                .Select(badge => badge.Id)
                .ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(streakIds);
            Assert.All(badges.Badges.Where(badge => streakIds.Contains(badge.Id)), badge =>
            {
                Assert.Equal(0, badge.Progress);
                Assert.Null(badge.EarnedAt);
            });
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
}
