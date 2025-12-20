using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Homassy.API.Models.ShoppingList;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;
using ProductUnit = Homassy.API.Enums.Unit;
using ProductCurrency = Homassy.API.Enums.Currency;

namespace Homassy.Tests.Integration;

public class ShoppingListControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public ShoppingListControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized Tests - Shopping List
    [Fact]
    public async Task GetShoppingLists_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/shoppinglist");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetShoppingList_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1.0/shoppinglist/{Guid.NewGuid()}");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateShoppingList_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateShoppingListRequest { Name = "Test List" };
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateShoppingList_WithoutToken_ReturnsUnauthorized()
    {
        var request = new UpdateShoppingListRequest { Name = "Updated List" };
        var response = await _client.PutAsJsonAsync($"/api/v1.0/shoppinglist/{Guid.NewGuid()}", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShoppingList_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1.0/shoppinglist/{Guid.NewGuid()}");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Unauthorized Tests - Shopping List Items
    [Fact]
    public async Task CreateShoppingListItem_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateShoppingListItemRequest
        {
            ShoppingListPublicId = Guid.NewGuid(),
            CustomName = "Test Item",
            Quantity = 1,
            Unit = ProductUnit.Piece
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateShoppingListItem_WithoutToken_ReturnsUnauthorized()
    {
        var request = new UpdateShoppingListItemRequest { Quantity = 2 };
        var response = await _client.PutAsJsonAsync($"/api/v1.0/shoppinglist/item/{Guid.NewGuid()}", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShoppingListItem_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1.0/shoppinglist/item/{Guid.NewGuid()}");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Validation Tests
    [Fact]
    public async Task CreateShoppingList_MissingName_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-no-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1.0/shoppinglist", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

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
    public async Task CreateShoppingList_NameTooShort_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-short");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateShoppingListRequest { Name = "A" }; // Too short

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

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
    public async Task CreateShoppingList_InvalidColor_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-color");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateShoppingListRequest
            {
                Name = "Test List",
                Color = "not-a-color"
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

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
    public async Task CreateShoppingListItem_ZeroQuantity_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("item-zero-qty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = Guid.NewGuid(),
                CustomName = "Test Item",
                Quantity = 0, // Invalid, must be > 0
                Unit = ProductUnit.Piece
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

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
    public async Task CreateShoppingListItem_NoProductOrCustomName_ReturnsBadRequest()
    {
        string? testEmail = null;
        Guid? listId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("item-no-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // First create a shopping list
            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;

            // Try to create item without ProductPublicId or CustomName
            var request = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                // No ProductPublicId
                // No CustomName
                Quantity = 1,
                Unit = ProductUnit.Piece
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Should fail validation - needs either ProductPublicId or CustomName
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Not Found Tests
    [Fact]
    public async Task GetShoppingList_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync($"/api/v1.0/shoppinglist/{Guid.NewGuid()}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UpdateShoppingList_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-upd-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateShoppingListRequest { Name = "Updated List" };
            var response = await _client.PutAsJsonAsync($"/api/v1.0/shoppinglist/{Guid.NewGuid()}", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task DeleteShoppingListItem_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("item-del-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.DeleteAsync($"/api/v1.0/shoppinglist/item/{Guid.NewGuid()}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Full Flow Tests
    [Fact]
    public async Task ShoppingList_FullCRUD_Succeeds()
    {
        string? testEmail = null;
        Guid? createdListId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-crud");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Step 1: Get initial lists
            _output.WriteLine("=== Step 1: Get Shopping Lists ===");
            var getResponse = await _client.GetAsync("/api/v1.0/shoppinglist");
            var getBody = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get Status: {getResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // Step 2: Create list
            _output.WriteLine("\n=== Step 2: Create Shopping List ===");
            var createRequest = new CreateShoppingListRequest
            {
                Name = "Weekly Groceries",
                Description = "Weekly shopping list",
                Color = "#4CAF50"
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create Status: {createResponse.StatusCode}");
            _output.WriteLine($"Create Response: {createBody}");
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            Assert.NotNull(createContent?.Data);
            createdListId = createContent.Data.PublicId;
            _output.WriteLine($"Created List ID: {createdListId}");

            // Step 3: Get detailed list
            _output.WriteLine("\n=== Step 3: Get Detailed Shopping List ===");
            var detailedResponse = await _client.GetAsync($"/api/v1.0/shoppinglist/{createdListId}");
            var detailedBody = await detailedResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Detailed Status: {detailedResponse.StatusCode}");
            _output.WriteLine($"Detailed Response: {detailedBody}");
            Assert.Equal(HttpStatusCode.OK, detailedResponse.StatusCode);

            // Step 4: Update list
            _output.WriteLine("\n=== Step 4: Update Shopping List ===");
            var updateRequest = new UpdateShoppingListRequest
            {
                Name = "Updated Weekly Groceries",
                Color = "#2196F3"
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1.0/shoppinglist/{createdListId}", updateRequest);
            var updateBody = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update Status: {updateResponse.StatusCode}");
            _output.WriteLine($"Update Response: {updateBody}");
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Step 5: Add item to list
            _output.WriteLine("\n=== Step 5: Add Item to Shopping List ===");
            var itemRequest = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = createdListId.Value,
                CustomName = "Milk",
                Quantity = 2,
                Unit = ProductUnit.Liter,
                Note = "Low fat"
            };
            var itemResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", itemRequest);
            var itemBody = await itemResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Add Item Status: {itemResponse.StatusCode}");
            _output.WriteLine($"Add Item Response: {itemBody}");
            Assert.Equal(HttpStatusCode.OK, itemResponse.StatusCode);

            var itemContent = await itemResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            var itemId = itemContent?.Data?.PublicId;

            // Step 6: Update item
            _output.WriteLine("\n=== Step 6: Update Shopping List Item ===");
            var updateItemRequest = new UpdateShoppingListItemRequest
            {
                Quantity = 3,
                Note = "Extra low fat"
            };
            var updateItemResponse = await _client.PutAsJsonAsync($"/api/v1.0/shoppinglist/item/{itemId}", updateItemRequest);
            var updateItemBody = await updateItemResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update Item Status: {updateItemResponse.StatusCode}");
            _output.WriteLine($"Update Item Response: {updateItemBody}");
            Assert.Equal(HttpStatusCode.OK, updateItemResponse.StatusCode);

            // Step 7: Delete item
            _output.WriteLine("\n=== Step 7: Delete Shopping List Item ===");
            var deleteItemResponse = await _client.DeleteAsync($"/api/v1.0/shoppinglist/item/{itemId}");
            var deleteItemBody = await deleteItemResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete Item Status: {deleteItemResponse.StatusCode}");
            _output.WriteLine($"Delete Item Response: {deleteItemBody}");
            Assert.Equal(HttpStatusCode.OK, deleteItemResponse.StatusCode);

            // Step 8: Delete list
            _output.WriteLine("\n=== Step 8: Delete Shopping List ===");
            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/shoppinglist/{createdListId}");
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete Status: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete Response: {deleteBody}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            _output.WriteLine("\n=== Shopping List CRUD Flow Completed Successfully! ===");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetShoppingList_WithShowPurchased_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? createdListId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-purchased");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create a list
            var createRequest = new CreateShoppingListRequest { Name = "Test List" };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            createdListId = createContent?.Data?.PublicId;

            // Get with showPurchased=true
            var response = await _client.GetAsync($"/api/v1.0/shoppinglist/{createdListId}?showPurchased=true");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Cleanup
            await _client.DeleteAsync($"/api/v1.0/shoppinglist/{createdListId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region QuickPurchaseFromShoppingListItem Tests
    [Fact]
    public async Task QuickPurchaseFromShoppingListItem_WithoutToken_ReturnsUnauthorized()
    {
        var request = new QuickPurchaseFromShoppingListItemRequest
        {
            ShoppingListItemPublicId = Guid.NewGuid(),
            PurchasedAt = DateTime.UtcNow,
            Quantity = 1
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QuickPurchaseFromShoppingListItem_NonExistentItem_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = Guid.NewGuid(),
                PurchasedAt = DateTime.UtcNow,
                Quantity = 1
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task QuickPurchaseFromShoppingListItem_InvalidQuantity_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-bad-qty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = Guid.NewGuid(),
                PurchasedAt = DateTime.UtcNow,
                Quantity = 0
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

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
    public async Task QuickPurchaseFromShoppingListItem_CustomItem_ReturnsBadRequest()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? itemId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-custom");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            _output.WriteLine("=== Step 1: Create Shopping List ===");
            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;
            _output.WriteLine($"List created: {listId}");

            _output.WriteLine("\n=== Step 2: Create Custom Item ===");
            var itemRequest = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                CustomName = "Custom Milk",
                Quantity = 2,
                Unit = ProductUnit.Liter
            };
            var itemResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", itemRequest);
            var itemContent = await itemResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            itemId = itemContent?.Data?.PublicId;
            _output.WriteLine($"Item created: {itemId}");

            _output.WriteLine("\n=== Step 3: Try Quick Purchase (should fail) ===");
            var purchaseRequest = new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = itemId!.Value,
                PurchasedAt = DateTime.UtcNow,
                Quantity = 2
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", purchaseRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("SHOPPINGLIST-0004", responseBody);

            if (itemId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/item/{itemId}");
            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task QuickPurchaseFromShoppingListItem_NonExistentStorageLocation_ReturnsNotFound()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId = null;
        Guid? itemId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-no-loc");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var productRequest = new CreateProductRequest { Name = "Test Product", Brand = "Test Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;

            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;

            var itemRequest = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                ProductPublicId = productId!.Value,
                Quantity = 2,
                Unit = ProductUnit.Piece
            };
            var itemResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", itemRequest);
            var itemContent = await itemResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            itemId = itemContent?.Data?.PublicId;

            var purchaseRequest = new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = itemId!.Value,
                PurchasedAt = DateTime.UtcNow,
                Quantity = 2,
                StorageLocationPublicId = Guid.NewGuid()
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", purchaseRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            if (itemId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/item/{itemId}");
            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
            if (productId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task QuickPurchaseFromShoppingListItem_FullFlow_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId = null;
        Guid? itemId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            _output.WriteLine("=== Step 1: Create Product ===");
            var productRequest = new CreateProductRequest
            {
                Name = "Organic Milk",
                Brand = "Test Dairy",
                IsEatable = true
            };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;
            _output.WriteLine($"Product created: {productId}");

            _output.WriteLine("\n=== Step 2: Create Shopping List ===");
            var listRequest = new CreateShoppingListRequest { Name = "Grocery Shopping" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;
            _output.WriteLine($"List created: {listId}");

            _output.WriteLine("\n=== Step 3: Create Shopping List Item ===");
            var itemRequest = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                ProductPublicId = productId!.Value,
                Quantity = 2,
                Unit = ProductUnit.Liter,
                Note = "Need for breakfast"
            };
            var itemResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", itemRequest);
            var itemContent = await itemResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            itemId = itemContent?.Data?.PublicId;
            _output.WriteLine($"Item created: {itemId}");

            _output.WriteLine("\n=== Step 4: Quick Purchase ===");
            var purchaseRequest = new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = itemId!.Value,
                PurchasedAt = DateTime.UtcNow,
                Quantity = 2,
                Price = 500,
                Currency = ProductCurrency.Huf,
                ExpirationAt = DateTime.UtcNow.AddDays(7),
                IsSharedWithFamily = false
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", purchaseRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            Assert.NotNull(content?.Data);
            Assert.NotNull(content.Data.PurchasedAt);

            _output.WriteLine($"\nShopping List Item PurchasedAt: {content.Data.PurchasedAt}");

            _output.WriteLine("\n=== Quick Purchase Flow Completed Successfully! ===");

            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
            if (productId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task QuickPurchaseFromShoppingListItem_WithPurchaseInfo_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId = null;
        Guid? itemId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-purchase-info");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var productRequest = new CreateProductRequest { Name = "Test Product", Brand = "Test Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;

            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;

            var itemRequest = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                ProductPublicId = productId!.Value,
                Quantity = 1,
                Unit = ProductUnit.Piece
            };
            var itemResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", itemRequest);
            var itemContent = await itemResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            itemId = itemContent?.Data?.PublicId;

            var purchaseDate = DateTime.UtcNow;
            var purchaseRequest = new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = itemId!.Value,
                PurchasedAt = purchaseDate,
                Quantity = 3,
                Price = 1500,
                Currency = ProductCurrency.Huf
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase", purchaseRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            Assert.NotNull(content?.Data);
            Assert.NotNull(content.Data.PurchasedAt);

            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
            if (productId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region CreateMultipleShoppingListItems Tests
    [Fact]
    public async Task CreateMultipleShoppingListItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateMultipleShoppingListItemsRequest
        {
            ShoppingListPublicId = Guid.NewGuid(),
            Items = [new CreateShoppingListItemEntry { CustomName = "Test", Quantity = 1, Unit = ProductUnit.Piece }]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/multiple", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMultipleShoppingListItems_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-item-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleShoppingListItemsRequest
            {
                ShoppingListPublicId = Guid.NewGuid(),
                Items = []
            };
            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

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
    public async Task CreateMultipleShoppingListItems_NonExistentList_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-item-nf");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleShoppingListItemsRequest
            {
                ShoppingListPublicId = Guid.NewGuid(),
                Items = [new CreateShoppingListItemEntry { CustomName = "Test Item", Quantity = 1, Unit = ProductUnit.Piece }]
            };
            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task CreateMultipleShoppingListItems_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? listId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-item-multi-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;

            var request = new CreateMultipleShoppingListItemsRequest
            {
                ShoppingListPublicId = listId!.Value,
                Items =
                [
                    new CreateShoppingListItemEntry { CustomName = "Milk", Quantity = 2, Unit = ProductUnit.Liter },
                    new CreateShoppingListItemEntry { CustomName = "Bread", Quantity = 1, Unit = ProductUnit.Piece },
                    new CreateShoppingListItemEntry { CustomName = "Eggs", Quantity = 12, Unit = ProductUnit.Piece }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<ShoppingListItemInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(3, content.Data.Count);

            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region DeleteMultipleShoppingListItems Tests
    [Fact]
    public async Task DeleteMultipleShoppingListItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new DeleteMultipleShoppingListItemsRequest { ItemPublicIds = [Guid.NewGuid()] };
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/shoppinglist/item/multiple")
        {
            Content = JsonContent.Create(request)
        };
        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMultipleShoppingListItems_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-del-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new DeleteMultipleShoppingListItemsRequest { ItemPublicIds = [] };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/shoppinglist/item/multiple")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _client.SendAsync(httpRequest);

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
    public async Task DeleteMultipleShoppingListItems_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? listId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("list-del-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;

            var createRequest = new CreateMultipleShoppingListItemsRequest
            {
                ShoppingListPublicId = listId!.Value,
                Items =
                [
                    new CreateShoppingListItemEntry { CustomName = "Item 1", Quantity = 1, Unit = ProductUnit.Piece },
                    new CreateShoppingListItemEntry { CustomName = "Item 2", Quantity = 1, Unit = ProductUnit.Piece }
                ]
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/multiple", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<List<ShoppingListItemInfo>>>();
            var createdIds = createContent?.Data?.Select(x => x.PublicId).ToList() ?? [];

            var deleteRequest = new DeleteMultipleShoppingListItemsRequest { ItemPublicIds = createdIds };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/shoppinglist/item/multiple")
            {
                Content = JsonContent.Create(deleteRequest)
            };
            var response = await _client.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region QuickPurchaseMultipleShoppingListItems Tests
    [Fact]
    public async Task QuickPurchaseMultipleShoppingListItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new QuickPurchaseMultipleShoppingListItemsRequest
        {
            Items = [new QuickPurchaseFromShoppingListItemRequest
            {
                ShoppingListItemPublicId = Guid.NewGuid(),
                PurchasedAt = DateTime.UtcNow,
                Quantity = 1
            }]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase/multiple", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QuickPurchaseMultipleShoppingListItems_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-multi-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new QuickPurchaseMultipleShoppingListItemsRequest { Items = [] };
            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase/multiple", request);

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
    public async Task QuickPurchaseMultipleShoppingListItems_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? listId = null;
        Guid? productId1 = null;
        Guid? productId2 = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("qp-multi-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var product1Request = new CreateProductRequest { Name = "Product 1", Brand = "Brand" };
            var product1Response = await _client.PostAsJsonAsync("/api/v1.0/product", product1Request);
            var product1Content = await product1Response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId1 = product1Content?.Data?.PublicId;

            var product2Request = new CreateProductRequest { Name = "Product 2", Brand = "Brand" };
            var product2Response = await _client.PostAsJsonAsync("/api/v1.0/product", product2Request);
            var product2Content = await product2Response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId2 = product2Content?.Data?.PublicId;

            var listRequest = new CreateShoppingListRequest { Name = "Test List" };
            var listResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", listRequest);
            var listContent = await listResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            listId = listContent?.Data?.PublicId;

            var item1Request = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                ProductPublicId = productId1!.Value,
                Quantity = 2,
                Unit = ProductUnit.Piece
            };
            var item1Response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", item1Request);
            var item1Content = await item1Response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            var itemId1 = item1Content?.Data?.PublicId;

            var item2Request = new CreateShoppingListItemRequest
            {
                ShoppingListPublicId = listId!.Value,
                ProductPublicId = productId2!.Value,
                Quantity = 3,
                Unit = ProductUnit.Piece
            };
            var item2Response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item", item2Request);
            var item2Content = await item2Response.Content.ReadFromJsonAsync<ApiResponse<ShoppingListItemInfo>>();
            var itemId2 = item2Content?.Data?.PublicId;

            var purchaseRequest = new QuickPurchaseMultipleShoppingListItemsRequest
            {
                Items =
                [
                    new QuickPurchaseFromShoppingListItemRequest
                    {
                        ShoppingListItemPublicId = itemId1!.Value,
                        PurchasedAt = DateTime.UtcNow,
                        Quantity = 2,
                        Price = 500,
                        Currency = ProductCurrency.Huf
                    },
                    new QuickPurchaseFromShoppingListItemRequest
                    {
                        ShoppingListItemPublicId = itemId2!.Value,
                        PurchasedAt = DateTime.UtcNow,
                        Quantity = 3,
                        Price = 750,
                        Currency = ProductCurrency.Huf
                    }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist/item/quick-purchase/multiple", purchaseRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<ShoppingListItemInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Count);
            Assert.All(content.Data, item => Assert.NotNull(item.PurchasedAt));

            if (listId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{listId}");
            if (productId1.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId1}");
            if (productId2.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId2}");
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
