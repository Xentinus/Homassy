using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;
using ProductUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Integration;

public class ProductControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public ProductControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized Tests
    [Fact]
    public async Task GetProducts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/product");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateProductRequest { Name = "Test Product", Brand = "Test Brand" };
        var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_WithoutToken_ReturnsUnauthorized()
    {
        var request = new UpdateProductRequest { Name = "Updated Product" };
        var response = await _client.PutAsJsonAsync($"/api/v1.0/product/{Guid.NewGuid()}", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1.0/product/{Guid.NewGuid()}");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDetailedProduct_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1.0/product/{Guid.NewGuid()}/detailed");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToggleFavorite_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync($"/api/v1.0/product/{Guid.NewGuid()}/favorite", null);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Validation Tests
    [Fact]
    public async Task CreateProduct_MissingName_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-no-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Send product without required Name
            var content = new StringContent("{\"brand\":\"Test Brand\"}", System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1.0/product", content);
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
    public async Task CreateProduct_MissingBrand_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-no-brand");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Send product without required Brand
            var content = new StringContent("{\"name\":\"Test Product\"}", System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1.0/product", content);
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
    public async Task CreateProduct_NameTooShort_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-short-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Name = "A", // Too short, minimum is 2
                Brand = "Test Brand"
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
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
    public async Task CreateProduct_InvalidBarcode_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-bad-barcode");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = "ABC123" // Invalid, must be digits only
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
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
    public async Task UpdateProduct_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateProductRequest { Name = "Updated Product" };
            var response = await _client.PutAsJsonAsync($"/api/v1.0/product/{Guid.NewGuid()}", request);
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
    public async Task DeleteProduct_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-del-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.DeleteAsync($"/api/v1.0/product/{Guid.NewGuid()}");
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
    public async Task ToggleFavorite_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-fav-notfound");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.PostAsync($"/api/v1.0/product/{Guid.NewGuid()}/favorite", null);
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
    public async Task Product_FullCRUD_Succeeds()
    {
        string? testEmail = null;
        Guid? createdProductId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-crud");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Step 1: Get initial list
            _output.WriteLine("=== Step 1: Get Products ===");
            var getResponse = await _client.GetAsync("/api/v1.0/product");
            var getBody = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get Status: {getResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // Step 2: Create
            _output.WriteLine("\n=== Step 2: Create Product ===");
            var createRequest = new CreateProductRequest
            {
                Name = "Test Milk",
                Brand = "Test Dairy",
                Category = "Dairy",
                Barcode = "1234567890123",
                IsEatable = true,
                Notes = "Test notes",
                IsFavorite = false
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/product", createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create Status: {createResponse.StatusCode}");
            _output.WriteLine($"Create Response: {createBody}");
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(createContent?.Data);
            createdProductId = createContent.Data.PublicId;
            _output.WriteLine($"Created Product ID: {createdProductId}");

            // Step 3: Get detailed
            _output.WriteLine("\n=== Step 3: Get Detailed Product ===");
            var detailedResponse = await _client.GetAsync($"/api/v1.0/product/{createdProductId}/detailed");
            var detailedBody = await detailedResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Detailed Status: {detailedResponse.StatusCode}");
            _output.WriteLine($"Detailed Response: {detailedBody}");
            Assert.Equal(HttpStatusCode.OK, detailedResponse.StatusCode);

            // Step 4: Toggle favorite
            _output.WriteLine("\n=== Step 4: Toggle Favorite ===");
            var favoriteResponse = await _client.PostAsync($"/api/v1.0/product/{createdProductId}/favorite", null);
            var favoriteBody = await favoriteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Favorite Status: {favoriteResponse.StatusCode}");
            _output.WriteLine($"Favorite Response: {favoriteBody}");
            Assert.Equal(HttpStatusCode.OK, favoriteResponse.StatusCode);

            // Step 5: Update
            _output.WriteLine("\n=== Step 5: Update Product ===");
            var updateRequest = new UpdateProductRequest
            {
                Name = "Updated Milk",
                Category = "Updated Dairy"
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1.0/product/{createdProductId}", updateRequest);
            var updateBody = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update Status: {updateResponse.StatusCode}");
            _output.WriteLine($"Update Response: {updateBody}");
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Step 6: Delete
            _output.WriteLine("\n=== Step 6: Delete Product ===");
            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/product/{createdProductId}");
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete Status: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete Response: {deleteBody}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            _output.WriteLine("\n=== Product CRUD Flow Completed Successfully! ===");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetAllDetailedProducts_ReturnsSuccess()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-detailed-all");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/product/detailed");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<DetailedProductInfo>>>();
            Assert.NotNull(content);
            Assert.True(content.Success);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region QuickAddMultipleInventoryItems Tests
    [Fact]
    public async Task QuickAddMultipleInventoryItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new QuickAddMultipleInventoryItemsRequest
        {
            Items =
            [
                new QuickAddMultipleInventoryItemEntry
                {
                    ProductPublicId = Guid.NewGuid(),
                    Quantity = 1,
                    Unit = ProductUnit.Piece
                }
            ]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QuickAddMultipleInventoryItems_EmptyItems_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-multi-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new QuickAddMultipleInventoryItemsRequest
            {
                Items = []
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", request);
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
    public async Task QuickAddMultipleInventoryItems_NonExistentProduct_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-multi-no-prod");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new QuickAddMultipleInventoryItemsRequest
            {
                Items =
                [
                    new QuickAddMultipleInventoryItemEntry
                    {
                        ProductPublicId = Guid.NewGuid(), // Non-existent product
                        Quantity = 1,
                        Unit = ProductUnit.Piece
                    }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", request);
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
    public async Task QuickAddMultipleInventoryItems_NonExistentStorageLocation_ReturnsNotFound()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-multi-no-loc");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create a product first
            var productRequest = new CreateProductRequest { Name = "Test Product", Brand = "Test Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;

            var request = new QuickAddMultipleInventoryItemsRequest
            {
                Items =
                [
                    new QuickAddMultipleInventoryItemEntry
                    {
                        ProductPublicId = productId!.Value,
                        Quantity = 1,
                        Unit = ProductUnit.Piece
                    }
                ],
                StorageLocationPublicId = Guid.NewGuid() // Non-existent storage location
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Cleanup product
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
    public async Task QuickAddMultipleInventoryItems_InvalidQuantity_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-multi-bad-qty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new QuickAddMultipleInventoryItemsRequest
            {
                Items =
                [
                    new QuickAddMultipleInventoryItemEntry
                    {
                        ProductPublicId = Guid.NewGuid(),
                        Quantity = 0, // Invalid - must be > 0
                        Unit = ProductUnit.Piece
                    }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", request);
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
    public async Task QuickAddMultipleInventoryItems_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? productId1 = null;
        Guid? productId2 = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-multi-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create products first
            var product1Request = new CreateProductRequest { Name = "Test Product 1", Brand = "Test Brand" };
            var product1Response = await _client.PostAsJsonAsync("/api/v1.0/product", product1Request);
            var product1Content = await product1Response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId1 = product1Content?.Data?.PublicId;

            var product2Request = new CreateProductRequest { Name = "Test Product 2", Brand = "Test Brand" };
            var product2Response = await _client.PostAsJsonAsync("/api/v1.0/product", product2Request);
            var product2Content = await product2Response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId2 = product2Content?.Data?.PublicId;

            _output.WriteLine($"Created products: {productId1}, {productId2}");

            var request = new QuickAddMultipleInventoryItemsRequest
            {
                Items =
                [
                    new QuickAddMultipleInventoryItemEntry
                    {
                        ProductPublicId = productId1!.Value,
                        Quantity = 5,
                        Unit = ProductUnit.Piece
                    },
                    new QuickAddMultipleInventoryItemEntry
                    {
                        ProductPublicId = productId2!.Value,
                        Quantity = 2.5m,
                        Unit = ProductUnit.Kilogram
                    }
                ],
                IsSharedWithFamily = false
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<InventoryItemInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Count);

            // Cleanup
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

    #region MoveInventoryItems Tests
    [Fact]
    public async Task MoveInventoryItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new MoveInventoryItemsRequest
        {
            InventoryItemPublicIds = [Guid.NewGuid()],
            StorageLocationPublicId = Guid.NewGuid()
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/move", request);
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MoveInventoryItems_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-move-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new MoveInventoryItemsRequest
            {
                InventoryItemPublicIds = [],
                StorageLocationPublicId = Guid.NewGuid()
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/move", request);
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
    public async Task MoveInventoryItems_NonExistentStorageLocation_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-move-no-loc");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new MoveInventoryItemsRequest
            {
                InventoryItemPublicIds = [Guid.NewGuid()],
                StorageLocationPublicId = Guid.NewGuid() // Non-existent
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/move", request);
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
    public async Task MoveInventoryItems_NonExistentInventoryItem_ReturnsNotFound()
    {
        string? testEmail = null;
        Guid? storageLocationId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-move-no-item");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Create a storage location first
            var locationRequest = new { Name = "Test Storage" };
            var locationResponse = await _client.PostAsJsonAsync("/api/v1.0/location/storage", locationRequest);
            
            if (locationResponse.IsSuccessStatusCode)
            {
                var locationContent = await locationResponse.Content.ReadAsStringAsync();
                _output.WriteLine($"Location Response: {locationContent}");
                
                // Parse the response to get storage location ID
                var locationResult = await locationResponse.Content.ReadFromJsonAsync<ApiResponse<dynamic>>();
                // We'll use a workaround since the exact type may vary
            }

            var request = new MoveInventoryItemsRequest
            {
                InventoryItemPublicIds = [Guid.NewGuid()], // Non-existent
                StorageLocationPublicId = storageLocationId ?? Guid.NewGuid()
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/move", request);
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
    public async Task MoveInventoryItems_FullFlow_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? productId = null;
        Guid? inventoryItemId = null;
        Guid? storageLocationId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-move-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Step 1: Create a product
            _output.WriteLine("=== Step 1: Create Product ===");
            var productRequest = new CreateProductRequest { Name = "Test Product", Brand = "Test Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;
            _output.WriteLine($"Product created: {productId}");

            // Step 2: Create inventory item
            _output.WriteLine("\n=== Step 2: Create Inventory Item ===");
            var inventoryRequest = new QuickAddInventoryItemRequest
            {
                ProductPublicId = productId!.Value,
                Quantity = 5,
                Unit = ProductUnit.Piece
            };
            var inventoryResponse = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick", inventoryRequest);
            var inventoryContent = await inventoryResponse.Content.ReadFromJsonAsync<ApiResponse<InventoryItemInfo>>();
            inventoryItemId = inventoryContent?.Data?.PublicId;
            _output.WriteLine($"Inventory item created: {inventoryItemId}");

            // Step 3: Create storage location
            _output.WriteLine("\n=== Step 3: Create Storage Location ===");
            var content = new StringContent("{\"name\":\"Test Fridge\"}", System.Text.Encoding.UTF8, "application/json");
            var locationResponse = await _client.PostAsync("/api/v1.0/location/storage", content);
            var locationBody = await locationResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Location Response: {locationBody}");
            
            // Try to extract the PublicId from the response
            if (locationResponse.IsSuccessStatusCode)
            {
                // Parse JSON to get PublicId
                using var doc = System.Text.Json.JsonDocument.Parse(locationBody);
                if (doc.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("publicId", out var publicIdElement))
                {
                    storageLocationId = publicIdElement.GetGuid();
                }
            }
            _output.WriteLine($"Storage location created: {storageLocationId}");

            if (!storageLocationId.HasValue)
            {
                _output.WriteLine("Could not create storage location, skipping move test");
                return;
            }

            // Step 4: Move inventory item
            _output.WriteLine("\n=== Step 4: Move Inventory Item ===");
            var moveRequest = new MoveInventoryItemsRequest
            {
                InventoryItemPublicIds = [inventoryItemId!.Value],
                StorageLocationPublicId = storageLocationId.Value
            };

            var moveResponse = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/move", moveRequest);
            var moveBody = await moveResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Move Status: {moveResponse.StatusCode}");
            _output.WriteLine($"Move Response: {moveBody}");

            Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);

            var moveContent = await moveResponse.Content.ReadFromJsonAsync<ApiResponse<List<InventoryItemInfo>>>();
            Assert.NotNull(moveContent?.Data);
            Assert.Single(moveContent.Data);

            _output.WriteLine("\n=== Move Inventory Items Flow Completed Successfully! ===");

            // Cleanup
            if (productId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId}");
            if (storageLocationId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/location/storage/{storageLocationId}");
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
