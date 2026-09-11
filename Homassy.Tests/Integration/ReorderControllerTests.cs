using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Automation;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Homassy.API.Models.ShoppingList;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;
using ProductUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Integration;

/// <summary>
/// The manual-order endpoints behind drag-and-drop reordering (#113). The unit tests
/// (<see cref="Unit.SparseOrderingTests"/>) cover the ordering arithmetic; these cover what the
/// endpoints do with it — that creates append, that a reorder is reflected in the next read, and that
/// an id the caller does not own is refused rather than quietly ignored.
/// </summary>
public class ReorderControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public ReorderControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized

    [Fact]
    public async Task ReorderShoppingListItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ReorderShoppingListItemsRequest
        {
            ShoppingListPublicId = Guid.NewGuid(),
            ItemPublicIds = [Guid.NewGuid()]
        };

        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/reorder", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReorderStorageLocations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ReorderLocationsRequest { LocationPublicIds = [Guid.NewGuid()] };

        var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/reorder", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReorderShoppingLocations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ReorderLocationsRequest { LocationPublicIds = [Guid.NewGuid()] };

        var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping/reorder", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReorderAutomations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ReorderAutomationsRequest { AutomationPublicIds = [Guid.NewGuid()] };

        var response = await _client.PostAsJsonAsync("/api/v1.0/automation/reorder", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Shopping list items

    [Fact]
    public async Task CreateShoppingListItems_AppendToTheEndOfTheManualOrder()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-append");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var listId = await CreateListAsync("Append order");
            var first = await CreateItemAsync(listId, "First");
            var second = await CreateItemAsync(listId, "Second");
            var third = await CreateItemAsync(listId, "Third");

            var items = await GetItemsAsync(listId);

            Assert.True(SortOrderOf(items, first) < SortOrderOf(items, second));
            Assert.True(SortOrderOf(items, second) < SortOrderOf(items, third));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task ReorderShoppingListItems_MovingOneItem_WritesOneRowAndIsReflectedInTheList()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-items");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var listId = await CreateListAsync("Aisle order");
            var a = await CreateItemAsync(listId, "Aaa item");
            var b = await CreateItemAsync(listId, "Bbb item");
            var c = await CreateItemAsync(listId, "Ccc item");

            // First reorder on a list whose rows are all at the migration default has to number the
            // list, so ask for the order it is already in and let that be the numbering pass.
            await ReorderItemsAsync(listId, [a, b, c]);

            // Now the move that this feature is actually about: one item dragged to the front.
            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/reorder", new ReorderShoppingListItemsRequest
            {
                ShoppingListPublicId = listId,
                ItemPublicIds = [c, a, b]
            });
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Reorder status: {response.StatusCode}, body: {body}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var moved = (await response.Content.ReadFromJsonAsync<ApiResponse<List<ReorderedEntry>>>())?.Data;
            Assert.NotNull(moved);
            // Sparse positions: moving one row to the front rewrites that row and nothing else.
            var entry = Assert.Single(moved);
            Assert.Equal(c, entry.PublicId);

            var items = await GetItemsAsync(listId);
            Assert.True(SortOrderOf(items, c) < SortOrderOf(items, a));
            Assert.True(SortOrderOf(items, a) < SortOrderOf(items, b));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task ReorderShoppingListItems_ItemFromAnotherList_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-foreign");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var listId = await CreateListAsync("Target list");
            var otherListId = await CreateListAsync("Other list");
            var mine = await CreateItemAsync(listId, "Mine item");
            var elsewhere = await CreateItemAsync(otherListId, "Elsewhere item");

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/reorder", new ReorderShoppingListItemsRequest
            {
                ShoppingListPublicId = listId,
                ItemPublicIds = [elsewhere, mine]
            });
            _output.WriteLine($"Status: {response.StatusCode}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task ReorderShoppingListItems_DuplicateId_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-dupe");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var listId = await CreateListAsync("Duplicate order");
            var item = await CreateItemAsync(listId, "Only item");

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/reorder", new ReorderShoppingListItemsRequest
            {
                ShoppingListPublicId = listId,
                ItemPublicIds = [item, item]
            });
            _output.WriteLine($"Status: {response.StatusCode}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    #endregion

    #region Storage locations

    [Fact]
    public async Task ReorderStorageLocations_IsReflectedInTheListOrder()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-storage");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Names chosen so the requested order is NOT the alphabetical one the endpoint falls back
            // to — otherwise the assertion would pass without the reorder doing anything.
            var alpha = await CreateStorageLocationAsync("Alpha pantry");
            var bravo = await CreateStorageLocationAsync("Bravo pantry");
            var charlie = await CreateStorageLocationAsync("Charlie pantry");

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/reorder", new ReorderLocationsRequest
            {
                LocationPublicIds = [charlie, alpha, bravo]
            });
            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var listResponse = await _client.GetAsync("/api/v1.0/location/storage?ReturnAll=true");
            var page = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<StorageLocationInfo>>>())?.Data;
            Assert.NotNull(page);

            var order = page.Items.Select(l => l.PublicId).ToList();
            Assert.Equal([charlie, alpha, bravo], order);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task ReorderStorageLocations_UnknownId_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-unknown");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var mine = await CreateStorageLocationAsync("Only pantry");

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/reorder", new ReorderLocationsRequest
            {
                LocationPublicIds = [mine, Guid.NewGuid()]
            });
            _output.WriteLine($"Status: {response.StatusCode}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    #endregion

    #region Shopping locations

    [Fact]
    public async Task ReorderShoppingLocations_IsReflectedInTheListOrder()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("reorder-shops");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var alpha = await CreateShoppingLocationAsync("Alpha market");
            var bravo = await CreateShoppingLocationAsync("Bravo market");

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping/reorder", new ReorderLocationsRequest
            {
                LocationPublicIds = [bravo, alpha]
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var listResponse = await _client.GetAsync("/api/v1.0/location/shopping?ReturnAll=true");
            var page = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ShoppingLocationInfo>>>())?.Data;
            Assert.NotNull(page);

            Assert.Equal([bravo, alpha], page.Items.Select(l => l.PublicId).ToList());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    #endregion

    #region Helpers

    private async Task<Guid> CreateListAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", new CreateShoppingListRequest { Name = name });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    private async Task<Guid> CreateItemAsync(Guid listId, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", new CreateShoppingListItemRequest
        {
            ShoppingListPublicId = listId,
            CustomName = name,
            Quantity = 1,
            Unit = ProductUnit.Piece
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    private async Task ReorderItemsAsync(Guid listId, List<Guid> itemIds)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/reorder", new ReorderShoppingListItemsRequest
        {
            ShoppingListPublicId = listId,
            ItemPublicIds = itemIds
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<ShoppingListItemInfo>> GetItemsAsync(Guid listId)
    {
        var response = await _client.GetAsync($"/api/v1.0/shoppinglist/{listId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<DetailedShoppingListInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.Items;
    }

    private static int SortOrderOf(List<ShoppingListItemInfo> items, Guid publicId)
        => items.Single(i => i.PublicId == publicId).SortOrder;

    private async Task<Guid> CreateStorageLocationAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage", new StorageLocationRequest { Name = name });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<StorageLocationInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    private async Task<Guid> CreateShoppingLocationAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", new ShoppingLocationRequest { Name = name });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingLocationInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    #endregion
}
