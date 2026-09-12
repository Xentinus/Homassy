using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Homassy.API.Models.Product;
using Homassy.API.Models.Search;
using Homassy.API.Models.ShoppingList;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// The command palette's one endpoint (#111).
/// </summary>
/// <remarks>
/// The endpoint answers from the Functions layer's in-memory caches, which
/// `CacheManagementService` refreshes every five seconds, so a row written a moment ago is not
/// searchable yet.
///
/// Rather than sleeping past that window like <see cref="SelectValueControllerTests"/> does,
/// these tests poll (<see cref="SearchUntilAsync"/>) and carry on the moment the row turns up —
/// usually well inside the window. Two reasons. It is not flaky when a refresh runs long, and,
/// more importantly, it does not park the suite on its cache boundary: the other
/// cache-dependent tests in this suite have only a second of margin, and enough concurrent
/// six-second sleeps is all it takes to push one of them over.
///
/// The two tests that need data therefore also assert several things each rather than being
/// split one assertion per test — each split would be another wait.
/// </remarks>
public class SearchControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    /// <summary>How long to keep polling for a just-created row — several refresh intervals.</summary>
    private const int CacheWaitTimeoutMs = 20000;

    /// <summary>Gap between polls. Short enough to leave the window as soon as the row lands.</summary>
    private const int CachePollIntervalMs = 250;

    public SearchControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    private async Task<GlobalSearchResponse> SearchAsync(string query, int? limit = null)
    {
        var url = $"/api/v1.0/search?q={Uri.EscapeDataString(query)}";
        if (limit.HasValue) url += $"&limit={limit.Value}";

        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<GlobalSearchResponse>>();
        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.NotNull(content.Data);

        _output.WriteLine($"q={query} -> {content.Data.TotalCount} hits in {content.Data.Groups.Count} groups");
        return content.Data;
    }

    /// <summary>
    /// Searches until <paramref name="isReady"/> is happy with the answer, or the timeout runs
    /// out — at which point the last answer is returned so the caller's own assertions produce
    /// the failure message rather than a bare timeout.
    /// </summary>
    private async Task<GlobalSearchResponse> SearchUntilAsync(
        string query,
        Func<GlobalSearchResponse, bool> isReady,
        int? limit = null)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(CacheWaitTimeoutMs);
        GlobalSearchResponse results;

        while (true)
        {
            results = await SearchAsync(query, limit);
            if (isReady(results) || DateTime.UtcNow >= deadline) break;
            await Task.Delay(CachePollIntervalMs);
        }

        return results;
    }

    private static SearchResultGroup? GroupOf(GlobalSearchResponse response, SearchResultKind kind) =>
        response.Groups.FirstOrDefault(g => g.Kind == kind);

    private async Task CreateProductAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/product",
            new CreateProductRequest
            {
                Name = name,
                Brand = "SearchTest",
                Unit = Homassy.API.Enums.Unit.Piece
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #region Authorization
    [Fact]
    public async Task Search_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/search?q=milk");

        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Request validation
    [Theory]
    [InlineData("")]
    [InlineData("m")]
    public async Task Search_QueryShorterThanTheMinimum_ReturnsNoGroups(string query)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"search-short-{query.Length}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var results = await SearchAsync(query);

            // Not an error - one letter matches most of a catalogue, so the server declines to
            // answer rather than returning a capped slice of everything.
            Assert.Empty(results.Groups);
            Assert.Equal(0, results.TotalCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task Search_QueryLongerThanTheLimit_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("search-toolong");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync($"/api/v1.0/search?q={new string('a', 129)}");

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task Search_EchoesTheQueryItAnswered()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("search-echo");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // The client discards a late answer by comparing this against what is in the field.
            var results = await SearchAsync("milk");

            Assert.Equal("milk", results.Query);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Finding things
    /// <summary>
    /// One pass over the endpoint's whole job: several entity types answered together, matched
    /// without regard to accents, a list found through an item on it, and none of it visible to
    /// somebody else.
    /// </summary>
    [Fact]
    public async Task Search_AnswersEveryTypeAtOnceAndOnlyForTheOwner()
    {
        string? ownerEmail = null;
        string? strangerEmail = null;
        try
        {
            var token = Guid.NewGuid().ToString("N")[..8];
            // Accented on purpose: one of the searches below is the same word without accents.
            var productName = $"Sörösüveg {token}";
            var listName = $"Weekend {token}";
            var locationName = $"Cellar {token}";
            var itemName = $"Pickled {token}";
            var itemListName = $"Groceries {Guid.NewGuid():N}";

            var (owner, ownerAuth) = await _authHelper.CreateAndAuthenticateUserAsync("search-owner");
            ownerEmail = owner;
            _authHelper.SetAuthToken(ownerAuth.AccessToken);

            await CreateProductAsync(productName);

            var list = await _client.PostAsJsonAsync(
                "/api/v1.0/shoppinglist",
                new CreateShoppingListRequest { Name = listName });
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            var location = await _client.PostAsJsonAsync(
                "/api/v1.0/location/storage",
                new StorageLocationRequest { Name = locationName, IsFreezer = false });
            Assert.Equal(HttpStatusCode.OK, location.StatusCode);

            // A second list whose name shares nothing with the term: it can only be found
            // through the item sitting on it.
            var itemList = await _client.PostAsJsonAsync(
                "/api/v1.0/shoppinglist",
                new CreateShoppingListRequest { Name = itemListName });
            var itemListContent = await itemList.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            var itemListId = itemListContent?.Data?.PublicId;
            Assert.NotNull(itemListId);

            var item = await _client.PostAsJsonAsync(
                "/api/v1.0/shoppinglist/item",
                new CreateShoppingListItemRequest
                {
                    ShoppingListPublicId = itemListId.Value,
                    CustomName = itemName,
                    Quantity = 1,
                    Unit = Homassy.API.Enums.Unit.Piece
                });
            Assert.Equal(HttpStatusCode.OK, item.StatusCode);

            // Ready once the last thing written is searchable: the item-only list is created
            // after everything else, so seeing it means the whole batch has been cached.
            var results = await SearchUntilAsync(
                token,
                r => GroupOf(r, SearchResultKind.ShoppingList)?.Items.Any(i => i.Title == itemListName) == true);

            var products = GroupOf(results, SearchResultKind.Product);
            var lists = GroupOf(results, SearchResultKind.ShoppingList);
            var locations = GroupOf(results, SearchResultKind.StorageLocation);

            Assert.NotNull(products);
            Assert.NotNull(lists);
            Assert.NotNull(locations);

            var product = Assert.Single(products.Items, i => i.Title == productName);
            Assert.Equal("SearchTest", product.Subtitle);
            Assert.Contains(lists.Items, i => i.Title == listName);
            Assert.Contains(locations.Items, i => i.Title == locationName);

            // Found through its item, and the subtitle names the item, so the row explains itself.
            var byItem = Assert.Single(lists.Items, i => i.Title == itemListName);
            Assert.Equal(itemName, byItem.Subtitle);

            // Matching runs in memory through `NormalizeForSearch`, so the accents are optional.
            // A SQL `ILIKE` would not do this, which is why the endpoint reads from the cache.
            var unaccented = await SearchAsync($"sorosuveg {token}");
            Assert.Contains(
                GroupOf(unaccented, SearchResultKind.Product)?.Items ?? [],
                i => i.Title == productName);

            // Ownership is delegated to the `Get...ByUserAndFamily` accessors rather than
            // re-expressed in the search code. This is the assertion that says so out loud.
            _authHelper.ClearAuthToken();
            var (stranger, strangerAuth) = await _authHelper.CreateAndAuthenticateUserAsync("search-stranger");
            strangerEmail = stranger;
            _authHelper.SetAuthToken(strangerAuth.AccessToken);

            var strangersView = await SearchAsync(token);
            var strangersLists = GroupOf(strangersView, SearchResultKind.ShoppingList);
            var strangersLocations = GroupOf(strangersView, SearchResultKind.StorageLocation);

            Assert.True(strangersLists is null || strangersLists.Items.All(i => i.Title != listName));
            Assert.True(strangersLists is null || strangersLists.Items.All(i => i.Title != itemListName));
            Assert.True(strangersLocations is null || strangersLocations.Items.All(i => i.Title != locationName));

            // The catalogue is global and a product carries no owner, so the product is the one
            // thing a stranger does see - the same rule as the chat's attachment picker.
            Assert.Contains(
                GroupOf(strangersView, SearchResultKind.Product)?.Items ?? [],
                i => i.Title == productName);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (strangerEmail != null)
                await _authHelper.CleanupUserAsync(strangerEmail);
            if (ownerEmail != null)
                await _authHelper.CleanupUserAsync(ownerEmail);
        }
    }
    #endregion

    #region Ranking and capping
    [Fact]
    public async Task Search_RanksTheExactNameFirstAndCapsAtTheRequestedLimit()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("search-rank");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var token = Guid.NewGuid().ToString("N")[..8];
            var exact = $"Rank{token}";

            // The exact match is created last on purpose: if ranking were ignored, insertion
            // order would put one of the longer names first.
            foreach (var name in new[] { $"{exact} Deluxe Edition", $"{exact} Mini", $"{exact} Family Pack", exact })
            {
                await CreateProductAsync(name);
            }

            var results = await SearchUntilAsync(
                exact,
                r => GroupOf(r, SearchResultKind.Product)?.TotalCount == 4,
                limit: 2);
            var products = GroupOf(results, SearchResultKind.Product);

            Assert.NotNull(products);
            Assert.Equal(exact, products.Items[0].Title);
            Assert.Equal(2, products.Items.Count);
            // What the client's "show all N in ..." row quotes: the whole total, not the number
            // of rows it was handed.
            Assert.Equal(4, products.TotalCount);
            Assert.False(products.HasMore);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion
}
