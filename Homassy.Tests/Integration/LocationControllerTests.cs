using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class LocationControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public LocationControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized Tests - Shopping Locations
    [Fact]
    public async Task GetShoppingLocations_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/location/shopping");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateShoppingLocation_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ShoppingLocationRequest { Name = "Test Store" };
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateShoppingLocation_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ShoppingLocationRequest { Name = "Updated Store" };
        var response = await _client.PutAsJsonAsync($"/api/v1.0/location/shopping/{Guid.NewGuid()}", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShoppingLocation_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1.0/location/shopping/{Guid.NewGuid()}");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Unauthorized Tests - Storage Locations
    [Fact]
    public async Task GetStorageLocations_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/location/storage");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStorageLocation_WithoutToken_ReturnsUnauthorized()
    {
        var request = new StorageLocationRequest { Name = "Test Pantry" };
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStorageLocation_WithoutToken_ReturnsUnauthorized()
    {
        var request = new StorageLocationRequest { Name = "Updated Pantry" };
        var response = await _client.PutAsJsonAsync($"/api/v1.0/location/storage/{Guid.NewGuid()}", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStorageLocation_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1.0/location/storage/{Guid.NewGuid()}");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Validation Tests
    [Fact]
    public async Task CreateShoppingLocation_InvalidColor_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-invalid-color");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new ShoppingLocationRequest
            {
                Name = "Test Store",
                Color = "invalid" // Invalid hex color
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", request);
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
    public async Task CreateShoppingLocation_InvalidUrl_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-invalid-url");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new ShoppingLocationRequest
            {
                Name = "Test Store",
                Website = "not-a-valid-url"
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", request);
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
    public async Task CreateStorageLocation_InvalidColor_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("storage-invalid");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new StorageLocationRequest
            {
                Name = "Test Pantry",
                Color = "rgb(255,0,0)" // Invalid, must be hex
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage", request);
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
    #endregion

    #region Not Found Tests
    [Fact]
    public async Task UpdateShoppingLocation_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new ShoppingLocationRequest { Name = "Updated Store" };
            var response = await _client.PutAsJsonAsync($"/api/v1.0/location/shopping/{Guid.NewGuid()}", request);
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
    public async Task DeleteStorageLocation_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("storage-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.DeleteAsync($"/api/v1.0/location/storage/{Guid.NewGuid()}");
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
    public async Task ShoppingLocation_FullCRUD_Succeeds()
    {
        string? testEmail = null;
        Guid? createdLocationId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("shop-crud");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Step 1: Get initial list
            _output.WriteLine("=== Step 1: Get Shopping Locations ===");
            var getResponse = await _client.GetAsync("/api/v1.0/location/shopping");
            var getBody = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get Status: {getResponse.StatusCode}");
            _output.WriteLine($"Get Response: {getBody}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // Step 2: Create
            _output.WriteLine("\n=== Step 2: Create Shopping Location ===");
            var createRequest = new ShoppingLocationRequest
            {
                Name = "Test Supermarket",
                Description = "A test supermarket",
                Color = "#FF5733",
                Address = "123 Test Street",
                City = "Test City"
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create Status: {createResponse.StatusCode}");
            _output.WriteLine($"Create Response: {createBody}");
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ShoppingLocationInfo>>();
            Assert.NotNull(createContent?.Data);
            createdLocationId = createContent.Data.PublicId;
            _output.WriteLine($"Created Location ID: {createdLocationId}");

            // Step 3: Update
            _output.WriteLine("\n=== Step 3: Update Shopping Location ===");
            var updateRequest = new ShoppingLocationRequest
            {
                Name = "Updated Supermarket",
                Color = "#33FF57"
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1.0/location/shopping/{createdLocationId}", updateRequest);
            var updateBody = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update Status: {updateResponse.StatusCode}");
            _output.WriteLine($"Update Response: {updateBody}");
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Step 4: Delete
            _output.WriteLine("\n=== Step 4: Delete Shopping Location ===");
            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/location/shopping/{createdLocationId}");
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete Status: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete Response: {deleteBody}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            _output.WriteLine("\n=== Shopping Location CRUD Flow Completed Successfully! ===");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task StorageLocation_FullCRUD_Succeeds()
    {
        string? testEmail = null;
        Guid? createdLocationId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("storage-crud");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Step 1: Get initial list
            _output.WriteLine("=== Step 1: Get Storage Locations ===");
            var getResponse = await _client.GetAsync("/api/v1.0/location/storage");
            var getBody = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get Status: {getResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // Step 2: Create
            _output.WriteLine("\n=== Step 2: Create Storage Location ===");
            var createRequest = new StorageLocationRequest
            {
                Name = "Kitchen Pantry",
                Description = "Main food storage",
                Color = "#3357FF",
                IsFreezer = false
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/location/storage", createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create Status: {createResponse.StatusCode}");
            _output.WriteLine($"Create Response: {createBody}");
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<StorageLocationInfo>>();
            Assert.NotNull(createContent?.Data);
            createdLocationId = createContent.Data.PublicId;

            // Step 3: Create Freezer
            _output.WriteLine("\n=== Step 3: Create Freezer Location ===");
            var freezerRequest = new StorageLocationRequest
            {
                Name = "Deep Freezer",
                IsFreezer = true,
                Color = "#00BFFF"
            };
            var freezerResponse = await _client.PostAsJsonAsync("/api/v1.0/location/storage", freezerRequest);
            Assert.Equal(HttpStatusCode.OK, freezerResponse.StatusCode);

            var freezerContent = await freezerResponse.Content.ReadFromJsonAsync<ApiResponse<StorageLocationInfo>>();
            var freezerId = freezerContent?.Data?.PublicId;

            // Step 4: Update
            _output.WriteLine("\n=== Step 4: Update Storage Location ===");
            var updateRequest = new StorageLocationRequest
            {
                Name = "Updated Pantry",
                Description = "Updated description"
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1.0/location/storage/{createdLocationId}", updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Step 5: Delete both
            _output.WriteLine("\n=== Step 5: Delete Storage Locations ===");
            var deleteResponse1 = await _client.DeleteAsync($"/api/v1.0/location/storage/{createdLocationId}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse1.StatusCode);

            if (freezerId.HasValue)
            {
                var deleteResponse2 = await _client.DeleteAsync($"/api/v1.0/location/storage/{freezerId}");
                Assert.Equal(HttpStatusCode.OK, deleteResponse2.StatusCode);
            }

            _output.WriteLine("\n=== Storage Location CRUD Flow Completed Successfully! ===");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region CreateMultipleStorageLocations Tests
    [Fact]
    public async Task CreateMultipleStorageLocations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateMultipleStorageLocationsRequest
        {
            Locations = [new StorageLocationRequest { Name = "Test" }]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/multiple", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMultipleStorageLocations_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-stor-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleStorageLocationsRequest { Locations = [] };
            var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/multiple", request);
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
    public async Task CreateMultipleStorageLocations_MissingName_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-stor-noname");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleStorageLocationsRequest
            {
                Locations = [new StorageLocationRequest { Description = "No name" }]
            };
            var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/multiple", request);
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
    public async Task CreateMultipleStorageLocations_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        var createdIds = new List<Guid>();
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-stor-multi-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleStorageLocationsRequest
            {
                Locations =
                [
                    new StorageLocationRequest { Name = "Fridge", Color = "#0000FF", IsFreezer = false },
                    new StorageLocationRequest { Name = "Freezer", Color = "#00FFFF", IsFreezer = true },
                    new StorageLocationRequest { Name = "Pantry", Description = "Dry goods" }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/storage/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<StorageLocationInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(3, content.Data.Count);

            foreach (var loc in content.Data)
            {
                createdIds.Add(loc.PublicId);
            }

            foreach (var id in createdIds)
            {
                await _client.DeleteAsync($"/api/v1.0/location/storage/{id}");
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

    #region CreateMultipleShoppingLocations Tests
    [Fact]
    public async Task CreateMultipleShoppingLocations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateMultipleShoppingLocationsRequest
        {
            Locations = [new ShoppingLocationRequest { Name = "Test" }]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping/multiple", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMultipleShoppingLocations_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-shop-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleShoppingLocationsRequest { Locations = [] };
            var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping/multiple", request);
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
    public async Task CreateMultipleShoppingLocations_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        var createdIds = new List<Guid>();
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-shop-multi-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleShoppingLocationsRequest
            {
                Locations =
                [
                    new ShoppingLocationRequest { Name = "Tesco", City = "Budapest" },
                    new ShoppingLocationRequest { Name = "Aldi", Address = "Main Street 1" },
                    new ShoppingLocationRequest { Name = "Lidl", Website = "https://lidl.hu" }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<ShoppingLocationInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(3, content.Data.Count);

            foreach (var loc in content.Data)
            {
                createdIds.Add(loc.PublicId);
            }

            foreach (var id in createdIds)
            {
                await _client.DeleteAsync($"/api/v1.0/location/shopping/{id}");
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

    #region DeleteMultipleStorageLocations Tests
    [Fact]
    public async Task DeleteMultipleStorageLocations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new DeleteMultipleStorageLocationsRequest { LocationPublicIds = [Guid.NewGuid()] };
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/storage/multiple")
        {
            Content = JsonContent.Create(request)
        };
        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMultipleStorageLocations_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-stor-del-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new DeleteMultipleStorageLocationsRequest { LocationPublicIds = [] };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/storage/multiple")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _client.SendAsync(httpRequest);
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
    public async Task DeleteMultipleStorageLocations_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-stor-del-nf");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new DeleteMultipleStorageLocationsRequest { LocationPublicIds = [Guid.NewGuid()] };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/storage/multiple")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _client.SendAsync(httpRequest);
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
    public async Task DeleteMultipleStorageLocations_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-stor-del-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var createRequest = new CreateMultipleStorageLocationsRequest
            {
                Locations =
                [
                    new StorageLocationRequest { Name = "Storage 1" },
                    new StorageLocationRequest { Name = "Storage 2" }
                ]
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/location/storage/multiple", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<List<StorageLocationInfo>>>();
            var createdIds = createContent?.Data?.Select(x => x.PublicId).ToList() ?? [];

            _output.WriteLine($"Created {createdIds.Count} storage locations");

            var deleteRequest = new DeleteMultipleStorageLocationsRequest { LocationPublicIds = createdIds };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/storage/multiple")
            {
                Content = JsonContent.Create(deleteRequest)
            };
            var response = await _client.SendAsync(httpRequest);
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
    #endregion

    #region DeleteMultipleShoppingLocations Tests
    [Fact]
    public async Task DeleteMultipleShoppingLocations_WithoutToken_ReturnsUnauthorized()
    {
        var request = new DeleteMultipleShoppingLocationsRequest { LocationPublicIds = [Guid.NewGuid()] };
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/shopping/multiple")
        {
            Content = JsonContent.Create(request)
        };
        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMultipleShoppingLocations_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-shop-del-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new DeleteMultipleShoppingLocationsRequest { LocationPublicIds = [] };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/shopping/multiple")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _client.SendAsync(httpRequest);
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
    public async Task DeleteMultipleShoppingLocations_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("loc-shop-del-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var createRequest = new CreateMultipleShoppingLocationsRequest
            {
                Locations =
                [
                    new ShoppingLocationRequest { Name = "Shop 1" },
                    new ShoppingLocationRequest { Name = "Shop 2" }
                ]
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/location/shopping/multiple", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<List<ShoppingLocationInfo>>>();
            var createdIds = createContent?.Data?.Select(x => x.PublicId).ToList() ?? [];

            _output.WriteLine($"Created {createdIds.Count} shopping locations");

            var deleteRequest = new DeleteMultipleShoppingLocationsRequest { LocationPublicIds = createdIds };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/location/shopping/multiple")
            {
                Content = JsonContent.Create(deleteRequest)
            };
            var response = await _client.SendAsync(httpRequest);
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
    #endregion
}
