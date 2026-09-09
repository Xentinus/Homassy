using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Extensions;
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

        /// <summary>
        /// Cache key prefix for <see cref="GetConsumptionSeriesAsync"/> within a family's
        /// <see cref="FamilyInsightsCache"/> entries - endpoint-qualified for the same reason
        /// <see cref="CompositionCacheKey"/> is (see its remarks): the cache stores values as
        /// <see cref="object"/> and casts to whatever <c>T</c> the caller asks for, so sharing a
        /// key with another endpoint would surface as a runtime <see cref="InvalidCastException"/>
        /// on a hit rather than a compile error.
        ///
        /// <para>
        /// <b>This key is built differently depending on scope, unlike <see cref="CompositionCacheKey"/>.</b>
        /// Every input the result actually depends on has to be in the key - <c>days</c>,
        /// <c>bucket</c> and the resolved timezone id always are, since changing any one of them
        /// changes the answer for the exact same caller. Whether the <em>user</em> id also has to
        /// be in it depends on which branch of <see cref="GetConsumptionSeriesAsync"/>'s scope ran:
        /// </para>
        /// <list type="bullet">
        /// <item>A caller <b>with</b> a family is scoped to <c>Activity.FamilyId == family</c>
        /// alone (see <see cref="ComputeConsumptionSeriesAsync"/>) - genuinely family-wide, the
        /// same answer for every current member asking with the same <c>days</c>/<c>bucket</c>/
        /// timezone. Folding the user id into the key here would give each member their own
        /// identical copy of the same family aggregate instead of one shared entry - still
        /// correct, just pointless, recomputing (and separately expiring) the same query once per
        /// member instead of once per family.</item>
        /// <item>A caller <b>without</b> a family is scoped to <c>Activity.UserId == caller</c> -
        /// genuinely per-user, so the key <em>must</em> carry <c>:u{userId}</c>, exactly like
        /// <see cref="CompositionCacheKey"/> always does. Both branches still partition under
        /// <c>familyId ?? 0</c> in <see cref="FamilyInsightsCache"/>, so every family-less caller
        /// collapses onto the same partition 0 - the per-user suffix is what keeps them from
        /// reading each other's series there, the same reasoning <see cref="CompositionCacheKey"/>
        /// documents for that collapse.</item>
        /// </list>
        /// </summary>
        private const string ConsumptionCacheKey = "consumption";

        private static readonly TimeSpan ConsumptionTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// A dense consumption time series - one point per <paramref name="bucket"/>-sized step
        /// across the last <paramref name="days"/> days, bucketed in the caller's own saved
        /// timezone. Cached under <see cref="ConsumptionCacheKey"/> for <see cref="ConsumptionTtl"/> -
        /// see that constant's remarks for exactly how the cache key is built and why it differs
        /// by scope.
        /// </summary>
        /// <param name="userId">
        /// The acting user. Required for the same reason it is on
        /// <see cref="GetInventoryCompositionAsync"/> - <see cref="Controllers.InsightsController"/>
        /// short-circuits to 401 before this is ever called for a caller with no resolvable user
        /// id - and additionally used here as the scope itself when <paramref name="familyId"/> is
        /// <see langword="null"/> (see <see cref="ComputeConsumptionSeriesAsync"/>).
        /// </param>
        /// <param name="familyId">
        /// The caller's family, or <see langword="null"/> if they have none. Unlike
        /// <see cref="GetInventoryCompositionAsync"/> - where a missing family only drops half of
        /// a union - this changes which column the query scopes on entirely: see
        /// <see cref="ComputeConsumptionSeriesAsync"/>'s remarks for why "the whole family's
        /// consumption" and "just this caller's own" are the two right answers, not a union of the
        /// two.
        /// </param>
        /// <param name="days">
        /// The window length in days. Bounds (30 or 90) are the controller's job to enforce, not
        /// this method's - see <see cref="Controllers.InsightsController"/>.
        /// </param>
        /// <param name="bucket">Day or week granularity - see <see cref="SeriesBucket"/>.</param>
        /// <param name="cancellationToken">
        /// Cancellation for this call's own attempt to (re)compute the value - see
        /// <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/> for why a piggybacking caller's own
        /// token can never cancel a computation it did not win the race to start.
        /// </param>
        public Task<ConsumptionSeriesResponse> GetConsumptionSeriesAsync(int userId, int? familyId, int days, SeriesBucket bucket, CancellationToken cancellationToken)
        {
            // Resolved once, up front, rather than inside the cache factory: it has to be part of
            // the key itself (see ConsumptionCacheKey's remarks), and GetUserProfileByUserId is a
            // cheap, cache-backed lookup (see UserFunctions), not a query worth deferring behind
            // the cache's single-flight factory.
            var userTimeZone = new UserFunctions(_contextFactory).GetUserProfileByUserId(userId)?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;
            var ianaTimeZoneId = ResolveIanaTimeZoneId(userTimeZone.ToTimeZoneId());

            var key = familyId.HasValue
                ? $"{ConsumptionCacheKey}:{days}:{bucket}:{ianaTimeZoneId}"
                : $"{ConsumptionCacheKey}:{days}:{bucket}:{ianaTimeZoneId}:u{userId}";

            return _cache.GetOrAddAsync(
                familyId ?? 0,
                key,
                ConsumptionTtl,
                ct => ComputeConsumptionSeriesAsync(userId, familyId, days, bucket, ianaTimeZoneId, ct),
                cancellationToken);
        }

        /// <summary>
        /// Validates <paramref name="ianaTimeZoneId"/> against the runtime's own timezone database
        /// and falls back to <c>"UTC"</c> instead of throwing when it is not recognised, rather
        /// than let an unresolvable id reach the raw SQL in <see cref="ComputeConsumptionSeriesAsync"/>
        /// and fail the whole request over what is, worst case, a cosmetic mislabelling of which
        /// calendar day a bucket belongs to. <see cref="UserTimeZoneExtensions.ToTimeZoneId"/>
        /// already defaults an unmapped <see cref="UserTimeZone"/> enum value to Budapest, so in
        /// practice this only ever catches a genuinely foreign string reaching this method some
        /// other way - but "genuinely foreign string" is exactly the case a raw SQL parameter must
        /// never trust blindly.
        /// </summary>
        private static string ResolveIanaTimeZoneId(string ianaTimeZoneId)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
                return ianaTimeZoneId;
            }
            catch (TimeZoneNotFoundException)
            {
                return "UTC";
            }
            catch (InvalidTimeZoneException)
            {
                return "UTC";
            }
        }

        /// <summary>
        /// Runs the actual aggregation on a cache miss.
        ///
        /// <para>
        /// <b>Scope.</b> A caller with a family sees <c>Activity.FamilyId == familyId</c> alone -
        /// every <see cref="ActivityType.ProductInventoryDecrease"/> recorded under that family,
        /// regardless of which member did the consuming or whether the specific item consumed was
        /// personal or family-shared. That is deliberate, not a simplification: every call site
        /// that records this activity type (see <c>ProductFunctions</c>'s consume/quick-consume/
        /// split paths) stamps it with the acting user's <em>session</em> family id
        /// unconditionally - so a family member's own personal-item consumption already carries
        /// the family id too, and "the whole family's consumption" is already exactly what
        /// <c>FamilyId == familyId</c> selects, with no need to also <c>OR</c> in
        /// <c>UserId == userId</c> the way <see cref="ComputeInventoryCompositionAsync"/> does for
        /// inventory (where the union matters because a personal item's <c>FamilyId</c> really is
        /// <see langword="null"/>). Unioning in <c>UserId</c> here would not add any rows a family
        /// member's activity doesn't already carry via <c>FamilyId</c> - it would only make the
        /// result <em>look</em> user-dependent, which is exactly the shape <see cref="ConsumptionCacheKey"/>'s
        /// remarks explain the cache key must avoid paying for. A caller with no family has no
        /// family id for any of their activity to carry, so they fall back to <c>UserId == userId</c> -
        /// "their own", the only scope that means anything for them.
        /// </para>
        ///
        /// <para>
        /// <b>Timezone and grouping (Fix round 1).</b> Bucketing happens in the caller's own local
        /// calendar, not the server's UTC: the grouping key is
        /// <c>TimeZoneInfo.ConvertTimeBySystemTimeZoneId(a.Timestamp, ianaTimeZoneId).Date</c> -
        /// Npgsql's own <c>NpgsqlDateTimeMethodTranslator</c> turns that into
        /// <c>date_trunc('day', "Timestamp" AT TIME ZONE @tz)</c>, the exact expression the previous
        /// raw-SQL version wrote by hand (verified with <c>ToQueryString()</c> against the pinned
        /// provider DLL - see this task's report for the captured SQL). Grouping is always by
        /// <b>day</b> here, never by week, regardless of <paramref name="bucket"/> -
        /// <see cref="SeriesZeroFill.Densify"/> is what folds surviving days into week buckets, and
        /// only after trimming to <c>[from, to]</c> (see its remarks on order of operations):
        /// grouping by week in SQL first, before that trim could run, is what previously let a
        /// window opening mid-week silently drop its own first partial week, and let a day one day
        /// past the window leak in under a week label that still happened to fall in range - both
        /// fixed by moving the week fold downstream of the trim instead of into the query.
        /// <c>Activities.Timestamp</c> is mapped <c>timestamp with time zone</c> (see the
        /// <c>AddActivityTracking</c> migration), so a single <c>AT TIME ZONE</c> hop is correct -
        /// it converts the stored instant straight to the target zone's local wall clock; a naive
        /// (without-time-zone) column would need converting through UTC first, or the same hop
        /// would silently reinterpret the naive value as if it were already local to the target
        /// zone and shift it the wrong way.
        /// </para>
        ///
        /// <para>
        /// <b>LINQ over <see cref="HomassyDbContext.Activities"/>, not raw SQL (Fix round 1).</b>
        /// The previous version reached for a hand-written <c>DbCommand</c> because this provider
        /// version (<c>Npgsql.EntityFrameworkCore.PostgreSQL</c> 10.0.0) has no
        /// <c>EF.Functions.DateTrunc</c> translation and no public <c>EF.Functions.AtTimeZone</c>
        /// either - both still true - but never checked whether the plain BCL
        /// <c>TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime, string)</c> is itself
        /// translatable, which it is (see above). Going raw bypassed EF's query pipeline entirely,
        /// which is exactly what silently dropped <see cref="HomassyDbContext.OnModelCreating"/>'s
        /// global soft-delete filter (<c>NOT "IsDeleted"</c> - the only global filter in this
        /// model): a hand-rolled <c>WHERE</c> clause has no way to pick that up, but an ordinary
        /// <c>DbSet&lt;Activity&gt;</c> query does, automatically, with no extra code written for
        /// it. This is plain LINQ, so it gets that filter back for free, and
        /// <c>Database.SqlQuery&lt;T&gt;</c>'s single-scalar-column limit - real, but only for raw
        /// SQL entry points - was never actually a constraint on the LINQ path used here. The
        /// <c>GROUP BY</c> and <c>SUM</c> still happen entirely in the database - only the small,
        /// already-aggregated (at most <paramref name="days"/> rows) result set is materialised
        /// into memory, same as every other query in this layer.
        /// </para>
        ///
        /// <para>
        /// <b>Window padding.</b> The <c>WHERE</c> clause bounds on UTC instants, but the caller's
        /// own local <c>[from, to]</c> window can extend up to ~14 hours past a naive UTC cut in
        /// either direction depending on their offset - so the UTC bounds are padded a full extra
        /// day on each side rather than risk excluding a row that is genuinely inside the local
        /// window before it ever reaches the <c>GROUP BY</c>. <see cref="SeriesZeroFill.Densify"/>
        /// trims anything still outside <c>[from, to]</c> by day, strictly before folding into a
        /// week (see its remarks on order of operations - this is exactly what makes the padding
        /// safe now that weeks are aggregated after trimming, not before): the padding can only
        /// ever over-fetch, never under-fetch or leak an out-of-window point into the response.
        /// </para>
        /// </summary>
        private async Task<ConsumptionSeriesResponse> ComputeConsumptionSeriesAsync(int userId, int? familyId, int days, SeriesBucket bucket, string ianaTimeZoneId, CancellationToken cancellationToken)
        {
            using var context = _contextFactory.CreateForReading();

            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId); // already resolved/validated by GetConsumptionSeriesAsync.
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);
            var toLocalDate = DateOnly.FromDateTime(nowLocal);
            var fromLocalDate = toLocalDate.AddDays(-(days - 1));

            // Padded a full extra day either side of the naive UTC cut - see this method's
            // "Window padding" remarks above. Both carry DateTimeKind.Utc (inherited from
            // DateTime.UtcNow, which AddDays preserves) - Npgsql requires that Kind on a value
            // bound to a `timestamptz` column such as Activities.Timestamp, or it throws rather
            // than guessing which zone an unspecified-Kind value is meant to be in.
            var fromUtc = DateTime.UtcNow.AddDays(-(days + 1));
            var toUtc = DateTime.UtcNow.AddDays(1);

            // Plain LINQ over context.Activities - an ordinary DbSet<Activity> query, so
            // HomassyDbContext's global soft-delete filter applies automatically (see this
            // method's "LINQ, not raw SQL" remarks above). Always grouped by DAY, regardless of
            // `bucket` - see SeriesZeroFill.Densify's remarks for why week aggregation happens
            // afterward, in C#, once the day-granular rows are trimmed to [from, to], never here.
            var scoped = familyId.HasValue
                ? context.Activities.Where(a => a.FamilyId == familyId)
                : context.Activities.Where(a => a.UserId == userId);

            var dayTotals = await scoped
                .Where(a => a.ActivityType == ActivityType.ProductInventoryDecrease && a.Timestamp >= fromUtc && a.Timestamp <= toUtc)
                .GroupBy(a => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(a.Timestamp, ianaTimeZoneId).Date)
                .Select(g => new { Bucket = g.Key, Value = g.Sum(a => a.Quantity ?? 0) })
                .ToListAsync(cancellationToken);

            // The DateOnly conversion happens here, client-side, after the GROUP BY/SUM have
            // already run in the database and only the (at most `days`) aggregated rows have been
            // materialised - never inside the query itself, which keeps the projection above
            // identical to the shape verified against the pinned provider DLL.
            var sparsePoints = dayTotals
                .Select(d => new SeriesPoint { Bucket = DateOnly.FromDateTime(d.Bucket), Value = d.Value })
                .ToList();

            var points = SeriesZeroFill.Densify(sparsePoints, fromLocalDate, toLocalDate, bucket);

            return new ConsumptionSeriesResponse
            {
                Points = points,
                Bucket = bucket,
                TimeZoneId = ianaTimeZoneId
            };
        }
    }
}
