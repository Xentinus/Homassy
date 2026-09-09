using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.Insights;
using Homassy.API.Models.Product;
using Homassy.Tests.Infrastructure;
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
    #endregion

    [Fact]
    public async Task GetInventoryComposition_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/insights/inventory-composition");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

            // No family must short-circuit to an empty result - never an error, and never a query
            // that falls through to an unscoped (i.e. global) count.
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
}
