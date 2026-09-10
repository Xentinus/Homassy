using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Models.Insights;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Functions
{
    /// <summary>
    /// Purchase-price aggregation for the R5 "Insight" milestone's #128 endpoints - the per-shop
    /// price history served by <see cref="Controllers.ProductController.GetPriceHistory"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately its own Functions class rather than more methods on
    /// <see cref="InsightFunctions"/>: price work is the one part of this milestone that depends on
    /// <see cref="UnitNormalization"/> and on Task 11's decimal <c>Price</c> column, and it hangs
    /// off <see cref="Controllers.ProductController"/> (a product's own history belongs on the
    /// product's own controller) rather than off <see cref="Controllers.InsightsController"/>.
    /// Takes <see cref="IDbContextFactory{TContext}"/> plus <see cref="FamilyInsightsCache"/>,
    /// exactly like <see cref="InsightFunctions"/>, and is never constructed by another Functions
    /// class, so there is no mutual dependency to route through <c>FunctionsRuntime</c> for.
    /// </remarks>
    public class PriceInsightFunctions
    {
        /// <summary>
        /// Cache key prefix for <see cref="GetPriceHistoryAsync"/> within a family's
        /// <see cref="FamilyInsightsCache"/> entries - endpoint-qualified for the same reason every
        /// other insight key is (see <see cref="InsightFunctions"/>'s own key constants): the cache
        /// stores values as <see cref="object"/> and casts to whatever <c>T</c> the caller asks
        /// for, so two endpoints sharing a key string under one family would surface as a runtime
        /// <see cref="InvalidCastException"/> on a hit rather than as a compile error.
        ///
        /// <para>
        /// The key actually used is <c>$"{PriceHistoryCacheKey}:{productPublicId}:{days}:u{userId}"</c>
        /// - the product and the window because the answer obviously depends on both, and the
        /// acting user id because <see cref="ComputePriceHistoryAsync"/> scopes purchases through
        /// the same personal-plus-family union the inventory list uses. Two members of one family
        /// therefore do not share an answer (one member's personal purchases are in their own
        /// result and in no one else's), so dropping the user id from the key would serve whichever
        /// member missed the cache first their own personal purchase history to every other member
        /// for the rest of the TTL - a leak between family members, not a stale-answer bug. Same
        /// reasoning as <c>InsightFunctions.SpendByLocationCacheKey</c>.
        /// </para>
        /// </summary>
        private const string PriceHistoryCacheKey = "price-history";

        private static readonly TimeSpan PriceHistoryTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The label <see cref="ShopPriceSeries.LocationName"/> carries for the bucket that folds
        /// every purchase with no shopping location, and the fallback for a location id that can no
        /// longer be resolved to a row. Matches <c>InsightFunctions</c>'s constant of the same name
        /// - the two are intentionally identical strings, since both feed the same client-side
        /// "unknown location" label.
        /// </summary>
        private const string UnknownLocationName = "Unknown location";

        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly FamilyInsightsCache _cache;

        public PriceInsightFunctions(IDbContextFactory<HomassyDbContext> contextFactory, FamilyInsightsCache cache)
        {
            _contextFactory = contextFactory;
            _cache = cache;
        }

        /// <summary>
        /// One product's purchase price history for the caller's household, normalized to a price
        /// per canonical unit so pack sizes can be compared, grouped by currency and then by
        /// comparison basis. Cached for <see cref="PriceHistoryTtl"/> under
        /// <see cref="PriceHistoryCacheKey"/> - see that constant's remarks for how the key is
        /// built and why it always carries the user id.
        /// </summary>
        /// <param name="userId">
        /// The acting user. Required, and used as half of the scope union regardless of whether
        /// <paramref name="familyId"/> is present - <see cref="Controllers.ProductController.GetPriceHistory"/>
        /// short-circuits to 401 before this is ever called for a caller whose session does not
        /// resolve to a local user row.
        /// </param>
        /// <param name="familyId">
        /// The caller's family, or <see langword="null"/> if they have none. A missing family id
        /// only drops the family half of the union, never short-circuits the result: a family-less
        /// caller still has their own personal purchases to show.
        /// </param>
        /// <param name="productPublicId">
        /// The product to report on. A public id this method cannot resolve - unknown, or a
        /// soft-deleted product - yields an empty response rather than a 404, deliberately: see
        /// <see cref="ComputePriceHistoryAsync"/>'s remarks.
        /// </param>
        /// <param name="days">
        /// The window length in days. Bounds are the controller's job to enforce, not this
        /// method's - see <see cref="Controllers.ProductController.GetPriceHistory"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation for this call's own attempt to (re)compute the value - see
        /// <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/> for why a piggybacking caller's own
        /// token can never cancel a computation it did not win the race to start.
        /// </param>
        public Task<PriceHistoryResponse> GetPriceHistoryAsync(int userId, int? familyId, Guid productPublicId, int days, CancellationToken cancellationToken)
        {
            var key = $"{PriceHistoryCacheKey}:{productPublicId}:{days}:u{userId}";

            return _cache.GetOrAddAsync(
                familyId ?? 0,
                key,
                PriceHistoryTtl,
                ct => ComputePriceHistoryAsync(userId, familyId, productPublicId, days, ct),
                cancellationToken);
        }

        /// <summary>
        /// Runs the actual aggregation on a cache miss.
        ///
        /// <para>
        /// <b>Scope.</b> <c>Entities.Product.ProductPurchaseInfo</c> carries no <c>UserId</c> or
        /// <c>FamilyId</c> of its own, so it is scoped through the inventory item it hangs off,
        /// with the same union <c>ProductFunctions.GetInventoryItemsByUserAndFamily</c> and
        /// <c>InsightFunctions.ComputeSpendByLocationAsync</c> use -
        /// <c>item.UserId == userId || (familyId.HasValue &amp;&amp; item.FamilyId == familyId)</c>.
        /// A family member's personal purchases belong in their own price history and in no one
        /// else's, exactly as their personal items belong in their own inventory.
        /// </para>
        ///
        /// <para>
        /// <b>Why an unknown product is an empty response and not a 404.</b> The product is matched
        /// by traversing <c>ProductInventoryItem.Product</c> inside the one query, so an unknown
        /// public id simply matches no rows. Answering 404 would need a second query whose only
        /// purpose is to distinguish "no such product" from "never bought" - and would hand an
        /// unauthenticated-to-this-product caller a probe for which product ids exist. An empty
        /// history is also what the client wants to render for a product nobody has bought yet, so
        /// the two cases collapse deliberately rather than by accident. One consequence worth
        /// knowing: <c>Product</c> is soft-deletable and the navigation is required, so EF's global
        /// query filter drops a deleted product's purchases here - a deleted product reports an
        /// empty history rather than its old prices, which is the behaviour a deleted product
        /// should have.
        /// </para>
        ///
        /// <para>
        /// <b>One query, then in memory.</b> The row count is bounded by one household's purchases
        /// of one product inside a bounded window, so everything after the fetch - normalization,
        /// the per-unit division, the three levels of grouping and the five statistics - happens in
        /// memory. Two things make that the right call rather than a shortcut: the normalization
        /// factors live in <see cref="UnitFunctions.Convert"/> as C# and have no SQL equivalent to
        /// group by, and the grouping key itself (<see cref="UnitNormalization.SeriesKeyFor"/>) is
        /// C# too. The only follow-up query is the one bounded location-name lookup, never a
        /// per-row one.
        /// </para>
        ///
        /// <para>
        /// <b>What is dropped, and why each.</b> A <see langword="null"/> <c>Price</c> is dropped:
        /// there is no price to put on a price chart, and (unlike spend-by-location, which still
        /// counts such a purchase in its item count) every number in this response is a price. A
        /// non-positive <c>OriginalQuantity</c> is dropped because it is the divisor - see
        /// <see cref="ToPricedPurchase"/>. A <see langword="null"/> <c>Currency</c> is dropped
        /// because <see cref="PriceHistoryResponse.ByCurrency"/> is keyed by currency and an amount
        /// with no currency cannot be compared with, or grouped alongside, one that has it; in
        /// practice <c>ProductFunctions.CreateInventoryItemAsync</c> defaults the currency from the
        /// buyer's profile, so a priced purchase with no currency is a legacy row rather than
        /// something the app can still create.
        /// </para>
        /// </summary>
        private async Task<PriceHistoryResponse> ComputePriceHistoryAsync(int userId, int? familyId, Guid productPublicId, int days, CancellationToken cancellationToken)
        {
            using var context = _contextFactory.CreateForReading();

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-days);

            // Non-deleted is enforced by the global soft-delete filter on ProductPurchaseInfo,
            // ProductInventoryItem and Product alike (see HomassyDbContext.OnModelCreating), never
            // by a hand-written IsDeleted check here - plain LINQ over the DbSet picks the filter
            // up on every side of the join for free. Price and Currency are filtered in SQL rather
            // than in memory so an unpriced purchase never crosses the wire at all.
            var rows = await context.ProductPurchaseInfos
                .Where(p => p.ProductInventoryItem.Product.PublicId == productPublicId
                            && p.PurchasedAt >= fromUtc && p.PurchasedAt <= toUtc
                            && p.Price != null
                            && p.Currency != null
                            && p.OriginalQuantity > 0
                            && (p.ProductInventoryItem.UserId == userId || (familyId.HasValue && p.ProductInventoryItem.FamilyId == familyId)))
                .Select(p => new
                {
                    p.PurchasedAt,
                    p.Price,
                    p.OriginalQuantity,
                    // ProductPurchaseInfo has no unit of its own: OriginalQuantity is recorded in
                    // the unit of the inventory item the purchase created.
                    p.ProductInventoryItem.Unit,
                    p.Currency,
                    p.ShoppingLocationId
                })
                .ToListAsync(cancellationToken);

            var priced = rows
                .Select(r => ToPricedPurchase(r.PurchasedAt, r.Price, r.OriginalQuantity, r.Unit, r.Currency, r.ShoppingLocationId))
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();

            if (priced.Count == 0)
            {
                return new PriceHistoryResponse();
            }

            var locationInfoById = await ResolveLocationNamesAsync(
                context,
                priced.Where(p => p.ShoppingLocationId.HasValue).Select(p => p.ShoppingLocationId!.Value),
                cancellationToken);

            var byCurrency = priced
                .GroupBy(p => p.Currency)
                .ToDictionary(
                    currencyGroup => currencyGroup.Key,
                    currencyGroup => (IReadOnlyList<PriceBasisGroup>)currencyGroup
                        .GroupBy(p => p.SeriesKey, StringComparer.Ordinal)
                        .Select(basisGroup => new PriceBasisGroup
                        {
                            SeriesKey = basisGroup.Key,
                            // Every purchase sharing a series key shares a canonical unit and a
                            // normalized flag by construction - SeriesKeyFor is a pure function of
                            // the canonical unit - so taking them from the group's first row is
                            // reading a group-wide fact, not picking a representative.
                            CanonicalUnit = basisGroup.First().CanonicalUnit,
                            Normalized = basisGroup.First().Normalized,
                            Shops = basisGroup
                                .GroupBy(p => p.ShoppingLocationId)
                                .Select(shopGroup => BuildShopSeries(shopGroup, locationInfoById))
                                // Busiest location first, then by name, so the series order (and
                                // therefore the colour each shop gets) is stable across requests
                                // rather than dependent on row order.
                                .OrderByDescending(s => s.Count)
                                .ThenBy(s => s.LocationName, StringComparer.Ordinal)
                                .ToList()
                        })
                        .OrderByDescending(g => g.Shops.Sum(s => s.Count))
                        .ThenBy(g => g.SeriesKey, StringComparer.Ordinal)
                        .ToList());

            return new PriceHistoryResponse
            {
                ByCurrency = byCurrency,
                BestKnown = PickBestKnown(priced, locationInfoById),
                LatestAboveAverageRatio = ComputeLatestAboveAverageRatio(priced)
            };
        }

        /// <summary>
        /// How far back <see cref="GetBestPricesAsync"/> looks. Fixed rather than caller-supplied:
        /// the shopping list asking "what is the least I have ever paid for this" wants the whole
        /// useful history, not a window it has to choose, and a per-call window would multiply the
        /// number of distinct answers for no benefit to the one screen that asks. A year matches
        /// the longest window <see cref="Controllers.ProductController.GetPriceHistory"/> offers,
        /// so a badge on the shopping list can never claim a price the product's own chart cannot
        /// show, and it keeps the query bounded the same way every other insight query is.
        /// </summary>
        private const int BestPricesWindowDays = 365;

        /// <summary>
        /// The most product ids one call may ask about - see
        /// <see cref="Controllers.InsightsController.GetBestPrices"/>, which rejects a longer list
        /// with 400 rather than truncating it. Comfortably above any real shopping list while
        /// still keeping the <c>= ANY(@ids)</c> array bounded.
        /// </summary>
        public const int MaxBestPriceProductIds = 200;

        /// <summary>
        /// The cheapest price the caller's household has paid for each of several products, in one
        /// round trip - the shopping list's per-row price chips and its estimated total, without a
        /// request per row.
        ///
        /// <para>
        /// <b>Products with no usable purchase history are omitted, not returned as null.</b> An
        /// absent key says "no price known" once; a present key with a null value would make every
        /// consumer check twice for the same thing, and would let a serialization round trip turn
        /// "unknown" into a rendered blank chip.
        /// </para>
        ///
        /// <para>
        /// <b>Deliberately not cached</b>, unlike every other method here. The cache key would have
        /// to cover the exact id set, which is whatever happens to be on one list at one moment -
        /// so entries would almost never be reused, while still occupying the shared per-family
        /// cache. The single query this runs is the cheaper answer.
        /// </para>
        /// </summary>
        /// <param name="userId">The acting user - half of the scope union, as everywhere here.</param>
        /// <param name="familyId">The caller's family, or <see langword="null"/>.</param>
        /// <param name="productPublicIds">
        /// The products to look up. An empty list returns an empty map <b>without querying at
        /// all</b> - the length bound is the controller's to enforce.
        /// </param>
        /// <param name="cancellationToken">Cancellation for the one query.</param>
        public async Task<IReadOnlyDictionary<Guid, BestKnownPrice>> GetBestPricesAsync(
            int userId,
            int? familyId,
            IReadOnlyList<Guid> productPublicIds,
            CancellationToken cancellationToken)
        {
            var distinctIds = productPublicIds.Distinct().ToList();
            if (distinctIds.Count == 0)
            {
                return new Dictionary<Guid, BestKnownPrice>();
            }

            using var context = _contextFactory.CreateForReading();

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-BestPricesWindowDays);

            // ONE query for the whole batch - Npgsql translates Contains over the id list to
            // = ANY(@ids), so the number of products asked about changes the parameter, never the
            // number of round trips. The location's name and public id are projected through the
            // optional ShoppingLocation navigation in this same statement (a LEFT JOIN) rather
            // than resolved by a follow-up lookup the way ComputePriceHistoryAsync does it: that
            // method needs the location only for the groups it builds, while here every product's
            // answer carries one, and one query is this endpoint's whole point. A soft-deleted
            // location comes back null through the global query filter and falls back to
            // UnknownLocationName below, exactly as an absent one does.
            var rows = await context.ProductPurchaseInfos
                .Where(p => distinctIds.Contains(p.ProductInventoryItem.Product.PublicId)
                            && p.PurchasedAt >= fromUtc && p.PurchasedAt <= toUtc
                            && p.Price != null
                            && p.Currency != null
                            && p.OriginalQuantity > 0
                            && (p.ProductInventoryItem.UserId == userId || (familyId.HasValue && p.ProductInventoryItem.FamilyId == familyId)))
                .Select(p => new
                {
                    ProductPublicId = p.ProductInventoryItem.Product.PublicId,
                    p.PurchasedAt,
                    p.Price,
                    p.OriginalQuantity,
                    p.ProductInventoryItem.Unit,
                    p.Currency,
                    p.ShoppingLocationId,
                    LocationPublicId = (Guid?)p.ShoppingLocation!.PublicId,
                    LocationName = (string?)p.ShoppingLocation!.Name
                })
                .ToListAsync(cancellationToken);

            // Built from the columns the one query already returned - not a second lookup. Keyed
            // by the internal location id purely so PickBestKnown can be shared verbatim with
            // ComputePriceHistoryAsync instead of growing a second copy of the picking rule.
            var locationInfoById = rows
                .Where(r => r.ShoppingLocationId.HasValue && r.LocationPublicId.HasValue && r.LocationName != null)
                .GroupBy(r => r.ShoppingLocationId!.Value)
                .ToDictionary(g => g.Key, g => (g.First().LocationPublicId!.Value, g.First().LocationName!));

            var result = new Dictionary<Guid, BestKnownPrice>();

            foreach (var productGroup in rows.GroupBy(r => r.ProductPublicId))
            {
                var priced = productGroup
                    .Select(r => ToPricedPurchase(r.PurchasedAt, r.Price, r.OriginalQuantity, r.Unit, r.Currency, r.ShoppingLocationId))
                    .Where(p => p.HasValue)
                    .Select(p => p!.Value)
                    .ToList();

                // Same picking rule as the product's own price history, so a chip on the shopping
                // list and the badge on the product page can never disagree.
                var best = PickBestKnown(priced, locationInfoById);
                if (best != null)
                {
                    result[productGroup.Key] = best;
                }
            }

            return result;
        }

        /// <summary>
        /// One purchase reduced to the shape every aggregate here is computed from: its price per
        /// canonical unit, plus the basis that price is expressed on.
        /// </summary>
        /// <param name="PurchasedAt">When the purchase was made (UTC).</param>
        /// <param name="UnitPrice">Price divided by the normalized quantity.</param>
        /// <param name="Quantity">The quantity as purchased, for <see cref="PricePoint.Quantity"/>.</param>
        /// <param name="Unit">The unit <paramref name="Quantity"/> is in, as purchased.</param>
        /// <param name="Currency">The currency paid in - never null by the time a row gets here.</param>
        /// <param name="ShoppingLocationId">
        /// The internal location id, kept internal on purpose: it is the grouping and lookup key,
        /// and only the resolved <c>PublicId</c> ever reaches a response.
        /// </param>
        /// <param name="SeriesKey"><see cref="UnitNormalization.SeriesKeyFor"/>'s key.</param>
        /// <param name="CanonicalUnit">The unit <paramref name="UnitPrice"/> is a price per.</param>
        /// <param name="Normalized">Whether this is a genuine per-weight/per-volume comparison.</param>
        private readonly record struct PricedPurchase(
            DateTime PurchasedAt,
            decimal UnitPrice,
            decimal Quantity,
            Unit Unit,
            Currency Currency,
            int? ShoppingLocationId,
            string SeriesKey,
            Unit CanonicalUnit,
            bool Normalized);

        /// <summary>
        /// Turns one raw purchase row into a <see cref="PricedPurchase"/>, or
        /// <see langword="null"/> when the row cannot become a comparable price at all.
        ///
        /// <para>
        /// <b>The division guard is on the normalized value, not on
        /// <see cref="NormalizedQuantity.Normalized"/>.</b> That flag is <see langword="false"/> for
        /// every countable unit - a Pack, a Piece, a Box - even when the quantity is a perfectly
        /// ordinary positive number, because a price per pack is not comparable to a price per
        /// kilogram. Skipping those rows would throw away exactly the groups this endpoint is
        /// supposed to report separately (a product bought both by weight and by the pack must
        /// produce two groups, one flagged and one not), so the flag is carried into the group
        /// rather than used as a filter. What actually protects the division is
        /// <see cref="NormalizedQuantity.Value"/> being strictly positive: a zero or negative
        /// purchased quantity normalizes to a zero or negative value, which is why the query
        /// already excludes those rows and why this method still checks - <c>Normalize</c>
        /// deliberately returns the converted value even for a quantity it flags as unusable
        /// (see <see cref="UnitNormalization"/>), so the flag alone would not have stopped a
        /// division by zero here.
        /// </para>
        /// </summary>
        private static PricedPurchase? ToPricedPurchase(
            DateTime purchasedAt,
            decimal? price,
            decimal originalQuantity,
            Unit unit,
            Currency? currency,
            int? shoppingLocationId)
        {
            if (!price.HasValue || !currency.HasValue)
            {
                return null;
            }

            var normalized = UnitNormalization.Normalize(originalQuantity, unit);
            if (normalized.Value <= 0)
            {
                return null;
            }

            return new PricedPurchase(
                purchasedAt,
                price.Value / normalized.Value,
                originalQuantity,
                unit,
                currency.Value,
                shoppingLocationId,
                UnitNormalization.SeriesKeyFor(normalized),
                normalized.CanonicalUnit,
                normalized.Normalized);
        }

        /// <summary>
        /// Resolves the display name and public id of every location id actually referenced, in one
        /// bounded query - never one per row and never one per location. A location that can no
        /// longer be resolved (soft-deleted after the purchase was made) is simply absent from the
        /// result, and every caller here falls back to <see cref="UnknownLocationName"/> with a
        /// <see langword="null"/> public id for it - the same pair of fallbacks the genuinely
        /// location-less bucket uses, since a missing location row means there is no public id to
        /// carry either.
        /// </summary>
        private static async Task<Dictionary<int, (Guid PublicId, string Name)>> ResolveLocationNamesAsync(
            HomassyDbContext context,
            IEnumerable<int> locationIds,
            CancellationToken cancellationToken)
        {
            var distinctIds = locationIds.Distinct().ToList();
            if (distinctIds.Count == 0)
            {
                return [];
            }

            return await context.ShoppingLocations
                .Where(l => distinctIds.Contains(l.Id))
                .Select(l => new { l.Id, l.PublicId, l.Name })
                .ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Name), cancellationToken);
        }

        /// <summary>
        /// Folds one location's purchases within one currency and one basis into the wire shape,
        /// with all five statistics computed over that group alone. <see cref="ShopPriceSeries.Latest"/>
        /// is the price of the newest purchase by <c>PurchasedAt</c> rather than the last row in
        /// enumeration order, which is why the points are ordered before anything reads them.
        /// </summary>
        private static ShopPriceSeries BuildShopSeries(
            IGrouping<int?, PricedPurchase> shopGroup,
            IReadOnlyDictionary<int, (Guid PublicId, string Name)> locationInfoById)
        {
            var ordered = shopGroup.OrderBy(p => p.PurchasedAt).ToList();

            (Guid PublicId, string Name)? resolved = shopGroup.Key.HasValue && locationInfoById.TryGetValue(shopGroup.Key.Value, out var info)
                ? info
                : null;

            return new ShopPriceSeries
            {
                ShoppingLocationPublicId = resolved?.PublicId,
                LocationName = resolved?.Name ?? UnknownLocationName,
                Points = ordered
                    .Select(p => new PricePoint
                    {
                        PurchasedAt = p.PurchasedAt,
                        UnitPrice = p.UnitPrice,
                        Quantity = p.Quantity,
                        Unit = p.Unit
                    })
                    .ToList(),
                Min = ordered.Min(p => p.UnitPrice),
                Max = ordered.Max(p => p.UnitPrice),
                Latest = ordered[^1].UnitPrice,
                Average = ordered.Average(p => p.UnitPrice),
                Count = ordered.Count
            };
        }

        /// <summary>
        /// The cheapest unit price actually paid - picked <b>within a single currency and a single
        /// series key</b>, never across either. Comparing 150 HUF per litre against 2 EUR per litre
        /// would need an exchange rate this milestone deliberately does not have, and comparing a
        /// price per kilogram against a price per pack is meaningless at any rate.
        ///
        /// <para>
        /// When the data spans several currencies or bases, the one with the most data points wins:
        /// that is the currency and basis the household actually shops in, so its best price is the
        /// one worth putting on a badge, and a single stray purchase in another currency cannot
        /// hijack the badge. Ties are broken deterministically - by the currency's enum value, then
        /// by series key ordinal, then by the most recent purchase among equally cheap ones (a
        /// fresher proof of the same price) - so the same data always produces the same badge
        /// rather than one that moves with row order.
        /// </para>
        /// </summary>
        private static BestKnownPrice? PickBestKnown(
            IEnumerable<PricedPurchase> priced,
            IReadOnlyDictionary<int, (Guid PublicId, string Name)> locationInfoById)
        {
            var winningGroup = priced
                .GroupBy(p => (p.Currency, p.SeriesKey))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.Currency)
                .ThenBy(g => g.Key.SeriesKey, StringComparer.Ordinal)
                .FirstOrDefault();

            if (winningGroup == null)
            {
                return null;
            }

            var cheapest = winningGroup
                .OrderBy(p => p.UnitPrice)
                .ThenByDescending(p => p.PurchasedAt)
                .First();

            (Guid PublicId, string Name)? resolved = cheapest.ShoppingLocationId.HasValue
                && locationInfoById.TryGetValue(cheapest.ShoppingLocationId.Value, out var info)
                ? info
                : null;

            return new BestKnownPrice
            {
                UnitPrice = cheapest.UnitPrice,
                Currency = cheapest.Currency,
                ShoppingLocationPublicId = resolved?.PublicId,
                LocationName = resolved?.Name ?? UnknownLocationName,
                At = cheapest.PurchasedAt,
                SeriesKey = cheapest.SeriesKey
            };
        }

        /// <summary>
        /// How the most recent purchase compares to what this household usually pays: the newest
        /// purchase's unit price over the average unit price of the (currency, series key) group it
        /// belongs to. Above 1 means dearer than usual.
        ///
        /// <para>
        /// The average is taken over that one group across every shop, not over the whole response:
        /// the question is "did I pay more than usual for this product on this basis", and averaging
        /// two currencies or a per-kilogram price with a per-pack one would answer nothing.
        /// <see langword="null"/> whenever the ratio would be meaningless rather than merely
        /// uninteresting - a group with a single purchase (a lone data point is not an average to
        /// be above) or an average of zero (every purchase in it was free, so there is no ratio to
        /// take at all, and dividing would throw).
        /// </para>
        /// </summary>
        private static decimal? ComputeLatestAboveAverageRatio(IReadOnlyList<PricedPurchase> priced)
        {
            var latest = priced
                .OrderByDescending(p => p.PurchasedAt)
                .First();

            var sameBasis = priced
                .Where(p => p.Currency == latest.Currency && string.Equals(p.SeriesKey, latest.SeriesKey, StringComparison.Ordinal))
                .ToList();

            if (sameBasis.Count < 2)
            {
                return null;
            }

            var average = sameBasis.Average(p => p.UnitPrice);
            if (average == 0m)
            {
                return null;
            }

            return latest.UnitPrice / average;
        }
    }
}
