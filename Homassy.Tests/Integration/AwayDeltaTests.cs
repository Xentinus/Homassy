using System.Net;
using System.Net.Http.Json;
using Homassy.API.Entities.Activity;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.Insights;
using Homassy.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// Integration tests for <c>GET api/v1.0/insights/delta</c> - the #127 endpoint behind "3 changes
/// since your last visit".
/// </summary>
/// <remarks>
/// The rule most of these exist to protect is that <b>the caller's own activity is excluded</b>. A
/// "what changed while you were away" that reports your own actions back at you inflates every
/// number with things you already know and makes the actor list read as if you had been busy in
/// your own absence.
/// </remarks>
public class AwayDeltaTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public AwayDeltaTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Helpers
    private async Task CreateFamilyAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/family/create", new CreateFamilyRequest { Name = name });
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Create family '{name}' status: {response.StatusCode}, body: {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private int? FamilyIdOf(int userId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        using (scope)
        {
            return context.Users.First(u => u.Id == userId).FamilyId;
        }
    }

    /// <summary>
    /// Attaches an existing user to a family directly and force-refreshes their cache entry, so a
    /// same-test request sees the membership without racing <c>CacheManagementService</c>'s poller -
    /// mirrors <c>InsightsControllerTests.AddUserToFamilyAsync</c>.
    /// </summary>
    private async Task AddUserToFamilyAsync(int userId, int familyId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var user = context.Users.First(u => u.Id == userId);
        user.FamilyId = familyId;
        await context.SaveChangesAsync();

        var userFunctions = scope.ServiceProvider.GetRequiredService<UserFunctions>();
        await userFunctions.RefreshUserCacheAsync(userId);
    }

    private async Task AddActivityAsync(int userId, int? familyId, ActivityType activityType, DateTime timestampUtc)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        context.Activities.Add(new Activity
        {
            UserId = userId,
            FamilyId = familyId,
            Timestamp = DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc),
            ActivityType = activityType,
            RecordId = 1,
            RecordName = "Away Delta Test Record"
        });
        await context.SaveChangesAsync();
    }

    /// <summary>Sets the caller's stored last-seen, for the "no since parameter" fallback.</summary>
    private async Task SetLastSeenAsync(int userId, DateTime? lastSeenUtc)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var profile = context.UserProfiles.First(p => p.UserId == userId);
        profile.LastSeenAt = lastSeenUtc.HasValue ? DateTime.SpecifyKind(lastSeenUtc.Value, DateTimeKind.Utc) : null;
        await context.SaveChangesAsync();

        var userFunctions = scope.ServiceProvider.GetRequiredService<UserFunctions>();
        await userFunctions.RefreshUserProfileCacheAsync(profile.Id);
    }

    private async Task<AwayDeltaResponse> GetDeltaAsync(DateTime? since)
    {
        var url = since.HasValue
            ? $"/api/v1.0/insights/delta?since={Uri.EscapeDataString(since.Value.ToString("O"))}"
            : "/api/v1.0/insights/delta";

        var response = await _client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {body}");

        // 200 with zeroes, never 204 - the client decides whether an empty delta is worth showing.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<AwayDeltaResponse>>();
        Assert.NotNull(content?.Data);
        return content.Data;
    }
    #endregion

    [Fact]
    public async Task GetAwayDelta_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/insights/delta");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The core case: another member's activity inside the window is counted by kind, the same
    /// activity before the window is not, and the caller's own is counted nowhere. <c>Total</c> is
    /// the sum of the kinds, and the actor list names the other member.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_CountsAnotherMembersActivityInTheWindowOnly()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("delta-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Away Delta Family");

            var userIdA = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userIdA);
            var familyId = FamilyIdOf(userIdA!.Value);
            Assert.NotNull(familyId);

            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("delta-b");
            testEmailB = emailB;
            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);
            await AddUserToFamilyAsync(userIdB!.Value, familyId!.Value);

            var since = DateTime.UtcNow.AddHours(-2);

            // Inside the window, by the other member: two adds, one consume, one purchase.
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddHours(-1));
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-50));
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryDecrease, DateTime.UtcNow.AddMinutes(-40));
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ShoppingListItemPurchase, DateTime.UtcNow.AddMinutes(-30));

            // Before the window: must not be counted.
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddHours(-5));

            // The caller's own, inside the window: must not be counted either.
            await AddActivityAsync(userIdA.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-20));

            _authHelper.SetAuthToken(authA.AccessToken);
            var delta = await GetDeltaAsync(since);

            Assert.Equal(2, delta.ItemsAdded);
            Assert.Equal(1, delta.ItemsConsumed);
            Assert.Equal(1, delta.ListItemsPurchased);
            Assert.Equal(4, delta.Total);
            Assert.Equal(delta.ItemsAdded + delta.ItemsConsumed + delta.ListItemsPurchased + delta.NewExpirations + delta.FamilyEvents, delta.Total);

            var actor = Assert.Single(delta.TopActors);
            Assert.Equal(4, actor.Count);
            Assert.False(string.IsNullOrWhiteSpace(actor.DisplayName));

            // The window is echoed back as used, since the client links the timeline with it.
            Assert.Equal(since, delta.Since, TimeSpan.FromSeconds(2));
            Assert.True(delta.Until >= delta.Since);
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
    /// At most three actors, busiest first - the summary is one line, and it stops being a line at
    /// four names.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_TopActors_AreAtMostThreeOrderedByCount()
    {
        var emails = new List<string>();
        try
        {
            var (ownerEmail, ownerAuth) = await _authHelper.CreateAndAuthenticateUserAsync("delta-actors-owner");
            emails.Add(ownerEmail);
            _authHelper.SetAuthToken(ownerAuth.AccessToken);
            await CreateFamilyAsync("Away Delta Actors Family");

            var ownerId = _factory.GetUserIdByEmail(ownerEmail);
            Assert.NotNull(ownerId);
            var familyId = FamilyIdOf(ownerId!.Value);
            Assert.NotNull(familyId);

            // Four other members with 4, 3, 2 and 1 activities - so the cut is visible and the
            // order is unambiguous.
            var counts = new[] { 4, 3, 2, 1 };
            for (var index = 0; index < counts.Length; index++)
            {
                var (memberEmail, _) = await _authHelper.CreateAndAuthenticateUserAsync($"delta-actor-{index}");
                emails.Add(memberEmail);

                var memberId = _factory.GetUserIdByEmail(memberEmail);
                Assert.NotNull(memberId);
                await AddUserToFamilyAsync(memberId!.Value, familyId!.Value);

                for (var i = 0; i < counts[index]; i++)
                {
                    await AddActivityAsync(memberId.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-10));
                }
            }

            _authHelper.SetAuthToken(ownerAuth.AccessToken);
            var delta = await GetDeltaAsync(DateTime.UtcNow.AddHours(-1));

            Assert.Equal(3, delta.TopActors.Count);
            Assert.Equal(new[] { 4, 3, 2 }, delta.TopActors.Select(actor => actor.Count));
            // Every kind count still includes the member who did not make the actor list.
            Assert.Equal(10, delta.ItemsAdded);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            foreach (var email in emails)
            {
                await _authHelper.CleanupUserAsync(email);
            }
        }
    }

    /// <summary>
    /// Nothing in the window is a 200 with zeroes and no actors - not a 204, and not an error.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_NoActivityInTheWindow_IsAnEmptyDeltaWith200()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("delta-quiet");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Away Delta Quiet Family");

            var delta = await GetDeltaAsync(DateTime.UtcNow.AddHours(-1));

            Assert.Equal(0, delta.Total);
            Assert.Empty(delta.TopActors);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// With no <c>since</c> the server falls back to the caller's stored last-seen - the new-device
    /// case, where the client has no local value of its own.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_WithoutSince_FallsBackToTheStoredLastSeen()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("delta-lastseen-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Away Delta LastSeen Family");

            var userIdA = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userIdA);
            var familyId = FamilyIdOf(userIdA!.Value);

            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("delta-lastseen-b");
            testEmailB = emailB;
            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);
            await AddUserToFamilyAsync(userIdB!.Value, familyId!.Value);

            // Seen an hour ago; one thing happened since, one before.
            await SetLastSeenAsync(userIdA!.Value, DateTime.UtcNow.AddHours(-1));
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-30));
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddHours(-3));

            _authHelper.SetAuthToken(authA.AccessToken);
            var delta = await GetDeltaAsync(since: null);

            Assert.Equal(1, delta.ItemsAdded);
            Assert.Equal(1, delta.Total);
            Assert.Equal(DateTime.UtcNow.AddHours(-1), delta.Since, TimeSpan.FromMinutes(1));
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
    /// Neither a <c>since</c> nor a stored last-seen is a first-ever launch: an empty delta, never
    /// the household's whole history dressed up as "what changed while you were away".
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_WithNeitherSinceNorStoredLastSeen_IsEmptyRatherThanEverything()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("delta-firstlaunch-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Away Delta First Launch Family");

            var userIdA = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userIdA);
            var familyId = FamilyIdOf(userIdA!.Value);

            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("delta-firstlaunch-b");
            testEmailB = emailB;
            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);
            await AddUserToFamilyAsync(userIdB!.Value, familyId!.Value);

            // Plenty of history for the other member - and none of it should be reported.
            await SetLastSeenAsync(userIdA!.Value, null);
            for (var i = 0; i < 5; i++)
            {
                await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-10 * (i + 1)));
            }

            _authHelper.SetAuthToken(authA.AccessToken);
            var delta = await GetDeltaAsync(since: null);

            Assert.Equal(0, delta.Total);
            Assert.Empty(delta.TopActors);
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
    /// A <c>since</c> in the future is a clock-skewed client, not a request for a negative window:
    /// an empty delta rather than an error or an inverted range.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_SinceInTheFuture_IsEmptyRatherThanANegativeWindow()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("delta-future-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Away Delta Future Family");

            var userIdA = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userIdA);
            var familyId = FamilyIdOf(userIdA!.Value);

            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("delta-future-b");
            testEmailB = emailB;
            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);
            await AddUserToFamilyAsync(userIdB!.Value, familyId!.Value);

            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-5));

            _authHelper.SetAuthToken(authA.AccessToken);
            var delta = await GetDeltaAsync(DateTime.UtcNow.AddHours(1));

            Assert.Equal(0, delta.Total);
            Assert.Empty(delta.TopActors);
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
    /// A very old <c>since</c> is clamped to 90 days, and the response says so: <c>Since</c> reports
    /// the window actually used, so a caller is never told it got the window it asked for. Activity
    /// older than the clamp is excluded, which is what keeps the query bounded.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_SinceOlderThanNinetyDays_IsClampedAndReportsTheClampedWindow()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("delta-clamp-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Away Delta Clamp Family");

            var userIdA = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userIdA);
            var familyId = FamilyIdOf(userIdA!.Value);

            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("delta-clamp-b");
            testEmailB = emailB;
            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);
            await AddUserToFamilyAsync(userIdB!.Value, familyId!.Value);

            // Inside the clamp, and well outside it.
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddDays(-10));
            await AddActivityAsync(userIdB.Value, familyId, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddDays(-200));

            _authHelper.SetAuthToken(authA.AccessToken);
            var delta = await GetDeltaAsync(DateTime.UtcNow.AddDays(-365));

            Assert.Equal(1, delta.ItemsAdded);
            Assert.Equal(1, delta.Total);
            Assert.Equal(DateTime.UtcNow.AddDays(-90), delta.Since, TimeSpan.FromMinutes(1));
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
    /// The scope rule: a second family's activity never appears, however much of it there is.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_SecondFamilysActivity_NeverAppears()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("delta-fam-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Away Delta Family A");

            var (emailB, authB) = await _authHelper.CreateAndAuthenticateUserAsync("delta-fam-b");
            testEmailB = emailB;
            _authHelper.SetAuthToken(authB.AccessToken);
            await CreateFamilyAsync("Away Delta Family B");

            var userIdB = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userIdB);
            var familyIdB = FamilyIdOf(userIdB!.Value);

            for (var i = 0; i < 7; i++)
            {
                await AddActivityAsync(userIdB.Value, familyIdB, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-5));
            }

            _authHelper.SetAuthToken(authA.AccessToken);
            var delta = await GetDeltaAsync(DateTime.UtcNow.AddHours(-1));

            Assert.Equal(0, delta.Total);
            Assert.Empty(delta.TopActors);
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
    /// A caller with no family gets an empty delta: with their own activity excluded there is nobody
    /// else's left to report, and the endpoint says so without querying for it.
    /// </summary>
    [Fact]
    public async Task GetAwayDelta_UserWithNoFamily_IsEmpty()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("delta-no-family");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            await AddActivityAsync(userId!.Value, null, ActivityType.ProductInventoryCreate, DateTime.UtcNow.AddMinutes(-5));

            var delta = await GetDeltaAsync(DateTime.UtcNow.AddHours(-1));

            Assert.Equal(0, delta.Total);
            Assert.Empty(delta.TopActors);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
}
