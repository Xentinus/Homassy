using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.ImageUpload;
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
        var request = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product", Brand = "Test Brand" };
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
                Unit = ProductUnit.Piece,
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
                Unit = ProductUnit.Piece,
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

    [Fact]
    public async Task CreateProduct_WithSingleCharacterName_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-short-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Name has a minimum length of 2; the web forms must not let a single character through.
            var content = new StringContent("{\"name\":\"A\",\"brand\":\"Test Brand\",\"unit\":0}", System.Text.Encoding.UTF8, "application/json");
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
    public async Task CreateProduct_WithCategoryAsString_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-cat-string");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // ProductCategory is a plain enum and no string enum converter is registered, so a
            // stringified category is not deserializable. The web pickers therefore have to emit
            // the enum's numbers - a form that sent "11" instead of 11 got this 400.
            var content = new StringContent(
                "{\"name\":\"Category As String\",\"brand\":\"Test Brand\",\"unit\":0,\"category\":\"11\"}",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await _client.PostAsync("/api/v1.0/product", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("category", responseBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task CreateProduct_WithCategoryOther_KeepsTheZeroValue()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-cat-other");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // ProductCategory.Other is 0, a legitimate category. A client that drops it with a
            // truthiness check (`category || null`) silently files the product under no category.
            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Category Other",
                Brand = "Test Brand",
                Category = ProductCategory.Other
            };
            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(ProductCategory.Other, content.Data.Category);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Model-validation failures never reach GlobalExceptionMiddleware - MVC answers them itself
    /// with a ValidationProblemDetails body that carries no errorCodes. The web client normalizes
    /// the keys of that body onto form fields, so the exact spellings the API produces are part of
    /// the contract: a DataAnnotations failure is keyed by the PascalCase CLR property name, and a
    /// JSON deserialization failure by a "$."-prefixed JSON path.
    /// </summary>
    [Fact]
    public async Task CreateProduct_InvalidRequest_ReturnsValidationProblemDetailsKeyedByField()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-vpd-shape");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // A DataAnnotations failure: too-short Name, too-long Notes.
            var annotationJson = $"{{\"name\":\"A\",\"brand\":\"Test Brand\",\"unit\":0,\"notes\":\"{new string('x', 200)}\"}}";
            var annotationResponse = await _client.PostAsync("/api/v1.0/product",
                new StringContent(annotationJson, System.Text.Encoding.UTF8, "application/json"));
            var annotationBody = await annotationResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"[DataAnnotations] Status: {annotationResponse.StatusCode}");
            _output.WriteLine($"[DataAnnotations] Response: {annotationBody}");

            Assert.Equal(HttpStatusCode.BadRequest, annotationResponse.StatusCode);

            using var annotationJsonDoc = JsonDocument.Parse(annotationBody);
            var annotationRoot = annotationJsonDoc.RootElement;

            // No errorCodes array: this is why the web client needs a second shape at all.
            Assert.False(annotationRoot.TryGetProperty("errorCodes", out _));
            var annotationErrors = annotationRoot.GetProperty("errors");

            // PascalCase CLR property names, not the camelCase wire names.
            Assert.True(annotationErrors.TryGetProperty("Name", out _));
            Assert.True(annotationErrors.TryGetProperty("Notes", out _));

            // A deserialization failure: category is an enum, so a string cannot bind.
            var jsonPathJson = "{\"name\":\"Bad Category\",\"brand\":\"Test Brand\",\"unit\":0,\"category\":\"11\"}";
            var jsonPathResponse = await _client.PostAsync("/api/v1.0/product",
                new StringContent(jsonPathJson, System.Text.Encoding.UTF8, "application/json"));
            var jsonPathBody = await jsonPathResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"[JSON path] Status: {jsonPathResponse.StatusCode}");
            _output.WriteLine($"[JSON path] Response: {jsonPathBody}");

            Assert.Equal(HttpStatusCode.BadRequest, jsonPathResponse.StatusCode);

            using var jsonPathDoc = JsonDocument.Parse(jsonPathBody);
            var jsonPathErrors = jsonPathDoc.RootElement.GetProperty("errors");

            // A JSON path, and a "request" key that is not a form field at all.
            Assert.True(jsonPathErrors.TryGetProperty("$.category", out _));
            Assert.True(jsonPathErrors.TryGetProperty("request", out _));
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
    public async Task DeleteProduct_Authenticated_ReturnsForbidden()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-del-forbidden");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.DeleteAsync($"/api/v1.0/product/{Guid.NewGuid()}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Products are global: deletion is rejected outright, never attempted
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains(ErrorCodes.ProductDeletionNotAllowed, responseBody);
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

    #region Product Image Tests
    [Fact]
    public async Task GetProductImage_WithoutToken_ReturnsUnauthorized()
    {
        // The endpoint answers with image bytes rather than an ApiResponse envelope, so it is
        // worth pinning that it is still behind [Authorize] like the rest of the controller.
        var response = await _client.GetAsync($"/api/v1.0/product/{Guid.NewGuid()}/image");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProductImage_FullFlow_ServesBytesAndCaches()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-image");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/product", new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Image Product",
                Brand = "Image Brand",
                Category = ProductCategory.Milk,
                IsEatable = true
            });
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(created?.Data);
            var productId = created.Data.PublicId;

            // A product with no picture has no URL and its image endpoint 404s.
            Assert.Null(created.Data.ProductImageUrl);
            var missingResponse = await _client.GetAsync($"/api/v1.0/product/{productId}/image");
            Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

            // 50x50 - the upload path's minimum dimensions.
            var validBase64 = TestImages.PngBase64();

            var uploadResponse = await _client.PostAsJsonAsync(
                $"/api/v1.0/product/{productId}/image",
                new UploadProductImageRequest { ProductPublicId = productId, ImageBase64 = validBase64 });
            var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Upload Status: {uploadResponse.StatusCode}");
            _output.WriteLine($"Upload Response: {uploadBody}");
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<ProductImageInfo>>();
            Assert.NotNull(uploaded?.Data);
            var thumbUrl = uploaded.Data.ProductImageUrl;
            var fullUrl = uploaded.Data.ProductImageFullUrl;

            // Both renditions are the same picture, so they carry the same version.
            Assert.Contains("size=thumb", thumbUrl);
            Assert.Contains("size=full", fullUrl);
            Assert.Contains("v=", thumbUrl);

            foreach (var url in new[] { thumbUrl, fullUrl })
            {
                var imageResponse = await _client.GetAsync(url);

                Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
                Assert.StartsWith("image/", imageResponse.Content.Headers.ContentType?.MediaType);
                Assert.NotEmpty(await imageResponse.Content.ReadAsByteArrayAsync());

                var etag = imageResponse.Headers.ETag;
                Assert.NotNull(etag);

                var conditional = new HttpRequestMessage(HttpMethod.Get, url);
                conditional.Headers.IfNoneMatch.Add(etag);
                var notModified = await _client.SendAsync(conditional);
                Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
            }

            // The thumbnail and the full image are different bytes, which is the point of asking
            // for one or the other rather than always shipping the upload.
            var thumbEtag = (await _client.GetAsync(thumbUrl)).Headers.ETag?.Tag;
            var fullEtag = (await _client.GetAsync(fullUrl)).Headers.ETag?.Tag;
            Assert.NotEqual(thumbEtag, fullEtag);

            // The product payload now carries the URL instead of the image. Polled rather than
            // asserted outright: the product row is served from the Functions layer's cache, which
            // picks the new version up on its next trigger-driven refresh, not synchronously.
            string? payloadUrl = null;
            for (var attempt = 0; attempt < 40 && payloadUrl == null; attempt++)
            {
                var detailedResponse = await _client.GetAsync($"/api/v1.0/product/{productId}/detailed");
                var detailed = await detailedResponse.Content.ReadFromJsonAsync<ApiResponse<DetailedProductInfo>>();
                payloadUrl = detailed?.Data?.ProductImageUrl;

                if (payloadUrl == null)
                {
                    await Task.Delay(250);
                }
            }

            Assert.Equal(thumbUrl, payloadUrl);

            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/product/{productId}/image");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var goneResponse = await _client.GetAsync(thumbUrl);
            Assert.Equal(HttpStatusCode.NotFound, goneResponse.StatusCode);
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

            // Step 1: Get initial list (now paginated)
            _output.WriteLine("=== Step 1: Get Products ===");
            var getResponse = await _client.GetAsync("/api/v1.0/product");
            var getBody = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get Status: {getResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // Verify paginated response structure
            var pagedContent = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductInfo>>>();
            Assert.NotNull(pagedContent?.Data);
            Assert.NotNull(pagedContent.Data.Items);

            // Step 2: Create
            _output.WriteLine("\n=== Step 2: Create Product ===");
            var createRequest = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Milk",
                Brand = "Test Dairy",
                Category = ProductCategory.Milk,
                Barcode = "1234567890128", // Valid EAN-13 barcode with correct checksum
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
                Category = ProductCategory.Milk
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1.0/product/{createdProductId}", updateRequest);
            var updateBody = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update Status: {updateResponse.StatusCode}");
            _output.WriteLine($"Update Response: {updateBody}");
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Step 6: Delete is always rejected — a Product row is global (only ProductCustomization
            // is family-scoped), so deleting one would remove it, and its inventory, for every family.
            _output.WriteLine("\n=== Step 6: Delete Product (rejected — products are global) ===");
            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/product/{createdProductId}");
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete Status: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete Response: {deleteBody}");
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
            Assert.Contains("PRODUCT-0004", deleteBody);

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
    public async Task GetAllDetailedProducts_ReturnsPagedSuccess()
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

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<DetailedProductInfo>>>();
            Assert.NotNull(content);
            Assert.True(content.Success);
            Assert.NotNull(content.Data);
            Assert.NotNull(content.Data.Items);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    // Test pagination parameters
    [Fact]
    public async Task GetProducts_WithPaginationParams_ReturnsCorrectPage()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-page-test");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/product?pageNumber=1&pageSize=10");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(1, content.Data.PageNumber);
            Assert.Equal(10, content.Data.PageSize);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetProducts_WithReturnAll_ReturnsUnpaginatedList()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-all-test");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/product?returnAll=true");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.True(content.Data.IsUnpaginated);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Purchase Price Precision Tests
    /// <summary>
    /// Task 11 (#128): <c>ProductPurchaseInfo.Price</c> moves from <c>int?</c> to <c>decimal?</c> so
    /// a EUR/USD price with cents survives the round trip - matching the live bug in
    /// <c>AddInventoryItemModal.vue:442</c>, whose <c>&lt;UInput type="number" step="0.01"&gt;</c>
    /// already lets a user type "12.99". The request body is raw JSON, not the typed
    /// <see cref="CreateInventoryItemRequest"/>, precisely so this test can express "12.99" even
    /// while <c>Price</c> is still an <c>int?</c> the compiler would refuse to assign a decimal
    /// literal to - the same reason the other deserialization-failure tests in this class
    /// (e.g. <see cref="CreateProduct_WithCategoryAsString_ReturnsBadRequest"/>) use a raw string
    /// body instead of the strongly-typed request. Before the fix this either 400s (JSON
    /// deserialization of "12.99" into <c>int?</c> fails outright) or, if it were silently coerced,
    /// would come back as 12 or 13 - never 12.99.
    /// <para>
    /// Fix round 1: the original version of this test asserted only on the create response, which
    /// is built from the same in-memory entity the handler just assigned <c>Price = 12.99m</c> to -
    /// it would pass even if the column silently mangled the stored value. It now also re-reads
    /// through a second, separate request and, authoritatively, straight from Postgres through a
    /// fresh <see cref="Homassy.Tests.Infrastructure.HomassyWebApplicationFactory.CreateScopedDbContext"/>.
    /// See the inline comments at each assertion for why the second request is corroborating and the
    /// direct DB read is the one that actually proves the storage-type change.
    /// </para>
    /// </summary>
    [Fact]
    public async Task CreateInventoryItem_PriceWithCents_RoundTripsExactlyThroughTheApi()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-price-cents");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var productRequest = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Price Precision Product", Brand = "Test Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;
            Assert.NotNull(productId);

            var currencyEur = (int)Currency.Eur;
            var json = $"{{\"productPublicId\":\"{productId}\",\"quantity\":1,\"price\":12.99,\"currency\":{currencyEur}}}";
            var response = await _client.PostAsync("/api/v1.0/product/inventory",
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryItemInfo>>();
            Assert.NotNull(content?.Data?.PurchaseInfo);
            Assert.NotNull(content!.Data!.PurchaseInfo!.Price);
            // Explicit decimal cast: keeps this line compiling both before Task 11 (Price is still
            // int?, so a plain `12.99m` vs. int? comparison is ambiguous across Assert.Equal's
            // overloads) and after (Price is decimal?, where the cast is a harmless no-op).
            //
            // This alone only proves the request was *accepted*: `content` is built by
            // CreateInventoryItemAsync from the same tracked entity it just assigned `Price = 12.99m`
            // to, in memory, moments after SaveChangesAsync. It would still read 12.99 here even if
            // numeric(18,4) silently mangled the stored value, so it cannot be the test's only
            // assertion - see the direct-to-Postgres read below, which is.
            Assert.Equal(12.99m, (decimal)content.Data.PurchaseInfo!.Price!.Value);

            // Second, separate request: re-read the item through GET .../detailed rather than the
            // create response. Per Homassy.API/CLAUDE.md ("Cache-First Architecture"),
            // GetPurchaseInfoByInventoryItemId/GetInventoryItemsByProductId serve straight from
            // ProductFunctions' static in-memory caches once populated, with no DB fallback of their
            // own - and CreateInventoryItemAsync never calls RefreshPurchaseInfoCacheAsync. The only
            // way this item's price reaches that cache at all is the table's AFTER INSERT trigger
            // (pg_notify("cache_changes")) waking CacheManagementService's LISTEN handler, which
            // re-SELECTs the row from Postgres through a brand-new HomassyDbContext. So this is a
            // genuinely separate round trip when it lands - but it is racing that background
            // notification, so it is corroborating, not authoritative: it is skipped rather than
            // failed if the cache had not caught up yet by the time this ran.
            var detailedResponse = await _client.GetAsync($"/api/v1.0/product/{productId}/detailed");
            var detailedBody = await detailedResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Detailed (2nd request) Status: {detailedResponse.StatusCode}");
            _output.WriteLine($"Detailed (2nd request) Response: {detailedBody}");
            Assert.Equal(HttpStatusCode.OK, detailedResponse.StatusCode);

            var detailedContent = await detailedResponse.Content.ReadFromJsonAsync<ApiResponse<DetailedProductInfo>>();
            var reReadItem = detailedContent?.Data?.InventoryItems.FirstOrDefault(i => i.PublicId == content.Data.PublicId);
            if (reReadItem?.PurchaseInfo?.Price is { } reReadPrice)
            {
                Assert.Equal(12.99m, reReadPrice);
            }
            else
            {
                _output.WriteLine("Detailed (2nd request) had not picked up the new item yet " +
                    "(cache-refresh race, not a precision failure) - relying on the direct DB read instead.");
            }

            // Authoritative assertion: read the row Postgres actually stored, through a brand-new
            // DbContext/connection that bypasses ProductFunctions' in-memory caches entirely. This is
            // the one assertion in this test that numeric(18,4) truncating or rounding the value would
            // actually fail.
            var (dbScope, dbContext) = _factory.CreateScopedDbContext();
            await using var _dbScope = dbScope as IAsyncDisposable;
            var storedPurchaseInfo = dbContext.ProductPurchaseInfos.FirstOrDefault(p => p.PublicId == content.Data.PurchaseInfo!.PublicId);
            Assert.NotNull(storedPurchaseInfo);
            Assert.Equal(12.99m, storedPurchaseInfo!.Price);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (productId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId}");
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Split Inventory Item Price Tests
    /// <summary>
    /// Task 11 fix round 1 (#128): <c>SplitInventoryItemAsync</c>'s prorate step
    /// (<c>ProductFunctions.cs</c>, "Prorate price if exists") used to be
    /// <c>newPurchaseInfo.Price = (int)(purchaseInfo.Price.Value * ratio);</c>, truncating the
    /// fraction off the new item's price on every split that didn't divide evenly. It now assigns
    /// the <c>decimal</c> directly, but nothing covered that fix - this pins both halves of it:
    /// that the fraction survives at all, and what the <c>numeric(18,4)</c> column actually does
    /// with digits past its fourth decimal place.
    /// <para>
    /// 32 units for EUR 1.00, split off 1: ratio = 1/32 = 0.03125 exactly, so the prorated price
    /// is EUR 0.03125 - a fifth decimal digit of exactly "5", the one case that actually tells
    /// rounding and truncation apart (both agree on, say, .03123). The old <c>(int)</c> cast
    /// collapses this to a flat 0. The fixed code keeps 0.03125 in memory; Postgres's
    /// <c>numeric(18,4)</c> column rounds it on storage rather than truncating it, and does so
    /// half-away-from-zero rather than banker's-rounding: the stored value is 0.0313, not the
    /// 0.0312 either of those other two rules would produce. Confirmed independently with
    /// <c>SELECT (2.00005)::numeric(18,4)</c> against the same Postgres 16 -> 2.0001, not 2.0000.
    /// </para>
    /// <para>
    /// Asserted directly against Postgres, not the split response's own
    /// <c>NewItem.PurchaseInfo</c>: <c>SplitInventoryItemAsync</c> builds it via
    /// <c>GetPurchaseInfoByInventoryItemId</c>, which - like the read path exercised in
    /// <see cref="CreateInventoryItem_PriceWithCents_RoundTripsExactlyThroughTheApi"/> - serves
    /// straight from <c>ProductFunctions</c>' static <c>_purchaseInfoCache</c> once populated, with
    /// no DB fallback of its own. Unlike the two <c>RefreshInventoryItemCacheAsync</c> calls the
    /// method makes for the two inventory items themselves, it never calls
    /// <c>RefreshPurchaseInfoCacheAsync</c> for the new item's freshly-inserted purchase-info row,
    /// so that cache entry does not exist yet when the response is built: <c>NewItem.PurchaseInfo</c>
    /// comes back null on every run, not just occasionally. That is a real, separate bug - reported,
    /// not fixed, here.
    /// </para>
    /// </summary>
    [Fact]
    public async Task SplitInventoryItem_PriceDoesNotDivideEvenly_PreservesFractionAndPinsPostgresRounding()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-split-price");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var productRequest = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Split Price Product", Brand = "Test Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;
            Assert.NotNull(productId);

            // 32 units for a flat EUR 1.00: splitting off 1 unit gives ratio = 1/32 = 0.03125
            // exactly, so the prorated price lands on a clean, non-repeating fifth decimal digit
            // of "5" - the exact case that distinguishes truncation, round-half-up and
            // round-half-to-even from one another (see the class doc comment above).
            var createRequest = new CreateInventoryItemRequest
            {
                ProductPublicId = productId!.Value,
                Quantity = 32m,
                Price = 1.00m,
                Currency = Currency.Eur
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/product/inventory", createRequest);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<InventoryItemInfo>>();
            var originalItemPublicId = createContent!.Data!.PublicId;

            var splitRequest = new SplitInventoryItemRequest { Quantity = 1m };
            var splitResponse = await _client.PostAsJsonAsync(
                $"/api/v1.0/product/inventory/{originalItemPublicId}/split", splitRequest);
            var splitBody = await splitResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {splitResponse.StatusCode}");
            _output.WriteLine($"Response: {splitBody}");
            Assert.Equal(HttpStatusCode.OK, splitResponse.StatusCode);

            var splitContent = await splitResponse.Content.ReadFromJsonAsync<ApiResponse<SplitInventoryItemResponse>>();
            Assert.NotNull(splitContent?.Data);
            var newItemPublicId = splitContent!.Data!.NewItem.PublicId;

            // Authoritative check: read what Postgres actually persisted for the new item's
            // prorated price, through a brand-new DbContext/connection - see the class doc comment
            // for why the split response's own NewItem.PurchaseInfo cannot be used for this.
            var (scope, dbContext) = _factory.CreateScopedDbContext();
            await using var _dbScope = scope as IAsyncDisposable;
            var newItemInternalId = dbContext.ProductInventoryItems.First(i => i.PublicId == newItemPublicId).Id;
            var newPurchaseInfo = dbContext.ProductPurchaseInfos.FirstOrDefault(p => p.ProductInventoryItemId == newItemInternalId);

            Assert.NotNull(newPurchaseInfo);
            Assert.NotNull(newPurchaseInfo!.Price);
            // Preserves the fraction at all - the bug this test exists for. The old `(int)` cast
            // would have stored exactly 0.
            Assert.NotEqual(0m, newPurchaseInfo.Price!.Value);
            // Pins Postgres's actual numeric(18,4) rounding of the exact-half fifth digit: rounds
            // half away from zero, up to 0.0313 - neither truncated (0.0312) nor banker's-rounded
            // to the even neighbour (also 0.0312, since 2 is even).
            Assert.Equal(0.0313m, newPurchaseInfo.Price!.Value);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (productId.HasValue)
                await _client.DeleteAsync($"/api/v1.0/product/{productId}");
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
            var productRequest = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product", Brand = "Test Brand" };
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
            var product1Request = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product 1", Brand = "Test Brand" };
            var product1Response = await _client.PostAsJsonAsync("/api/v1.0/product", product1Request);
            var product1Content = await product1Response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId1 = product1Content?.Data?.PublicId;

            var product2Request = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product 2", Brand = "Test Brand" };
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
                    },
                    new QuickAddMultipleInventoryItemEntry
                    {
                        ProductPublicId = productId2!.Value,
                        Quantity = 2.5m,
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
            var productRequest = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product", Brand = "Test Brand" };
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

    #region DeleteMultipleInventoryItems Tests
    [Fact]
    public async Task DeleteMultipleInventoryItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new DeleteMultipleInventoryItemsRequest { ItemPublicIds = [Guid.NewGuid()] };
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/product/inventory/multiple")
        {
            Content = JsonContent.Create(request)
        };
        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMultipleInventoryItems_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-del-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new DeleteMultipleInventoryItemsRequest { ItemPublicIds = [] };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/product/inventory/multiple")
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
    public async Task DeleteMultipleInventoryItems_NonExistent_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-del-nf");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new DeleteMultipleInventoryItemsRequest { ItemPublicIds = [Guid.NewGuid()] };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/product/inventory/multiple")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _client.SendAsync(httpRequest);

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
    public async Task DeleteMultipleInventoryItems_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-del-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var productRequest = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product", Brand = "Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;

            var addRequest = new QuickAddMultipleInventoryItemsRequest
            {
                Items =
                [
                    new QuickAddMultipleInventoryItemEntry { ProductPublicId = productId!.Value, Quantity = 1 },
                    new QuickAddMultipleInventoryItemEntry { ProductPublicId = productId!.Value, Quantity = 2 }
                ]
            };
            var addResponse = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", addRequest);
            var addContent = await addResponse.Content.ReadFromJsonAsync<ApiResponse<List<InventoryItemInfo>>>();
            var createdIds = addContent?.Data?.Select(x => x.PublicId).ToList() ?? [];

            var deleteRequest = new DeleteMultipleInventoryItemsRequest { ItemPublicIds = createdIds };
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1.0/product/inventory/multiple")
            {
                Content = JsonContent.Create(deleteRequest)
            };
            var response = await _client.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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

    #region ConsumeMultipleInventoryItems Tests
    [Fact]
    public async Task ConsumeMultipleInventoryItems_WithoutToken_ReturnsUnauthorized()
    {
        var request = new ConsumeMultipleInventoryItemsRequest
        {
            Items = [new ConsumeInventoryItemEntry { InventoryItemPublicId = Guid.NewGuid(), Quantity = 1 }]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/consume/multiple", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConsumeMultipleInventoryItems_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-cons-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new ConsumeMultipleInventoryItemsRequest { Items = [] };
            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/consume/multiple", request);

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
    public async Task ConsumeMultipleInventoryItems_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("inv-cons-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var productRequest = new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test Product", Brand = "Brand" };
            var productResponse = await _client.PostAsJsonAsync("/api/v1.0/product", productRequest);
            var productContent = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = productContent?.Data?.PublicId;

            var addRequest = new QuickAddMultipleInventoryItemsRequest
            {
                Items =
                [
                    new QuickAddMultipleInventoryItemEntry { ProductPublicId = productId!.Value, Quantity = 10 },
                    new QuickAddMultipleInventoryItemEntry { ProductPublicId = productId!.Value, Quantity = 5 }
                ]
            };
            var addResponse = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/quick/multiple", addRequest);
            var addContent = await addResponse.Content.ReadFromJsonAsync<ApiResponse<List<InventoryItemInfo>>>();
            var createdItems = addContent?.Data ?? [];

            var consumeRequest = new ConsumeMultipleInventoryItemsRequest
            {
                Items =
                [
                    new ConsumeInventoryItemEntry { InventoryItemPublicId = createdItems[0].PublicId, Quantity = 3 },
                    new ConsumeInventoryItemEntry { InventoryItemPublicId = createdItems[1].PublicId, Quantity = 2 }
                ]
            };
            var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory/consume/multiple", consumeRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<InventoryItemInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Count);
            Assert.Equal(7, content.Data[0].CurrentQuantity);
            Assert.Equal(3, content.Data[1].CurrentQuantity);

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

    #region CreateMultipleProducts Tests
    [Fact]
    public async Task CreateMultipleProducts_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreateMultipleProductsRequest
        {
            Products = [new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Test", Brand = "Brand" }]
        };
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/multiple", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMultipleProducts_EmptyList_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-multi-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleProductsRequest { Products = [] };
            var response = await _client.PostAsJsonAsync("/api/v1.0/product/multiple", request);

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
    public async Task CreateMultipleProducts_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        var createdIds = new List<Guid>();
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("prod-multi-ok");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateMultipleProductsRequest
            {
                Products =
                [
                    new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Product 1", Brand = "Brand A", Category = ProductCategory.Grain },
                    new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Product 2", Brand = "Brand B", IsEatable = false },
                    new CreateProductRequest { Unit = ProductUnit.Piece, Name = "Product 3", Brand = "Brand C", IsFavorite = true }
                ]
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product/multiple", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(3, content.Data.Count);
            Assert.True(content.Data[2].IsFavorite);

            foreach (var product in content.Data)
            {
                createdIds.Add(product.PublicId);
            }

            foreach (var id in createdIds)
            {
                await _client.DeleteAsync($"/api/v1.0/product/{id}");
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

    #region Barcode Validation Tests
    [Theory]
    [InlineData("4006381333931")]
    [InlineData("5901234123457")]
    [InlineData("9780201379624")]
    public async Task CreateProduct_WithValidEan13Barcode_ReturnsSuccess(string barcode)
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"barcode-ean13-{barcode[..6]}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = barcode
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(content?.Data);
            productId = content.Data.PublicId;

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

    [Theory]
    [InlineData("96385074")]
    [InlineData("55123457")]
    [InlineData("12345670")]
    public async Task CreateProduct_WithValidEan8Barcode_ReturnsSuccess(string barcode)
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"barcode-ean8-{barcode}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = barcode
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(content?.Data);
            productId = content.Data.PublicId;

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

    [Theory]
    [InlineData("012345678905")]
    [InlineData("042100005264")]
    [InlineData("614141000036")]
    public async Task CreateProduct_WithValidUpcABarcode_ReturnsSuccess(string barcode)
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"barcode-upca-{barcode[..6]}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = barcode
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(content?.Data);
            productId = content.Data.PublicId;

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

    [Theory]
    [InlineData("4006381333930")]
    [InlineData("5901234123456")]
    [InlineData("1234567890123")]
    public async Task CreateProduct_WithInvalidEan13Checksum_ReturnsBadRequest(string barcode)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"barcode-bad-ean13-{barcode[..6]}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = barcode
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("checksum", responseBody.ToLowerInvariant());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Theory]
    [InlineData("ABC123456789")]
    [InlineData("123-456-7890")]
    [InlineData("12345ABC")]
    public async Task CreateProduct_WithInvalidBarcodeCharacters_ReturnsBadRequest(string barcode)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"barcode-chars-{Guid.NewGuid().ToString()[..8]}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = barcode
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

    [Theory]
    [InlineData("12345")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    public async Task CreateProduct_WithInvalidBarcodeLength_ReturnsBadRequest(string barcode)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync($"barcode-len-{barcode.Length}");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand",
                Barcode = barcode
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
    public async Task UpdateProduct_WithValidBarcode_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("barcode-update-valid");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var createRequest = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand"
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/product", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = createContent?.Data?.PublicId;

            var updateRequest = new UpdateProductRequest
            {
                Barcode = "4006381333931"
            };

            var response = await _client.PutAsJsonAsync($"/api/v1.0/product/{productId}", updateRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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
    public async Task UpdateProduct_WithInvalidBarcodeChecksum_ReturnsBadRequest()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("barcode-update-invalid");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var createRequest = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Test Product",
                Brand = "Test Brand"
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/product", createRequest);
            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            productId = createContent?.Data?.PublicId;

            var updateRequest = new UpdateProductRequest
            {
                Barcode = "4006381333930"
            };

            var response = await _client.PutAsJsonAsync($"/api/v1.0/product/{productId}", updateRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("checksum", responseBody.ToLowerInvariant());

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
    public async Task CreateProduct_WithRealWorldCocaColaBarcode_ReturnsSuccess()
    {
        string? testEmail = null;
        Guid? productId = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("barcode-cocacola");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateProductRequest
            {
                Unit = ProductUnit.Piece,
                Name = "Coca-Cola",
                Brand = "Coca-Cola",
                Barcode = "5449000000996"
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/product", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
            Assert.NotNull(content?.Data);
            productId = content.Data.PublicId;

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
}
