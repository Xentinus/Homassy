using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Product;
using Homassy.API.Models.ShoppingList;
using Homassy.Data.Models.Common;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;
using ProductUnit = Homassy.Data.Enums.Unit;

namespace Homassy.Tests.Integration;

/// <summary>
/// The "load inventory from shopping list" flow (#63): which items the picker offers, and what
/// loading one does to the item it came from.
/// </summary>
public class LoadInventoryFromShoppingListTests : IClassFixture<HomassyWebApplicationFactory>
{
    private const string LoadableEndpoint = "/api/v1.0/shoppinglist/item/loadable-inventory";

    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public LoadInventoryFromShoppingListTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    [Fact]
    public async Task GetLoadableInventoryItems_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(LoadableEndpoint);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A free-text item has no product to attach stock to, so it must never reach the picker — the
    /// per-item step would have nothing to create.
    /// </summary>
    [Fact]
    public async Task GetLoadableInventoryItems_OffersProductItemsOnly()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId = null;
        Guid? productItemId = null;
        Guid? customItemId = null;

        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loadable-products");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            productId = await CreateProductAsync("Loadable Milk");
            listId = await CreateListAsync("Loadable List");
            productItemId = await CreateItemAsync(listId.Value, productId, null);
            customItemId = await CreateItemAsync(listId.Value, null, "Loadable Custom");

            var items = await GetLoadableAsync();

            Assert.Contains(items, i => i.PublicId == productItemId);
            Assert.DoesNotContain(items, i => i.PublicId == customItemId);

            var offered = items.First(i => i.PublicId == productItemId);
            Assert.Equal(productId, offered.ProductPublicId);
            Assert.Equal("Loadable List", offered.ShoppingListName);
            Assert.Null(offered.PurchasedAt);
        }
        finally
        {
            await CleanupAsync(listId, productId, testEmail, productItemId, customItemId);
        }
    }

    /// <summary>
    /// Loading an item into the stock is what takes it out of the picker — otherwise the same
    /// shopping trip could be stocked twice over.
    /// </summary>
    [Fact]
    public async Task GetLoadableInventoryItems_ExcludesItemsAlreadyLoaded()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId = null;
        Guid? itemId = null;

        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loadable-once");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            productId = await CreateProductAsync("Loaded Once");
            listId = await CreateListAsync("Loaded Once List");
            itemId = await CreateItemAsync(listId.Value, productId, null);

            Assert.Contains(await GetLoadableAsync(), i => i.PublicId == itemId);

            var purchaseResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/shoppinglist/item/quick-purchase",
                new QuickPurchaseFromShoppingListItemRequest
                {
                    ShoppingListItemPublicId = itemId!.Value,
                    PurchasedAt = DateTime.UtcNow,
                    Quantity = 2
                });

            _output.WriteLine($"Quick purchase status: {purchaseResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, purchaseResponse.StatusCode);

            Assert.DoesNotContain(await GetLoadableAsync(), i => i.PublicId == itemId);
        }
        finally
        {
            await CleanupAsync(listId, productId, testEmail, itemId);
        }
    }

    /// <summary>
    /// The picker's whole point is stocking a trip that already happened, so loading an item that
    /// was bought days ago must not rewrite the shopping history to say it was bought today.
    /// </summary>
    [Fact]
    public async Task QuickPurchase_KeepsTheOriginalPurchaseDate()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId = null;
        Guid? itemId = null;

        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loadable-date");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            productId = await CreateProductAsync("Old Purchase");
            listId = await CreateListAsync("Old Purchase List");
            itemId = await CreateItemAsync(listId.Value, productId, null);

            var boughtOn = DateTime.UtcNow.AddDays(-5);
            var markResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/shoppinglist/item/purchase",
                new PurchaseShoppingListItemRequest
                {
                    ShoppingListItemPublicId = itemId!.Value,
                    PurchasedAt = boughtOn
                });

            Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);

            var loadResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/shoppinglist/item/quick-purchase",
                new QuickPurchaseFromShoppingListItemRequest
                {
                    ShoppingListItemPublicId = itemId.Value,
                    PurchasedAt = DateTime.UtcNow,
                    Quantity = 2
                });

            var body = await loadResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {loadResponse.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, loadResponse.StatusCode);

            var loaded = await loadResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            Assert.NotNull(loaded?.Data?.PurchasedAt);
            Assert.True(
                Math.Abs((loaded.Data.PurchasedAt!.Value - boughtOn).TotalMinutes) < 1,
                $"Expected the original purchase date ({boughtOn:u}), got {loaded.Data.PurchasedAt:u}");
        }
        finally
        {
            await CleanupAsync(listId, productId, testEmail, itemId);
        }
    }

    private async Task<List<LoadableShoppingListItemInfo>> GetLoadableAsync()
    {
        var response = await _client.GetAsync($"{LoadableEndpoint}?PageNumber=1&PageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LoadableShoppingListItemInfo>>>();
        return content?.Data?.Items ?? [];
    }

    private async Task<Guid> CreateProductAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/product", new CreateProductRequest
        {
            Unit = ProductUnit.Piece,
            Name = name,
            Brand = "Test Brand"
        });

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    private async Task<Guid> CreateListAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", new CreateShoppingListRequest { Name = name });
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    private async Task<Guid> CreateItemAsync(Guid listId, Guid? productId, string? customName)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", new CreateShoppingListItemRequest
        {
            ShoppingListPublicId = listId,
            ProductPublicId = productId,
            CustomName = customName,
            Quantity = 2,
            Unit = ProductUnit.Piece
        });

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    private async Task CleanupAsync(Guid? listId, Guid? productId, string? testEmail, params Guid?[] itemIds)
    {
        foreach (var itemId in itemIds.Where(i => i.HasValue))
            await _client.DeleteAsync($"/api/v1.0/shoppinglist/item/{itemId}");

        if (listId.HasValue)
            await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
        if (productId.HasValue)
            await _client.DeleteAsync($"/api/v1.0/product/{productId}");

        _authHelper.ClearAuthToken();
        if (testEmail != null)
            await _authHelper.CleanupUserAsync(testEmail);
    }
}
