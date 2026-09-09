using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.Insights;
using Homassy.API.Models.Product;
using Homassy.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using ProductUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Integration;

public class InsightsControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public InsightsControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Helpers
    /// <summary>
    /// Creates a family for whichever user is currently authenticated on <see cref="_client"/>.
    /// </summary>
    private async Task CreateFamilyAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/family/create", new CreateFamilyRequest { Name = name });
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Create family '{name}' status: {response.StatusCode}, body: {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Creates a product in the given category for whichever user is currently authenticated on
    /// <see cref="_client"/> and returns its public id.
    /// </summary>
    private async Task<Guid> CreateProductAsync(ProductCategory category, string namePrefix)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/product", new CreateProductRequest
        {
            Unit = ProductUnit.Piece,
            Name = $"{namePrefix} {category}",
            Brand = "Insight Brand",
            Category = category
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Create product failed: {response.StatusCode} {body}");

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    /// <summary>
    /// Quick-adds a single family-shared inventory item (one row) for the given product, for
    /// whichever user is currently authenticated on <see cref="_client"/>.
    /// </summary>
    private async Task AddSharedInventoryItemAsync(Guid productPublicId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick", new QuickAddInventoryItemRequest
        {
            ProductPublicId = productPublicId,
            Quantity = 1,
            IsSharedWithFamily = true
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Quick-add inventory item failed: {response.StatusCode} {body}");
    }

    /// <summary>
    /// Quick-adds a single personal (not family-shared) inventory item (one row) for the given
    /// product, for whichever user is currently authenticated on <see cref="_client"/>.
    /// </summary>
    private async Task AddPersonalInventoryItemAsync(Guid productPublicId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick", new QuickAddInventoryItemRequest
        {
            ProductPublicId = productPublicId,
            Quantity = 1,
            IsSharedWithFamily = false
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Quick-add inventory item failed: {response.StatusCode} {body}");
    }

    /// <summary>
    /// Attaches an existing user to an existing family directly in the database, then
    /// force-refreshes that user's <see cref="UserFunctions"/> cache entry immediately - mirrors
    /// <c>UserControllerTests.CreateFamilyWithMembersAsync</c> - so a same-test call right after
    /// this sees the new membership without racing <c>CacheManagementService</c>'s 5s poller
    /// (the real join/approve HTTP flow updates only the database row and relies on that poller,
    /// which is not what this test is about).
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

    /// <summary>
    /// Deletes a user's local rows (<c>User</c>, <c>UserProfile</c>, <c>UserNotificationPreferences</c>)
    /// directly - the same rows <see cref="Homassy.Tests.Infrastructure.HomassyWebApplicationFactory.CleanupTestUserAsync"/>
    /// removes - but, unlike that method, never touches the mock Kratos session. Simulates a
    /// Kratos session that is still valid (still passes <c>[Authorize]</c>) but no longer
    /// resolves to a local user: exactly the case <c>SessionInfo.GetUserId()</c> returns null for
    /// even though the request authenticated (see <c>SessionInfo.SetFromKratosSession</c>'s
    /// "doesn't exist locally yet" branch). Also evicts the user from <see cref="UserFunctions"/>'s
    /// static cache so the very next request sees the deletion instead of a stale cache hit.
    /// </summary>
    private async Task DeleteLocalUserRowAsync(int userId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var user = context.Users.First(u => u.Id == userId);

        var profile = context.UserProfiles.FirstOrDefault(p => p.UserId == userId);
        if (profile != null)
        {
            context.UserProfiles.Remove(profile);
        }

        var notificationPrefs = context.UserNotificationPreferences.FirstOrDefault(n => n.UserId == userId);
        if (notificationPrefs != null)
        {
            context.UserNotificationPreferences.Remove(notificationPrefs);
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync();

        var userFunctions = scope.ServiceProvider.GetRequiredService<UserFunctions>();
        await userFunctions.RefreshUserCacheAsync(userId);
    }
    #endregion

    [Fact]
    public async Task GetInventoryComposition_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Fix round 2: a request that authenticates - a valid Kratos session, so <c>[Authorize]</c>
    /// passes - but whose session cannot be resolved to a local <c>User</c> row (<c>SessionInfo.GetUserId()</c>
    /// is null) must be 401, not 200 with an empty payload. This is an authentication problem -
    /// the server does not know who is asking - never a legitimately empty dataset. Mirrors the
    /// in-repo precedent for exactly this condition: <c>UserController.SendTestPushNotification</c>
    /// / <c>SendTestEmail</c>.
    /// </summary>
    /// <remarks>
    /// Not to be confused with <see cref="GetInventoryComposition_UserWithNoFamily_ReturnsEmptyResultNotAnErrorOrGlobalCount"/>
    /// or <see cref="GetInventoryComposition_UserWithNoFamilyButOwnItems_ReturnsOwnPersonalComposition"/>:
    /// both of those callers have a valid, resolvable user id and simply no family - a
    /// legitimately empty (or personal-items-only) result that must keep returning 200. Only a
    /// null <em>user</em> id becomes 401.
    /// </remarks>
    [Fact]
    public async Task GetInventoryComposition_SessionWithNoLocalUser_ReturnsUnauthorized()
    {
        string? kratosIdentityId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("insight-no-local-user");
            kratosIdentityId = auth.AccessToken.Replace("mock-session-", "");
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);

            // Deletes the local User row (and its dependents) but deliberately leaves the mock
            // Kratos session registered - see DeleteLocalUserRowAsync's doc comment for why this
            // reproduces "authenticated, but no resolvable user id" rather than "unauthenticated"
            // (already covered by GetInventoryComposition_WithoutToken_ReturnsUnauthorized above).
            await DeleteLocalUserRowAsync(userId!.Value);

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();

            // The User row is already gone at this point, so CleanupUserAsync(email) - which
            // looks the user up by email in the database - could not find it to clear its mock
            // Kratos session. Clear the session directly instead, the same way
            // HomassyWebApplicationFactory.CleanupTestUserAsync would have.
            if (kratosIdentityId != null)
                await _factory.MockKratos.DeleteIdentitySessionsAsync(kratosIdentityId);
        }
    }

    [Fact]
    public async Task GetInventoryComposition_UserWithNoFamily_ReturnsEmptyResultNotAnErrorOrGlobalCount()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("insight-no-family");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            // No family and no items yields an empty result - never an error, and never a query
            // that falls through to an unscoped (i.e. global) count. This user does have a user
            // id, so the query does run (no short-circuit here) - it simply finds nothing. See
            // GetInventoryComposition_UserWithNoFamilyButOwnItems_ReturnsOwnPersonalComposition
            // below for the case that actually exercises the family-less half of the union.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(content?.Data);
            Assert.True(content.Success);
            Assert.Empty(content.Data.Slices);
            Assert.Equal(0, content.Data.OtherCount);
            Assert.Equal(0, content.Data.TotalCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The interpretation fix in this fix round: a null family id must not short-circuit the
    /// result to empty. A member with no family still has their own personal (non-shared)
    /// inventory items, and this endpoint must show them - the same union
    /// <c>ProductFunctions.GetInventoryItemsByUserAndFamily</c> and friends use everywhere else.
    /// </summary>
    [Fact]
    public async Task GetInventoryComposition_UserWithNoFamilyButOwnItems_ReturnsOwnPersonalComposition()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("insight-no-family-items");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Deliberately never calls CreateFamilyAsync - this user has no family at all.
            var milkProductId = await CreateProductAsync(ProductCategory.Milk, "no-fam");
            await AddPersonalInventoryItemAsync(milkProductId);

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(1, content.Data.TotalCount);
            Assert.Equal(0, content.Data.OtherCount);
            var milkSlice = Assert.Single(content.Data.Slices, s => s.Category == ProductCategory.Milk);
            Assert.Equal(1, milkSlice.Count);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetInventoryComposition_ThreeCategories_ReturnsThreeSlicesWithSharesSummingToOne()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("insight-3cat");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            await CreateFamilyAsync("Insight Family 3cat");

            foreach (var category in new[] { ProductCategory.Milk, ProductCategory.Bread, ProductCategory.Cheese })
            {
                var productId = await CreateProductAsync(category, "3cat");
                await AddSharedInventoryItemAsync(productId);
            }

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(3, content.Data.Slices.Count);
            Assert.Equal(0, content.Data.OtherCount);
            Assert.Equal(3, content.Data.TotalCount);

            var totalShare = content.Data.Slices.Sum(s => s.Share);
            Assert.True(Math.Abs(totalShare - 1m) < 0.01m, $"Expected slice shares to sum to ~1, got {totalShare}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetInventoryComposition_MoreThanEightCategories_ReturnsExactlyEightSlicesPlusOtherCount()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("insight-10cat");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            await CreateFamilyAsync("Insight Family 10cat");

            // 10 distinct categories, one inventory item each - more than the 8-slice cap.
            var categories = new[]
            {
                ProductCategory.Grain, ProductCategory.Bread, ProductCategory.CerealAndBreakfast,
                ProductCategory.Pasta, ProductCategory.Rice, ProductCategory.Flour,
                ProductCategory.Sugar, ProductCategory.Salt, ProductCategory.Spices, ProductCategory.Oil
            };

            foreach (var category in categories)
            {
                var productId = await CreateProductAsync(category, "10cat");
                await AddSharedInventoryItemAsync(productId);
            }

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(8, content.Data.Slices.Count);
            Assert.True(content.Data.OtherCount > 0, "Expected a non-zero OtherCount for the categories past the top 8");
            Assert.Equal(categories.Length, content.Data.TotalCount);
            Assert.Equal(categories.Length, content.Data.Slices.Sum(s => s.Count) + content.Data.OtherCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Fix round 2: the "two different Others" bug. Before this fix, when the explicit
    /// <see cref="ProductCategory.Other"/> category ranked in the top 8 by count, it appeared as
    /// its own entry in <see cref="InventoryCompositionResponse.Slices"/> <em>and</em>
    /// <see cref="InventoryCompositionResponse.OtherCount"/> stayed whatever the top-8 overflow
    /// summed to - two different things both meaning "other" in the same payload, even though the
    /// <c>Slices.Sum + OtherCount == TotalCount</c> invariant still held (the arithmetic was never
    /// wrong, only the shape). This test builds exactly that situation: 8 distinct non-Other
    /// categories with 1 item each, plus a substantially larger explicit-<see cref="ProductCategory.Other"/>
    /// bucket that would out-rank every one of them on count alone - so before the fix, it would
    /// have claimed a top-8 slice instead of folding into <see cref="InventoryCompositionResponse.OtherCount"/>.
    /// </summary>
    [Fact]
    public async Task GetInventoryComposition_ExplicitOtherCategoryRanksInTopEight_FoldsIntoOtherCountNotItsOwnSlice()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("insight-other-fold");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            await CreateFamilyAsync("Insight Family Other Fold");

            // 8 distinct non-Other categories, 1 item each.
            var nonOtherCategories = new[]
            {
                ProductCategory.Grain, ProductCategory.Bread, ProductCategory.CerealAndBreakfast,
                ProductCategory.Pasta, ProductCategory.Rice, ProductCategory.Flour,
                ProductCategory.Sugar, ProductCategory.Salt
            };
            foreach (var category in nonOtherCategories)
            {
                var productId = await CreateProductAsync(category, "other-fold");
                await AddSharedInventoryItemAsync(productId);
            }

            // A substantial explicit-Other bucket - enough to out-rank every category above on
            // count alone, so under the pre-fix behaviour it would have claimed a top-8 slice of
            // its own instead of folding into OtherCount.
            var otherProductId = await CreateProductAsync(ProductCategory.Other, "other-fold");
            for (var i = 0; i < 5; i++)
            {
                await AddSharedInventoryItemAsync(otherProductId);
            }

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(content?.Data);

            // The bug this proves fixed: Slices must never contain an Other entry, no matter how
            // large its count is relative to everything else.
            Assert.DoesNotContain(content.Data.Slices, s => s.Category == ProductCategory.Other);

            // OtherCount must include the explicit-Other items, not just top-8 overflow - and
            // here there is no overflow at all, since exactly 8 non-Other categories exist.
            Assert.Equal(13, content.Data.TotalCount); // 8 x 1 + 5
            Assert.Equal(8, content.Data.Slices.Count);
            Assert.Equal(5, content.Data.OtherCount);

            // The invariant this fix must preserve.
            Assert.Equal(content.Data.TotalCount, content.Data.Slices.Sum(s => s.Count) + content.Data.OtherCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The test that matters most in this task: a second family's inventory must never leak into
    /// the caller's own composition, even though both families' inventory items live in the same
    /// table.
    /// </summary>
    [Fact]
    public async Task GetInventoryComposition_SecondFamilysInventory_NeverAppears()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            // Family A - the caller under test.
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("insight-fam-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Insight Family A");

            var milkProductId = await CreateProductAsync(ProductCategory.Milk, "fam-a");
            await AddSharedInventoryItemAsync(milkProductId);
            await AddSharedInventoryItemAsync(milkProductId);

            var breadProductId = await CreateProductAsync(ProductCategory.Bread, "fam-a");
            await AddSharedInventoryItemAsync(breadProductId);

            // Family B - a second, unrelated family with its own, larger, differently-categorized stock.
            var (emailB, authB) = await _authHelper.CreateAndAuthenticateUserAsync("insight-fam-b");
            testEmailB = emailB;
            _authHelper.SetAuthToken(authB.AccessToken);
            await CreateFamilyAsync("Insight Family B");

            var meatProductId = await CreateProductAsync(ProductCategory.Meat, "fam-b");
            for (var i = 0; i < 5; i++)
            {
                await AddSharedInventoryItemAsync(meatProductId);
            }

            // Switch back to family A's caller and read the composition.
            _authHelper.SetAuthToken(authA.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(content?.Data);

            // Family A owns exactly 3 items (2 Milk + 1 Bread) - never family B's 5 Meat items.
            Assert.Equal(3, content.Data.TotalCount);
            Assert.Equal(0, content.Data.OtherCount);
            Assert.DoesNotContain(content.Data.Slices, s => s.Category == ProductCategory.Meat);

            var milkSlice = Assert.Single(content.Data.Slices, s => s.Category == ProductCategory.Milk);
            Assert.Equal(2, milkSlice.Count);

            var breadSlice = Assert.Single(content.Data.Slices, s => s.Category == ProductCategory.Bread);
            Assert.Equal(1, breadSlice.Count);
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
    /// The test that would have caught the cache-key leak fixed in this round: the cache result
    /// depends on the acting user (personal items are part of the union now), not only the
    /// family, so the cache key must carry the user id. Without it, two members of the same
    /// family share one <see cref="Homassy.API.Services.FamilyInsightsCache"/> entry, and
    /// whichever member's request misses the cache first has their own personal items served
    /// back to every other member for the rest of the TTL.
    ///
    /// <para>
    /// Two members of the same family: member A has 2 personal Milk items and shares 1 Bread
    /// item with the family; member B has 1 personal Cheese item and sees that same shared
    /// Bread item. A calls first (populating whatever cache entry the implementation uses), then
    /// B calls. With a family-only key, B's call would be a cache hit on A's entry - B would see
    /// A's Milk/Bread composition (total 3) instead of B's own Cheese/Bread one (total 2).
    /// </para>
    /// </summary>
    [Fact]
    public async Task GetInventoryComposition_TwoMembersOfSameFamily_EachSeesOwnPersonalItemsNeverTheOthers()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            // Member A creates the family, two personal Milk items, and one item shared with
            // the family.
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("insight-cache-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Insight Cache Family");

            var milkProductId = await CreateProductAsync(ProductCategory.Milk, "cache-a-personal");
            await AddPersonalInventoryItemAsync(milkProductId);
            await AddPersonalInventoryItemAsync(milkProductId);

            var breadProductId = await CreateProductAsync(ProductCategory.Bread, "cache-shared");
            await AddSharedInventoryItemAsync(breadProductId);

            var userAId = _factory.GetUserIdByEmail(emailA);
            Assert.NotNull(userAId);

            int familyId;
            {
                var (scope, context) = _factory.CreateScopedDbContext();
                using var _ = scope;
                var userA = context.Users.First(u => u.Id == userAId!.Value);
                Assert.NotNull(userA.FamilyId);
                familyId = userA.FamilyId!.Value;
            }

            // Member B joins the same family - directly via the database plus an explicit cache
            // refresh (see AddUserToFamilyAsync), not the join/approve HTTP flow, because that
            // flow's own cache-propagation delay is not what this test is about - then adds
            // their own personal item, in a category A never touches.
            var (emailB, authB) = await _authHelper.CreateAndAuthenticateUserAsync("insight-cache-b");
            testEmailB = emailB;

            var userBId = _factory.GetUserIdByEmail(emailB);
            Assert.NotNull(userBId);
            await AddUserToFamilyAsync(userBId!.Value, familyId);

            _authHelper.SetAuthToken(authB.AccessToken);
            var cheeseProductId = await CreateProductAsync(ProductCategory.Cheese, "cache-b-personal");
            await AddPersonalInventoryItemAsync(cheeseProductId);

            // Member A calls first - this is the request that populates whatever cache entry
            // the implementation keys the result under.
            _authHelper.SetAuthToken(authA.AccessToken);
            var responseA = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var bodyA = await responseA.Content.ReadAsStringAsync();
            _output.WriteLine($"A Status: {responseA.StatusCode}");
            _output.WriteLine($"A Response: {bodyA}");
            Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
            var contentA = await responseA.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(contentA?.Data);

            // Member B calls second. With a family-only cache key this would be a cache hit
            // returning member A's own result computed just above.
            _authHelper.SetAuthToken(authB.AccessToken);
            var responseB = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
            var bodyB = await responseB.Content.ReadAsStringAsync();
            _output.WriteLine($"B Status: {responseB.StatusCode}");
            _output.WriteLine($"B Response: {bodyB}");
            Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
            var contentB = await responseB.Content.ReadFromJsonAsync<ApiResponse<InventoryCompositionResponse>>();
            Assert.NotNull(contentB?.Data);

            // A sees A's own 2 Milk plus the shared Bread - never B's Cheese.
            Assert.Equal(3, contentA.Data.TotalCount);
            Assert.DoesNotContain(contentA.Data.Slices, s => s.Category == ProductCategory.Cheese);
            var milkSliceA = Assert.Single(contentA.Data.Slices, s => s.Category == ProductCategory.Milk);
            Assert.Equal(2, milkSliceA.Count);
            var breadSliceA = Assert.Single(contentA.Data.Slices, s => s.Category == ProductCategory.Bread);
            Assert.Equal(1, breadSliceA.Count);

            // B sees B's own Cheese plus the shared Bread - never A's Milk. A leaking cache
            // would fail this block first, since B's TotalCount would come back as A's 3
            // instead of B's own 2.
            Assert.Equal(2, contentB.Data.TotalCount);
            Assert.DoesNotContain(contentB.Data.Slices, s => s.Category == ProductCategory.Milk);
            var cheeseSliceB = Assert.Single(contentB.Data.Slices, s => s.Category == ProductCategory.Cheese);
            Assert.Equal(1, cheeseSliceB.Count);
            var breadSliceB = Assert.Single(contentB.Data.Slices, s => s.Category == ProductCategory.Bread);
            Assert.Equal(1, breadSliceB.Count);
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
}
