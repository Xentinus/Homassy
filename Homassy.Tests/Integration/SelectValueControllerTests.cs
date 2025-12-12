using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Homassy.API.Models.ShoppingList;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class SelectValueControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public SelectValueControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized Tests
    [Theory]
    [InlineData(SelectValueType.ShoppingLocation)]
    [InlineData(SelectValueType.StorageLocation)]
    [InlineData(SelectValueType.Product)]
    [InlineData(SelectValueType.ProductInventoryItem)]
    [InlineData(SelectValueType.ShoppingList)]
    public async Task GetSelectValues_WithoutToken_ReturnsUnauthorized(SelectValueType type)
    {
        var response = await _client.GetAsync($"/api/v1.0/selectvalue/{(int)type}");
        _output.WriteLine($"Type: {type}, Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSelectValues_WithoutToken_UsingEnumName_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/selectvalue/ShoppingList");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Valid Request Tests
    [Theory]
    [InlineData(SelectValueType.ShoppingLocation)]
    [InlineData(SelectValueType.StorageLocation)]
    [InlineData(SelectValueType.Product)]
    [InlineData(SelectValueType.ProductInventoryItem)]
    [InlineData(SelectValueType.ShoppingList)]
    public async Task GetSelectValues_WithToken_ReturnsOk(SelectValueType type)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"select-{type}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync($"/api/v1.0/selectvalue/{(int)type}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Type: {type}");
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<SelectValue>>>();
            Assert.NotNull(content);
            Assert.True(content.Success);
            Assert.NotNull(content.Data);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetSelectValues_UsingEnumName_ReturnsOk()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/selectvalue/ShoppingList");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetSelectValues_EmptyResult_ReturnsEmptyList()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // New user should have no shopping locations
            var response = await _client.GetAsync("/api/v1.0/selectvalue/0"); // ShoppingLocation
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<SelectValue>>>();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Count: {content?.Data?.Count}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content?.Data);
            Assert.Empty(content.Data);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Invalid Request Tests
    [Fact]
    public async Task GetSelectValues_InvalidEnumValue_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-invalid");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Use an invalid enum value (999)
            var response = await _client.GetAsync("/api/v1.0/selectvalue/999");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Should return BadRequest for invalid enum value
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
    public async Task GetSelectValues_InvalidEnumString_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-badstr");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Use an invalid enum string
            var response = await _client.GetAsync("/api/v1.0/selectvalue/InvalidType");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Should return BadRequest for invalid enum string
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
    public async Task GetSelectValues_NegativeEnumValue_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-neg");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Use a negative enum value
            var response = await _client.GetAsync("/api/v1.0/selectvalue/-1");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Should return BadRequest for negative enum value
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

    #region Data Verification Tests
    [Fact]
    public async Task GetSelectValues_ShoppingList_ReturnsCreatedList()
    {
        string? testEmail = null;
        Guid? createdListId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-verify-list");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create a shopping list
            var listName = "Test Shopping List for SelectValue";
            var createRequest = new CreateShoppingListRequest { Name = listName };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
            createdListId = createContent?.Data?.PublicId;

            _output.WriteLine($"Created list: {createdListId}");

            // Wait for cache to refresh (CacheManagementService refreshes every 5 seconds)
            await Task.Delay(6000);

            // Get select values for ShoppingList
            var response = await _client.GetAsync("/api/v1.0/selectvalue/ShoppingList");
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<SelectValue>>>();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Count: {content?.Data?.Count}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content?.Data);
            Assert.NotEmpty(content.Data);

            // Verify the created list is in the results
            var foundList = content.Data.FirstOrDefault(sv => sv.PublicId == createdListId);
            Assert.NotNull(foundList);
            Assert.Equal(listName, foundList.Text);

            _output.WriteLine($"Found list in select values: {foundList.Text}");

            // Cleanup - delete the list
            await _client.DeleteAsync($"/api/v1.0/shoppinglist/{createdListId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetSelectValues_StorageLocation_ReturnsCreatedLocation()
    {
        string? testEmail = null;
        Guid? createdLocationId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-verify-storage");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create a storage location
            var locationName = "Test Fridge";
            var createRequest = new StorageLocationRequest { Name = locationName, IsFreezer = false };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/location/storage", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<StorageLocationInfo>>();
            createdLocationId = createContent?.Data?.PublicId;

            _output.WriteLine($"Created storage location: {createdLocationId}");

            // Wait for cache to refresh (CacheManagementService refreshes every 5 seconds)
            await Task.Delay(6000);

            // Get select values for StorageLocation
            var response = await _client.GetAsync("/api/v1.0/selectvalue/StorageLocation");
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<SelectValue>>>();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Count: {content?.Data?.Count}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content?.Data);
            Assert.NotEmpty(content.Data);

            // Verify the created location is in the results
            var foundLocation = content.Data.FirstOrDefault(sv => sv.PublicId == createdLocationId);
            Assert.NotNull(foundLocation);
            Assert.Equal(locationName, foundLocation.Text);

            _output.WriteLine($"Found location in select values: {foundLocation.Text}");

            // Cleanup - delete the location
            await _client.DeleteAsync($"/api/v1.0/location/storage/{createdLocationId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetSelectValues_ShoppingLocation_ReturnsCreatedLocation()
    {
        string? testEmail = null;
        Guid? createdLocationId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-verify-shop");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create a shopping location
            var locationName = "Test Supermarket";
            var createRequest = new ShoppingLocationRequest { Name = locationName };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingLocationInfo>>();
            createdLocationId = createContent?.Data?.PublicId;

            _output.WriteLine($"Created shopping location: {createdLocationId}");

            // Wait for cache to refresh (CacheManagementService refreshes every 5 seconds)
            await Task.Delay(6000);

            // Get select values for ShoppingLocation
            var response = await _client.GetAsync("/api/v1.0/selectvalue/ShoppingLocation");
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<SelectValue>>>();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Count: {content?.Data?.Count}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content?.Data);
            Assert.NotEmpty(content.Data);

            // Verify the created location is in the results
            var foundLocation = content.Data.FirstOrDefault(sv => sv.PublicId == createdLocationId);
            Assert.NotNull(foundLocation);
            Assert.Equal(locationName, foundLocation.Text);

            _output.WriteLine($"Found location in select values: {foundLocation.Text}");

            // Cleanup - delete the location
            await _client.DeleteAsync($"/api/v1.0/location/shopping/{createdLocationId}");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetSelectValues_MultipleItems_ReturnsSortedByText()
    {
        string? testEmail = null;
        var createdListIds = new List<Guid>();
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("select-sorted");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create multiple shopping lists with different names (not alphabetically sorted)
            var names = new[] { "Zebra List", "Apple List", "Mango List" };
            foreach (var name in names)
            {
                var createRequest = new CreateShoppingListRequest { Name = name };
                var createResponse = await _client.PostAsJsonAsync("/api/v1.0/shoppinglist", createRequest);
                var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingListInfo>>();
                if (createContent?.Data?.PublicId != null)
                {
                    createdListIds.Add(createContent.Data.PublicId);
                }
            }

            _output.WriteLine($"Created {createdListIds.Count} lists");

            // Wait for cache to refresh (CacheManagementService refreshes every 5 seconds)
            await Task.Delay(6000);

            // Get select values for ShoppingList
            var response = await _client.GetAsync("/api/v1.0/selectvalue/ShoppingList");
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<SelectValue>>>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content?.Data);
            Assert.True(content.Data.Count >= 3);

            // Verify results are sorted alphabetically
            var texts = content.Data.Select(sv => sv.Text).ToList();
            var sortedTexts = texts.OrderBy(t => t).ToList();
            
            _output.WriteLine($"Actual order: {string.Join(", ", texts)}");
            _output.WriteLine($"Expected order: {string.Join(", ", sortedTexts)}");

            Assert.Equal(sortedTexts, texts);

            // Cleanup
            foreach (var id in createdListIds)
            {
                await _client.DeleteAsync($"/api/v1.0/shoppinglist/{id}");
            }
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Case Sensitivity Tests
    [Theory]
    [InlineData("shoppinglist")]
    [InlineData("SHOPPINGLIST")]
    [InlineData("ShoppingList")]
    [InlineData("shoppingList")]
    public async Task GetSelectValues_CaseInsensitiveEnumName_ReturnsOk(string enumName)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"select-case-{enumName[..4]}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync($"/api/v1.0/selectvalue/{enumName}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Enum name: {enumName}");
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // ASP.NET Core enum binding is case-insensitive by default
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
