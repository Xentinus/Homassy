using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Models.Insights;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Functions
{
    /// <summary>
    /// Business logic for the R5 "Insight" family-scoped aggregation endpoints served by
    /// <see cref="Controllers.InsightsController"/>.
    /// </summary>
    /// <remarks>
    /// Takes <see cref="IDbContextFactory{TContext}"/> directly, like the other Functions classes
    /// that need nothing but a context (see the constructor table in <c>Homassy.API/CLAUDE.md</c>),
    /// plus <see cref="FamilyInsightsCache"/> - the singleton every R5 insight endpoint fronts its
    /// aggregation with. This class is never constructed by another Functions class, so there is no
    /// mutual-dependency cycle to route through <see cref="FunctionsRuntime"/> for.
    /// </remarks>
    public class InsightFunctions
    {
        /// <summary>
        /// How many of the family's largest categories become their own <see cref="CompositionSlice"/>;
        /// every category past this rank is folded into <see cref="InventoryCompositionResponse.OtherCount"/>
        /// instead, so the chart this feeds never has to render (and label) more slices than a legend can hold.
        /// </summary>
        private const int MaxSlices = 8;

        /// <summary>
        /// Cache key for <see cref="GetInventoryCompositionAsync"/> within a family's
        /// <see cref="FamilyInsightsCache"/> entries. Endpoint-qualified and never a bare parameter
        /// string: the cache stores values as <see cref="object"/> and casts to whatever <c>T</c> the
        /// caller asks for, so two endpoints sharing a key under one family would surface as a runtime
        /// <see cref="InvalidCastException"/> on a cache hit rather than a compile error.
        /// </summary>
        private const string CompositionCacheKey = "composition";

        private static readonly TimeSpan CompositionTtl = TimeSpan.FromMinutes(5);

        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly FamilyInsightsCache _cache;

        public InsightFunctions(IDbContextFactory<HomassyDbContext> contextFactory, FamilyInsightsCache cache)
        {
            _contextFactory = contextFactory;
            _cache = cache;
        }

        /// <summary>
        /// The family's current inventory, broken down by product category. Cached per family for
        /// 5 minutes under <see cref="CompositionCacheKey"/> - see <see cref="FamilyInsightsCache"/>
        /// for the single-flight and per-family isolation guarantees that cache provides.
        /// </summary>
        /// <param name="familyId">
        /// The caller's family. Callers with no family must never reach this method - see
        /// <see cref="Controllers.InsightsController"/>, which short-circuits to an empty response
        /// before any query runs rather than passing a sentinel value here.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation for this call's own attempt to (re)compute the value. Only reaches the
        /// underlying query when this call is the one that wins the cache's single-flight race -
        /// see <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/>.
        /// </param>
        public Task<InventoryCompositionResponse> GetInventoryCompositionAsync(int familyId, CancellationToken cancellationToken)
        {
            return _cache.GetOrAddAsync(
                familyId,
                CompositionCacheKey,
                CompositionTtl,
                ct => ComputeInventoryCompositionAsync(familyId, ct),
                cancellationToken);
        }

        /// <summary>
        /// Runs the actual aggregation on a cache miss. The grouping and the per-category count both
        /// happen in SQL - a single <c>GROUP BY</c> round trip - never by pulling the family's
        /// inventory rows into memory first and grouping them in C#, which would pass every test at
        /// this scale and fall over on a real household's worth of stock.
        /// </summary>
        private async Task<InventoryCompositionResponse> ComputeInventoryCompositionAsync(int familyId, CancellationToken cancellationToken)
        {
            using var context = _contextFactory.CreateForReading();

            // Non-deleted is enforced by the global soft-delete query filter on both
            // ProductInventoryItem and Product (see HomassyDbContext.OnModelCreating), not by an
            // explicit IsDeleted check here. A product with no category (Category is nullable)
            // folds into ProductCategory.Other in the same GROUP BY, rather than as a second,
            // untranslatable client-side bucket.
            var categoryCounts = await context.ProductInventoryItems
                .Where(item => item.FamilyId == familyId && !item.IsFullyConsumed)
                .GroupBy(item => item.Product.Category ?? ProductCategory.Other)
                .Select(group => new { Category = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            var totalCount = categoryCounts.Sum(c => c.Count);

            // Only this small, already-aggregated list (at most one row per distinct category the
            // family actually uses) is ever materialised - ordering/Take/Skip below run over that,
            // never over the underlying inventory rows.
            var ordered = categoryCounts
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Category)
                .ToList();

            var otherCount = ordered.Skip(MaxSlices).Sum(c => c.Count);

            var slices = ordered
                .Take(MaxSlices)
                .Select(c => new CompositionSlice
                {
                    Category = c.Category,
                    Count = c.Count,
                    Share = totalCount > 0 ? Math.Round((decimal)c.Count / totalCount, 4) : 0m
                })
                .ToList();

            return new InventoryCompositionResponse
            {
                Slices = slices,
                OtherCount = otherCount,
                TotalCount = totalCount
            };
        }
    }
}
