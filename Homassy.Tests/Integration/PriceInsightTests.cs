using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.Insights;
using Homassy.API.Models.Location;
using Homassy.API.Models.Product;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using ProductUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Integration;

/// <summary>
/// Integration tests for <c>GET api/v1.0/product/{productPublicId}/price-history</c> - the #128
/// endpoint that turns a household's raw purchase rows into a comparable price per unit.
/// </summary>
/// <remarks>
/// The single test this whole file exists for is
/// <see cref="GetPriceHistory_LargerPackAtHigherPrice_IsCorrectlyTheCheaperUnitPrice"/>: a 2 l
/// bottle costing more in total than a 1 l bottle is the cheaper buy, and no amount of grouping or
/// averaging matters if that one comparison comes out backwards. Everything else here pins the
/// rules that keep such a comparison honest - currencies never mixed, bases never blended, another
/// family's purchases never counted.
/// </remarks>
public class PriceInsightTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public PriceInsightTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
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
    /// Creates a product with an explicit <paramref name="unit"/> and returns its public id. The
    /// unit matters here in a way it does not for the other insight endpoints:
    /// <c>ProductFunctions.CreateInventoryItemAsync</c> copies it onto every inventory item created
    /// for the product, and that item's unit is the only unit a purchase row has - which is what
    /// <c>UnitNormalization</c> then normalizes by. A test that wants a per-litre comparison has to
    /// ask for a Liter product here; it cannot set the unit on the purchase itself.
    /// </summary>
    private async Task<Guid> CreateProductAsync(ProductCategory category, ProductUnit unit, string namePrefix)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/product", new CreateProductRequest
        {
            Unit = unit,
            Name = $"{namePrefix} {category}",
            Brand = "Price Insight Brand",
            Category = category
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Create product failed: {response.StatusCode} {body}");

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    /// <summary>
    /// Creates a shopping location for whichever user is currently authenticated on
    /// <see cref="_client"/> and returns its public id.
    /// </summary>
    private async Task<Guid> CreateShoppingLocationAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/location/shopping", new ShoppingLocationRequest
        {
            Name = name
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Create shopping location failed: {response.StatusCode} {body}");

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingLocationInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    /// <summary>
    /// Creates an inventory item together with its purchase row, and returns the item's public id
    /// so a caller can go on to set the purchase timestamp with <see cref="SetPurchasedAtAsync"/>.
    /// </summary>
    /// <remarks>
    /// <c>ProductFunctions.CreateInventoryItemAsync</c> only writes a <c>ProductPurchaseInfo</c>
    /// row at all when the request carries a price, a shopping location or a receipt number - so a
    /// test that wants an <b>unpriced</b> purchase row (there is one below) must still pass a
    /// shopping location, or it silently creates no purchase row to aggregate.
    /// </remarks>
    private async Task<Guid> CreatePurchaseAsync(
        Guid productPublicId,
        decimal quantity,
        decimal? price,
        Currency? currency = null,
        Guid? shoppingLocationPublicId = null,
        bool isSharedWithFamily = false)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/product/inventory", new CreateInventoryItemRequest
        {
            ProductPublicId = productPublicId,
            IsSharedWithFamily = isSharedWithFamily,
            Quantity = quantity,
            Price = price,
            Currency = currency,
            ShoppingLocationPublicId = shoppingLocationPublicId
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Create purchase failed: {response.StatusCode} {body}");

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryItemInfo>>();
        Assert.NotNull(content?.Data);
        return content.Data.PublicId;
    }

    /// <summary>
    /// Backdates a purchase to an exact UTC instant, directly in the database.
    /// <c>ProductPurchaseInfo.PurchasedAt</c> defaults to <see cref="DateTime.UtcNow"/> and no
    /// request model carries it, so without this every purchase a test creates lands within
    /// milliseconds of every other one - which leaves "the latest purchase" (and therefore
    /// <see cref="ShopPriceSeries.Latest"/> and <see cref="PriceHistoryResponse.LatestAboveAverageRatio"/>)
    /// decided by insertion order rather than by anything the test states.
    /// </summary>
    private async Task SetPurchasedAtAsync(Guid inventoryItemPublicId, DateTime purchasedAtUtc)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var itemId = await context.ProductInventoryItems
            .Where(i => i.PublicId == inventoryItemPublicId)
            .Select(i => i.Id)
            .FirstAsync();

        var purchase = await context.ProductPurchaseInfos.FirstAsync(p => p.ProductInventoryItemId == itemId);
        purchase.PurchasedAt = DateTime.SpecifyKind(purchasedAtUtc, DateTimeKind.Utc);
        await context.SaveChangesAsync();
    }

    private async Task<PriceHistoryResponse> GetPriceHistoryAsync(Guid productPublicId, int days = 90)
    {
        var response = await _client.GetAsync($"/api/v1.0/product/{productPublicId}/price-history?days={days}");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<PriceHistoryResponse>>();
        Assert.NotNull(content?.Data);
        return content.Data;
    }

    /// <summary>
    /// The single basis group of a single-currency response, asserted to actually be single on both
    /// levels - a leak or a blend shows up as an extra entry, which a "first matching" lookup would
    /// quietly pass over.
    /// </summary>
    private static PriceBasisGroup SingleGroup(PriceHistoryResponse history, Currency currency)
    {
        var currencyEntry = Assert.Single(history.ByCurrency);
        Assert.Equal(currency, currencyEntry.Key);
        return Assert.Single(currencyEntry.Value);
    }
    #endregion

    [Fact]
    public async Task GetPriceHistory_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1.0/product/{Guid.NewGuid()}/price-history?days=90");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The window is a closed set (90/180/365), not an arbitrary caller-supplied number - an
    /// unbounded window is an unbounded query. Rejected before anything reaches the database.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(-90)]
    [InlineData(3650)]
    public async Task GetPriceHistory_UnsupportedWindowLength_ReturnsBadRequest(int days)
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-bad-window");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync($"/api/v1.0/product/{Guid.NewGuid()}/price-history?days={days}");
            _output.WriteLine($"days={days} -> {response.StatusCode}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The five per-shop statistics, over three purchases split across two shops. Each shop's
    /// numbers are computed over that shop's own points only - never pooled - and
    /// <see cref="ShopPriceSeries.Latest"/> is the newest purchase by timestamp rather than the
    /// last row the database happened to return, which is why the purchases are backdated to
    /// distinct instants.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_ThreePurchasesAtTwoShops_ReturnsTwoSeriesWithCorrectStatistics()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-two-shops");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shopA = await CreateShoppingLocationAsync("Price Shop A");
            var shopB = await CreateShoppingLocationAsync("Price Shop B");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "two-shops");

            // Shop A: 1 l for 200 (=> 200/l) ten days ago, then 2 l for 300 (=> 150/l) five days
            // ago - so A's Latest is the cheaper, more recent one, which is the only way to tell
            // "newest by timestamp" apart from "lowest" or "last inserted".
            var older = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 200, shoppingLocationPublicId: shopA);
            await SetPurchasedAtAsync(older, DateTime.UtcNow.AddDays(-10));
            var newer = await CreatePurchaseAsync(productPublicId, quantity: 2, price: 300, shoppingLocationPublicId: shopA);
            await SetPurchasedAtAsync(newer, DateTime.UtcNow.AddDays(-5));

            // Shop B: one purchase, 1 l for 250 (=> 250/l).
            var atB = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 250, shoppingLocationPublicId: shopB);
            await SetPurchasedAtAsync(atB, DateTime.UtcNow.AddDays(-7));

            var history = await GetPriceHistoryAsync(productPublicId);

            var group = SingleGroup(history, Currency.Huf);
            Assert.Equal("per-l", group.SeriesKey);
            Assert.Equal(ProductUnit.Liter, group.CanonicalUnit);
            Assert.True(group.Normalized);
            Assert.Equal(2, group.Shops.Count);

            var seriesA = Assert.Single(group.Shops, s => s.ShoppingLocationPublicId == shopA);
            Assert.Equal("Price Shop A", seriesA.LocationName);
            Assert.Equal(2, seriesA.Count);
            Assert.Equal(150m, seriesA.Min);
            Assert.Equal(200m, seriesA.Max);
            Assert.Equal(150m, seriesA.Latest);
            Assert.Equal(175m, seriesA.Average);
            // Oldest first, so a line chart can render the points in the order it receives them.
            Assert.Equal(new[] { 200m, 150m }, seriesA.Points.Select(p => p.UnitPrice));
            // The quantity is reported as purchased (2 l), not as normalized - the normalized
            // basis is already stated once on the group.
            Assert.Equal(new[] { 1m, 2m }, seriesA.Points.Select(p => p.Quantity));
            Assert.All(seriesA.Points, p => Assert.Equal(ProductUnit.Liter, p.Unit));

            var seriesB = Assert.Single(group.Shops, s => s.ShoppingLocationPublicId == shopB);
            Assert.Equal("Price Shop B", seriesB.LocationName);
            Assert.Equal(1, seriesB.Count);
            Assert.Equal(250m, seriesB.Min);
            Assert.Equal(250m, seriesB.Max);
            Assert.Equal(250m, seriesB.Latest);
            Assert.Equal(250m, seriesB.Average);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The comparison the whole endpoint exists for: a 2 l bottle at 300 costs more in total than a
    /// 1 l bottle at 200, and is nonetheless the cheaper buy. Without normalization the raw prices
    /// would rank these two exactly backwards, which is the mistake #128 was filed for.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_LargerPackAtHigherPrice_IsCorrectlyTheCheaperUnitPrice()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-pack-size");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shop = await CreateShoppingLocationAsync("Price Pack Size Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "pack-size");

            var small = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 200, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(small, DateTime.UtcNow.AddDays(-4));
            var large = await CreatePurchaseAsync(productPublicId, quantity: 2, price: 300, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(large, DateTime.UtcNow.AddDays(-3));

            var history = await GetPriceHistoryAsync(productPublicId);

            var series = Assert.Single(SingleGroup(history, Currency.Huf).Shops);
            Assert.Equal(new[] { 200m, 150m }, series.Points.Select(p => p.UnitPrice));

            // The point of the whole task, stated as plainly as it can be: the dearer receipt is
            // the cheaper purchase.
            var perLitreOfTheBiggerPack = series.Points.Single(p => p.Quantity == 2m).UnitPrice;
            var perLitreOfTheSmallerPack = series.Points.Single(p => p.Quantity == 1m).UnitPrice;
            Assert.True(perLitreOfTheBiggerPack < perLitreOfTheSmallerPack,
                $"the 2 l purchase must be the cheaper one per litre, got {perLitreOfTheBiggerPack} vs {perLitreOfTheSmallerPack}");

            // And the badge agrees with the chart.
            Assert.NotNull(history.BestKnown);
            Assert.Equal(150m, history.BestKnown.UnitPrice);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Two currencies are two entries, never one. This milestone has no exchange rate and invents
    /// none, so nothing in the response may pool them - not the per-shop statistics, and not the
    /// best-known-price badge, which is picked inside one currency only.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_TwoCurrencies_ReturnsTwoEntriesNeverMixed()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-two-currencies");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shop = await CreateShoppingLocationAsync("Price Currency Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "two-currencies");

            // Two HUF purchases so HUF is unambiguously the currency with the most data points,
            // and one EUR purchase whose numeric unit price (2) is far below every HUF one - if
            // any aggregate mixed the two, the EUR row would win comparisons it must never enter.
            var huf1 = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 400, currency: Currency.Huf, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(huf1, DateTime.UtcNow.AddDays(-9));
            var huf2 = await CreatePurchaseAsync(productPublicId, quantity: 2, price: 600, currency: Currency.Huf, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(huf2, DateTime.UtcNow.AddDays(-8));
            var eur = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 2, currency: Currency.Eur, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(eur, DateTime.UtcNow.AddDays(-7));

            var history = await GetPriceHistoryAsync(productPublicId);

            Assert.Equal(2, history.ByCurrency.Count);

            var hufSeries = Assert.Single(Assert.Single(history.ByCurrency[Currency.Huf]).Shops);
            Assert.Equal(2, hufSeries.Count);
            Assert.Equal(300m, hufSeries.Min);
            Assert.Equal(400m, hufSeries.Max);
            Assert.Equal(350m, hufSeries.Average);

            var eurSeries = Assert.Single(Assert.Single(history.ByCurrency[Currency.Eur]).Shops);
            Assert.Equal(1, eurSeries.Count);
            Assert.Equal(2m, eurSeries.Average);

            // The badge belongs to the currency with the most data points, and reports that
            // currency explicitly - a bare "150" with no currency is exactly the blend this rule
            // exists to prevent.
            Assert.NotNull(history.BestKnown);
            Assert.Equal(Currency.Huf, history.BestKnown.Currency);
            Assert.Equal(300m, history.BestKnown.UnitPrice);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// A product bought by weight and by the pack produces two basis groups - one normalized, one
    /// not - never a single blended series. A price per kilogram and a price per pack are not
    /// points on the same axis, and the <see cref="PriceBasisGroup.Normalized"/> flag is what tells
    /// the client which of the two it is looking at.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_MassAndCountableUnits_ReturnsTwoBasisGroupsNeverBlended()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-two-bases");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shop = await CreateShoppingLocationAsync("Price Basis Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Meat, ProductUnit.Kilogram, "two-bases");

            // 2 kg for 3000 => 1500/kg, a normalized basis.
            var kiloPurchase = await CreatePurchaseAsync(productPublicId, quantity: 2, price: 3000, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(kiloPurchase, DateTime.UtcNow.AddDays(-6));

            // How one product legitimately ends up with purchases on two bases: the unit lives on
            // the product, ProductFunctions.CreateInventoryItemAsync copies it onto each item at
            // creation, and an existing item keeps the unit it was created with. So re-shelving
            // the product as a pack good leaves the earlier kilogram purchases exactly as they
            // were - which is the state this endpoint has to report without blending.
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1.0/product/{productPublicId}", new UpdateProductRequest
            {
                Unit = ProductUnit.Pack
            });
            var updateBody = await updateResponse.Content.ReadAsStringAsync();
            Assert.True(updateResponse.StatusCode == HttpStatusCode.OK, $"Update product unit failed: {updateResponse.StatusCode} {updateBody}");

            // 3 packs for 900 => 300/pack, a countable basis.
            var packPurchase = await CreatePurchaseAsync(productPublicId, quantity: 3, price: 900, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(packPurchase, DateTime.UtcNow.AddDays(-5));

            var history = await GetPriceHistoryAsync(productPublicId);

            var currencyEntry = Assert.Single(history.ByCurrency);
            Assert.Equal(Currency.Huf, currencyEntry.Key);
            Assert.Equal(2, currencyEntry.Value.Count);

            var kiloGroup = Assert.Single(currencyEntry.Value, g => g.SeriesKey == "per-kg");
            Assert.Equal(ProductUnit.Kilogram, kiloGroup.CanonicalUnit);
            Assert.True(kiloGroup.Normalized);
            Assert.Equal(1500m, Assert.Single(kiloGroup.Shops).Latest);

            var packGroup = Assert.Single(currencyEntry.Value, g => g.SeriesKey == "per-pack");
            Assert.Equal(ProductUnit.Pack, packGroup.CanonicalUnit);
            // Not normalized: still perfectly comparable to another price per pack, but never to a
            // price per kilogram - which is the distinction the client has to label.
            Assert.False(packGroup.Normalized);
            Assert.Equal(300m, Assert.Single(packGroup.Shops).Latest);

            // The 300/pack is numerically far below the 1500/kg, so a blended series would have
            // handed the badge to it. The badge stays inside one basis - the one with the most
            // data points, with the enum-then-ordinal tie-break deciding this 1-1 tie in favour
            // of "per-kg".
            Assert.NotNull(history.BestKnown);
            Assert.Equal("per-kg", history.BestKnown.SeriesKey);
            Assert.Equal(1500m, history.BestKnown.UnitPrice);

            // One purchase per basis, so the newest purchase's own group has no average to be
            // above - and specifically not an average blended with the other basis.
            Assert.Null(history.LatestAboveAverageRatio);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The badge reports the cheapest price actually paid, and says where and when - a number with
    /// no shop and no date is not something a family can act on next time they shop.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_BestKnown_PicksLowestUnitPriceWithShopAndTimestamp()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-best-known");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var dearShop = await CreateShoppingLocationAsync("Price Dear Shop");
            var cheapShop = await CreateShoppingLocationAsync("Price Cheap Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "best-known");

            var dear = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 500, shoppingLocationPublicId: dearShop);
            await SetPurchasedAtAsync(dear, DateTime.UtcNow.AddDays(-3));

            // The cheapest purchase is deliberately the OLDER one, so "cheapest" cannot be
            // confused with "most recent".
            var cheapAt = DateTime.UtcNow.AddDays(-20);
            var cheap = await CreatePurchaseAsync(productPublicId, quantity: 4, price: 800, shoppingLocationPublicId: cheapShop);
            await SetPurchasedAtAsync(cheap, cheapAt);

            var history = await GetPriceHistoryAsync(productPublicId);

            Assert.NotNull(history.BestKnown);
            Assert.Equal(200m, history.BestKnown.UnitPrice);
            Assert.Equal(cheapShop, history.BestKnown.ShoppingLocationPublicId);
            Assert.Equal("Price Cheap Shop", history.BestKnown.LocationName);
            Assert.Equal("per-l", history.BestKnown.SeriesKey);
            Assert.Equal(Currency.Huf, history.BestKnown.Currency);
            Assert.Equal(cheapAt, history.BestKnown.At, TimeSpan.FromSeconds(1));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// "You paid more than usual" as a ratio the client can threshold itself. Above 1 when the
    /// newest purchase is dearer than the household's own average for that product on that basis.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_LatestDearerThanAverage_ReturnsRatioAboveOne()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-above-average");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shop = await CreateShoppingLocationAsync("Price Above Average Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "above-average");

            // 100/l, 200/l, then 300/l last: average 200, latest 300 => ratio 1.5.
            var first = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 100, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(first, DateTime.UtcNow.AddDays(-30));
            var second = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 200, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(second, DateTime.UtcNow.AddDays(-20));
            var third = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 300, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(third, DateTime.UtcNow.AddDays(-1));

            var history = await GetPriceHistoryAsync(productPublicId);

            Assert.NotNull(history.LatestAboveAverageRatio);
            Assert.True(history.LatestAboveAverageRatio > 1m, $"expected a ratio above 1, got {history.LatestAboveAverageRatio}");
            Assert.Equal(1.5m, history.LatestAboveAverageRatio!.Value, precision: 4);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// A single purchase has no average to be above, so the ratio is null rather than 1 - the
    /// client must be able to tell "not enough history yet" apart from "exactly average".
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_SinglePurchase_ReturnsNullRatio()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-single-purchase");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shop = await CreateShoppingLocationAsync("Price Single Purchase Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "single-purchase");

            var only = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 250, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(only, DateTime.UtcNow.AddDays(-2));

            var history = await GetPriceHistoryAsync(productPublicId);

            Assert.Null(history.LatestAboveAverageRatio);
            // The rest of the response is still fully populated - a missing ratio is not a missing
            // history.
            Assert.NotNull(history.BestKnown);
            Assert.Equal(250m, history.BestKnown.UnitPrice);
            Assert.Equal(1, Assert.Single(SingleGroup(history, Currency.Huf).Shops).Count);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// A purchase with no price is skipped without taking the series down with it. Unlike
    /// spend-by-location, which still counts such a purchase in its item count, every number in
    /// this response is a price - there is nothing truthful to plot for a purchase that has none.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_NullPricePurchase_IsSkippedWithoutBreakingTheSeries()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-null-price");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var shop = await CreateShoppingLocationAsync("Price Null Price Shop");
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "null-price");

            var priced = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 220, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(priced, DateTime.UtcNow.AddDays(-4));
            // No price at all - the shopping location is what makes CreateInventoryItemAsync write
            // a purchase row for it in the first place (see CreatePurchaseAsync's remarks).
            var unpriced = await CreatePurchaseAsync(productPublicId, quantity: 5, price: null, shoppingLocationPublicId: shop);
            await SetPurchasedAtAsync(unpriced, DateTime.UtcNow.AddDays(-2));

            var history = await GetPriceHistoryAsync(productPublicId);

            var series = Assert.Single(SingleGroup(history, Currency.Huf).Shops);
            Assert.Equal(1, series.Count);
            Assert.Equal(220m, Assert.Single(series.Points).UnitPrice);
            // The unpriced purchase must not have become a phantom zero - which would drag the
            // minimum and the badge to 0 and make everything look free.
            Assert.Equal(220m, series.Min);
            Assert.NotNull(history.BestKnown);
            Assert.Equal(220m, history.BestKnown.UnitPrice);
            // One usable purchase, so no ratio - and specifically not a ratio computed against a
            // zero-padded average.
            Assert.Null(history.LatestAboveAverageRatio);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The scope rule, tested the only way it can be: another family buys the very same product at
    /// an unmistakable price, and none of it appears. <see cref="Assert.Single{T}(IEnumerable{T})"/>
    /// with no predicate is deliberate - a predicate match alone would still pass with the other
    /// family's series sitting beside it.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_SecondFamilysPurchases_NeverAppear()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("price-fam-a");
            testEmailA = emailA;
            _authHelper.SetAuthToken(authA.AccessToken);
            await CreateFamilyAsync("Price Family A");

            var shopA = await CreateShoppingLocationAsync("Price Family A Shop");
            // Products are global master data, so both families buy this exact product - which is
            // what makes the leak possible to test at all.
            var productPublicId = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "price-fam");
            var purchaseA = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 300, shoppingLocationPublicId: shopA, isSharedWithFamily: true);
            await SetPurchasedAtAsync(purchaseA, DateTime.UtcNow.AddDays(-5));

            var (emailB, authB) = await _authHelper.CreateAndAuthenticateUserAsync("price-fam-b");
            testEmailB = emailB;
            _authHelper.SetAuthToken(authB.AccessToken);
            await CreateFamilyAsync("Price Family B");

            var shopB = await CreateShoppingLocationAsync("Price Family B Shop");
            var purchaseB = await CreatePurchaseAsync(productPublicId, quantity: 1, price: 999999, shoppingLocationPublicId: shopB, isSharedWithFamily: true);
            await SetPurchasedAtAsync(purchaseB, DateTime.UtcNow.AddDays(-4));

            _authHelper.SetAuthToken(authA.AccessToken);
            var history = await GetPriceHistoryAsync(productPublicId);

            var series = Assert.Single(SingleGroup(history, Currency.Huf).Shops);
            Assert.Equal(shopA, series.ShoppingLocationPublicId);
            Assert.Equal(1, series.Count);
            Assert.Equal(300m, Assert.Single(series.Points).UnitPrice);
            Assert.NotNull(history.BestKnown);
            Assert.Equal(300m, history.BestKnown.UnitPrice);
            // Family B's single purchase would have been the household's newest, so a leak would
            // also have produced a ratio here.
            Assert.Null(history.LatestAboveAverageRatio);
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

    /// <summary>
    /// A product the household has never bought answers 200 with an empty history - not 404, and
    /// certainly not another family's numbers. An unknown product id behaves identically, which is
    /// deliberate: distinguishing the two would need a second query whose only product is a probe
    /// for which product ids exist.
    /// </summary>
    [Fact]
    public async Task GetPriceHistory_NeverPurchasedProduct_ReturnsEmptyResponseNotNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("price-never-bought");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var neverBought = await CreateProductAsync(ProductCategory.Milk, ProductUnit.Liter, "never-bought");

            var history = await GetPriceHistoryAsync(neverBought);
            Assert.Empty(history.ByCurrency);
            Assert.Null(history.BestKnown);
            Assert.Null(history.LatestAboveAverageRatio);

            // An id that matches no product at all - same 200, same empty payload.
            var unknown = await GetPriceHistoryAsync(Guid.NewGuid());
            Assert.Empty(unknown.ByCurrency);
            Assert.Null(unknown.BestKnown);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
}
