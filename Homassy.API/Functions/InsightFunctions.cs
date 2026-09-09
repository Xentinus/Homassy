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
        /// How many of the family's largest non-<see cref="ProductCategory.Other"/> categories
        /// become their own <see cref="CompositionSlice"/>; every one of those categories past
        /// this rank is folded into <see cref="InventoryCompositionResponse.OtherCount"/> instead,
        /// so the chart this feeds never has to render (and label) more slices than a legend can
        /// hold. <see cref="ProductCategory.Other"/> itself is never ranked against this cap - see
        /// <see cref="InventoryCompositionResponse.OtherCount"/>'s XML doc.
        /// </summary>
        private const int MaxSlices = 8;

        /// <summary>
        /// Cache key prefix for <see cref="GetInventoryCompositionAsync"/> within a family's
        /// <see cref="FamilyInsightsCache"/> entries. Endpoint-qualified and never a bare parameter
        /// string: the cache stores values as <see cref="object"/> and casts to whatever <c>T</c> the
        /// caller asks for, so two endpoints sharing a key under one family would surface as a runtime
        /// <see cref="InvalidCastException"/> on a cache hit rather than a compile error.
        ///
        /// <para>
        /// The key actually used is <c>$"{CompositionCacheKey}:u{userId}"</c> - this prefix plus the
        /// acting user's id, never the prefix alone. The cached result depends on the user, not only
        /// the family: it is the union of the user's own personal inventory items and their family's
        /// shared items (see <see cref="ComputeInventoryCompositionAsync"/>), the same scoping every
        /// other inventory query in the codebase uses. <see cref="FamilyInsightsCache"/> only keys on
        /// (family, key), so without the user id folded into the key, two members of the same family
        /// would share one cache entry - whichever member's request happened to miss the cache first
        /// would have their own personal items served to every other member of the family for the
        /// rest of the TTL. That is a data leak between family members, not merely a stale-answer bug.
        /// </para>
        ///
        /// <para>
        /// <b>General rule for every insight endpoint that follows this pattern</b> (Tasks 8, 9, 19,
        /// 20): every input the computed result depends on must appear in the cache key - the family
        /// id alone is only ever enough when the result truly depends on nothing else.
        /// </para>
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
        /// The acting user's current inventory - their own personal items plus their family's
        /// shared items - broken down by product category. Cached for 5 minutes under a key
        /// derived from <see cref="CompositionCacheKey"/> - see its remarks for why the user id
        /// has to be part of that key - and see <see cref="FamilyInsightsCache"/> for the
        /// single-flight guarantees the cache itself provides.
        /// </summary>
        /// <param name="userId">
        /// The acting user. Required - callers with no user id must never reach this method, since
        /// the result would be meaningless without one; see <see cref="Controllers.InsightsController"/>,
        /// which short-circuits to an empty response before any query runs in that case, rather
        /// than passing a sentinel value here.
        /// </param>
        /// <param name="familyId">
        /// The caller's family, or <see langword="null"/> if they have none. Unlike
        /// <paramref name="userId"/>, a missing family id is not a short-circuit case: it simply
        /// drops the family half of the union in <see cref="ComputeInventoryCompositionAsync"/>,
        /// so a caller with no family still sees their own personal items.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation for this call's own attempt to (re)compute the value. Only reaches the
        /// underlying query when this call is the one that wins the cache's single-flight race -
        /// see <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/>.
        /// </param>
        public Task<InventoryCompositionResponse> GetInventoryCompositionAsync(int userId, int? familyId, CancellationToken cancellationToken)
        {
            // The family bucket collapses every family-less caller to 0, but that can never
            // collide two users' entries: the per-user suffix on the key itself already makes the
            // full (familyBucket, key) pair unique per user, with or without a family.
            return _cache.GetOrAddAsync(
                familyId ?? 0,
                $"{CompositionCacheKey}:u{userId}",
                CompositionTtl,
                ct => ComputeInventoryCompositionAsync(userId, familyId, ct),
                cancellationToken);
        }

        /// <summary>
        /// Runs the actual aggregation on a cache miss. Scopes inventory items the same way every
        /// other inventory query in the codebase does (e.g.
        /// <c>ProductFunctions.GetInventoryItemsByUserAndFamily</c>): the union of the caller's own
        /// personal items and their family's shared items, never family-shared items alone - a
        /// family-only filter would plot a different set of items than the member's own inventory
        /// list shows them, with a total that disagrees, and would wrongly come back empty for a
        /// member who has personal items but no family. The grouping and the per-category count
        /// both happen in SQL - a single <c>GROUP BY</c> round trip - never by pulling the caller's
        /// inventory rows into memory first and grouping them in C#, which would pass every test at
        /// this scale and fall over on a real household's worth of stock.
        /// </summary>
        private async Task<InventoryCompositionResponse> ComputeInventoryCompositionAsync(int userId, int? familyId, CancellationToken cancellationToken)
        {
            using var context = _contextFactory.CreateForReading();

            // Non-deleted is enforced by the global soft-delete query filter on both
            // ProductInventoryItem and Product (see HomassyDbContext.OnModelCreating), not by an
            // explicit IsDeleted check here. A product with no category (Category is nullable)
            // folds into ProductCategory.Other in the same GROUP BY, rather than as a second,
            // untranslatable client-side bucket.
            var categoryCounts = await context.ProductInventoryItems
                .Where(item => (item.UserId == userId || (familyId.HasValue && item.FamilyId == familyId)) && !item.IsFullyConsumed)
                .GroupBy(item => item.Product.Category ?? ProductCategory.Other)
                .Select(group => new { Category = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            var totalCount = categoryCounts.Sum(c => c.Count);

            // ProductCategory.Other is never ranked for a slice of its own, no matter how large
            // its count is - it is pulled out here and folded straight into OtherCount below,
            // alongside the top-N overflow, whether it got there as an explicit category or via
            // the null-Category fold in the GroupBy key above. See
            // InventoryCompositionResponse.OtherCount's XML doc for the full reasoning: without
            // this, a response could carry both a Slices entry for Other AND a non-zero
            // OtherCount - two different things both claiming to mean "other".
            var explicitOtherCount = categoryCounts.FirstOrDefault(c => c.Category == ProductCategory.Other)?.Count ?? 0;

            // Only this small, already-aggregated list (at most one row per distinct non-Other
            // category the caller actually uses) is ever materialised - ordering/Take/Skip below
            // run over that, never over the underlying inventory rows.
            var ordered = categoryCounts
                .Where(c => c.Category != ProductCategory.Other)
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Category)
                .ToList();

            // OtherCount is the single "not individually listed" bucket: the explicit/null-folded
            // Other category plus every non-Other category ranked past MaxSlices. Slices' summed
            // Count plus this always equals totalCount - see InsightsControllerTests for the
            // assertions that prove it, including the case where Other alone would otherwise have
            // out-ranked every top-8 slice.
            var otherCount = explicitOtherCount + ordered.Skip(MaxSlices).Sum(c => c.Count);

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
