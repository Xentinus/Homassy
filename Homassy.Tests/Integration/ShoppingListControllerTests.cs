using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.ShoppingList;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;
using ProductUnit = Homassy.API.Enums.Unit;

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
}
