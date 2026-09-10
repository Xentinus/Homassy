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

        /// <summary>
        /// Cache key prefix for <see cref="GetSpendByLocationAsync"/> within a family's
        /// <see cref="FamilyInsightsCache"/> entries - endpoint-qualified for the same reason
        /// <see cref="CompositionCacheKey"/> is (see its remarks): the cache stores values as
        /// <see cref="object"/> and casts to whatever <c>T</c> the caller asks for, so sharing a
        /// key with another endpoint would surface as a runtime <see cref="InvalidCastException"/>
        /// on a hit rather than a compile error.
        ///
        /// <para>
        /// The key actually used is <c>$"{SpendByLocationCacheKey}:{days}:u{userId}"</c> - always
        /// both the window length in days and the acting user id, even when
        /// the caller has a family. This matches <see cref="CompositionCacheKey"/>, not
        /// <see cref="ConsumptionCacheKey"/>: <see cref="ComputeConsumptionSeriesAsync"/> can drop
        /// the user id for a family caller only because it scopes that caller to
        /// <c>Activity.FamilyId == familyId</c> alone - genuinely family-wide, since every
        /// activity is stamped with the session family id unconditionally (see that method's
        /// remarks). <see cref="ComputeSpendByLocationAsync"/> instead scopes through
        /// <c>ProductInventoryItem</c> with the same union
        /// <c>ProductFunctions.GetInventoryItemsByUserAndFamily</c> and
        /// <see cref="ComputeInventoryCompositionAsync"/> use -
        /// <c>item.UserId == userId || (familyId.HasValue &amp;&amp; item.FamilyId == familyId)</c> -
        /// so a family caller's own personal purchases are part of their result and no other
        /// member's. Two members of the same family therefore do not share one answer, so the
        /// user id has to be in the key regardless of family, exactly like
        /// <see cref="CompositionCacheKey"/> - otherwise whichever member's request misses the
        /// cache first would have their own personal spend served to every other member for the
        /// rest of the TTL.
        /// </para>
        /// </summary>
        private const string SpendByLocationCacheKey = "spend-by-location";

        private static readonly TimeSpan SpendByLocationTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The label <see cref="LocationSpend.LocationName"/> carries for the bucket that folds
        /// every purchase whose <c>ShoppingLocationId</c> is <see langword="null"/> - see
        /// <see cref="ComputeSpendByLocationAsync"/>'s remarks for why that purchase is folded
        /// here rather than dropped, and reused as the fallback for a non-null id this method
        /// cannot resolve to a location row (e.g. deleted after the purchase was made).
        /// </summary>
        private const string UnknownLocationName = "Unknown location";

        /// <summary>
        /// The caller's purchases within the last <paramref name="days"/> days, broken down by
        /// shopping location and, within each location, by currency. Cached for
        /// <see cref="SpendByLocationTtl"/> under <see cref="SpendByLocationCacheKey"/> - see that
        /// constant's remarks for exactly how the key is built and why it always carries the user
        /// id. See <see cref="ComputeSpendByLocationAsync"/> for the scope rule and the three data
        /// rules (no currency conversion, the unknown-location fold, the null-price fold) this
        /// endpoint exists to get right.
        /// </summary>
        /// <param name="userId">
        /// The acting user. Required for the same reason as on
        /// <see cref="GetInventoryCompositionAsync"/> - <see cref="Controllers.InsightsController"/>
        /// short-circuits to 401 before this is ever called for a caller with no resolvable user
        /// id - and used here as half of the scope union regardless of whether
        /// <paramref name="familyId"/> is present.
        /// </param>
        /// <param name="familyId">
        /// The caller's family, or <see langword="null"/> if they have none. Exactly like
        /// <see cref="GetInventoryCompositionAsync"/>, a missing family id only drops the family
        /// half of the union in <see cref="ComputeSpendByLocationAsync"/> - it never short-circuits
        /// the result, since a family-less caller still has their own personal purchases to show.
        /// </param>
        /// <param name="days">
        /// The window length in days. Bounds (30 or 90) are the controller's job to enforce, not
        /// this method's - see <see cref="Controllers.InsightsController"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation for this call's own attempt to (re)compute the value - see
        /// <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/> for why a piggybacking caller's own
        /// token can never cancel a computation it did not win the race to start.
        /// </param>
        public Task<SpendByLocationResponse> GetSpendByLocationAsync(int userId, int? familyId, int days, CancellationToken cancellationToken)
        {
            var key = $"{SpendByLocationCacheKey}:{days}:u{userId}";

            return _cache.GetOrAddAsync(
                familyId ?? 0,
                key,
                SpendByLocationTtl,
                ct => ComputeSpendByLocationAsync(userId, familyId, days, ct),
                cancellationToken);
        }

        /// <summary>
        /// Runs the actual aggregation on a cache miss.
        ///
        /// <para>
        /// <b>Scope.</b> <c>Entities.Product.ProductPurchaseInfo</c> carries no <c>FamilyId</c> or
        /// <c>UserId</c> of its own - it hangs off <c>Entities.Product.ProductInventoryItem</c> via
        /// <c>ProductInventoryItemId</c>, so it is scoped through that item, exactly the way
        /// <see cref="ComputeInventoryCompositionAsync"/> scopes inventory itself:
        /// <c>item.UserId == userId || (familyId.HasValue &amp;&amp; item.FamilyId == familyId)</c> -
        /// the same union <c>ProductFunctions.GetInventoryItemsByUserAndFamily</c> uses everywhere
        /// else in the codebase. This is deliberately the union, not
        /// <see cref="ComputeConsumptionSeriesAsync"/>'s either/or on
        /// <c>Activity.FamilyId</c>/<c>UserId</c>: that either/or is correct there only because
        /// every <c>ProductInventoryDecrease</c> activity is stamped with the acting user's
        /// session family id unconditionally, so a family member's own personal-item consumption
        /// already carries the family id and needs no union. A purchase carries no such stamp - it
        /// is only ever reachable through the inventory item it purchased, and that item's own
        /// <c>FamilyId</c> is genuinely <see langword="null"/> for a personal item even when its
        /// owner has a family. So purchases follow inventory's scoping rule, not consumption's - a
        /// family member's personal purchases must still show up in their own spend, exactly as
        /// their personal items still show up in their own composition.
        /// </para>
        ///
        /// <para>
        /// <b>LINQ over <see cref="HomassyDbContext.ProductPurchaseInfos"/>, not raw SQL.</b> Both
        /// <c>ProductPurchaseInfo</c> and <c>ProductInventoryItem</c> are soft-deletable (see
        /// <see cref="HomassyDbContext.OnModelCreating"/>'s global filter), so a hand-written
        /// command would silently drop <c>NOT "IsDeleted"</c> on both sides of the join - the exact
        /// defect Task 8 shipped and had to be rewritten for. Plain LINQ over the <c>DbSet</c>
        /// picks the filter up on both sides for free, with no extra code written for it.
        /// </para>
        ///
        /// <para>
        /// <b>Grouping happens in SQL, and it is what makes the two null-handling rules work.</b>
        /// Rows are grouped by <c>(ShoppingLocationId, Currency)</c> - a nullable pair, so
        /// PostgreSQL's own <c>GROUP BY</c> already collects every null-location purchase into a
        /// real, counted group instead of dropping it. Per group, <c>ItemCount</c> is
        /// <c>g.Count()</c> - every purchase in the group, unconditionally. <c>PricedCount</c> is
        /// <c>g.Count(p =&gt; p.Price != null)</c> and is the actual signal the fold below uses to
        /// decide whether the group has anything to report - <b>not</b> whether <c>Spend</c> has a
        /// value: EF Core translates <c>g.Sum(p =&gt; (decimal?)p.Price)</c> to
        /// <c>COALESCE(sum(p."Price"::numeric), 0.0)</c> (verified against the pinned provider DLL
        /// via the captured SQL in this task's report), matching plain .NET <c>Enumerable.Sum</c>'s
        /// own behaviour of returning <c>0</c>, never <see langword="null"/>, when there is nothing
        /// to sum - so a group whose every purchase has a <see langword="null"/> <c>Price</c> comes
        /// back with <c>Spend == 0m</c>, indistinguishable by nullability alone from a genuine
        /// zero-cost purchase. <c>PricedCount</c> is what actually distinguishes "nothing to sum"
        /// (0) from "summed to zero" (&gt;0): the fold below adds a
        /// <see cref="LocationSpend.SpendByCurrency"/> entry only when <c>PricedCount &gt; 0</c>, so
        /// a null-price purchase is counted in <see cref="LocationSpend.ItemCount"/> but never turns
        /// into a zero-value currency entry. Only this small, already-aggregated result (at most
        /// locations × currencies rows) is ever materialised - the fold into one
        /// <see cref="LocationSpend"/> per location happens afterwards, in memory, never over the
        /// caller's raw purchase rows.
        /// </para>
        ///
        /// <para>
        /// <b>Currency is not a reliable proxy for "has a price," either.</b>
        /// <c>ProductFunctions.CreateInventoryItemAsync</c> defaults a purchase's <c>Currency</c>
        /// to the user's own saved <c>Entities.User.UserProfile.DefaultCurrency</c> whenever the
        /// request does not specify one - regardless of whether <c>Price</c> was supplied - so a
        /// <see langword="null"/>-<c>Price</c> purchase routinely still carries a real, non-null
        /// <c>Currency</c>. Folding a currency entry whenever a group's <c>Currency</c> is non-null
        /// (instead of checking <c>PricedCount</c>, as above) would therefore add a phantom
        /// zero-spend entry for most real null-price purchases, rather than correctly omitting
        /// them - this is exactly the bug an earlier version of this method shipped (it checked
        /// <c>Spend.HasValue</c>, which - per the <c>COALESCE</c> translation above - is
        /// <see langword="true"/> even for an all-null-price group) until this task's own
        /// <c>InsightsControllerTests.GetSpendByLocation_NullPrice_CountsTowardItemCountButNotSpend</c>
        /// caught the resulting phantom <c>{Huf: 0}</c> entry.
        /// </para>
        ///
        /// <para>
        /// <b>Location names and public ids (Fix round 1).</b> Both are resolved together with
        /// one small follow-up query against <see cref="HomassyDbContext.ShoppingLocations"/>,
        /// keyed by the distinct non-null location ids the aggregation actually returned - never
        /// per-row inside the aggregation, and never one query per location.
        /// <see cref="LocationSpend.ShoppingLocationPublicId"/> is exposed instead of the
        /// internal <c>ShoppingLocationId</c> this method groups and looks up by, matching this
        /// codebase's convention of never handing out an enumerable primary key from a public DTO
        /// (see the "BaseEntity" section of <c>Homassy.API/Entities/CLAUDE.md</c>) - it comes from this same bounded lookup, not a
        /// second query, so resolving it costs nothing beyond the one extra column already
        /// selected here, rather than pulling location rows into memory afterwards, which would
        /// trade the convention fix for an N+1. A location id this lookup cannot resolve (e.g.
        /// soft-deleted after the purchase was made) falls back to <see cref="UnknownLocationName"/>
        /// for the name and <see langword="null"/> for the public id - the same pair of
        /// fallbacks the null-<c>ShoppingLocationId</c> bucket itself uses, since there being no
        /// location row means there is no public id to carry either.
        /// </para>
        /// </summary>
        private async Task<SpendByLocationResponse> ComputeSpendByLocationAsync(int userId, int? familyId, int days, CancellationToken cancellationToken)
        {
            using var context = _contextFactory.CreateForReading();

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-days);

            // Non-deleted is enforced by the global soft-delete query filter on both
            // ProductPurchaseInfo and ProductInventoryItem (see HomassyDbContext.OnModelCreating),
            // not by an explicit IsDeleted check here - see this method's "LINQ, not raw SQL"
            // remarks above. GroupBy and the per-group Count/Sum all happen in SQL; only the
            // resulting (at most locations x currencies) rows are pulled into memory.
            var rows = await context.ProductPurchaseInfos
                .Where(p => p.PurchasedAt >= fromUtc && p.PurchasedAt <= toUtc &&
                            (p.ProductInventoryItem.UserId == userId || (familyId.HasValue && p.ProductInventoryItem.FamilyId == familyId)))
                .GroupBy(p => new { p.ShoppingLocationId, p.Currency })
                .Select(g => new
                {
                    g.Key.ShoppingLocationId,
                    g.Key.Currency,
                    ItemCount = g.Count(),
                    // The actual "does this group have anything to report" signal - see this
                    // method's remarks on why Spend's own nullability cannot be used for that.
                    PricedCount = g.Count(p => p.Price != null),
                    // Cast inside the Sum, not after: keeps this a decimal aggregation today, so
                    // Task 11's migration of Price to decimal? only removes the cast rather than
                    // changing the shape of this query. EF Core translates this to
                    // COALESCE(sum(...), 0.0) - see this method's remarks for why that means
                    // PricedCount, not Spend's nullability, is what the fold below must check.
                    Spend = g.Sum(p => (decimal?)p.Price)
                })
                .ToListAsync(cancellationToken);

            var locationIds = rows
                .Where(r => r.ShoppingLocationId.HasValue)
                .Select(r => r.ShoppingLocationId!.Value)
                .Distinct()
                .ToList();

            // One bounded follow-up lookup (distinct locations actually referenced), never a
            // per-row query - see this method's "Location names and public ids" remarks above.
            // Selects PublicId alongside Name in the same query, so LocationSpend's public id
            // costs nothing beyond one extra projected column - never a second round trip, and
            // never a per-row lookup.
            var locationInfoById = locationIds.Count > 0
                ? await context.ShoppingLocations
                    .Where(l => locationIds.Contains(l.Id))
                    .Select(l => new { l.Id, l.PublicId, l.Name })
                    .ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Name), cancellationToken)
                : new Dictionary<int, (Guid PublicId, string Name)>();

            // Re-grouping this already-small, already-aggregated row set by ShoppingLocationId
            // alone (never touching the database again) is the "fold the per-currency rows into
            // the response shape afterwards" step: each location's ItemCount sums every currency
            // sub-group's count, while SpendByCurrency only ever picks up sub-groups that actually
            // have a priced purchase (PricedCount > 0) - see this method's remarks above for why
            // Spend's own nullability cannot be used for that check. ShoppingLocationPublicId and
            // LocationName both fold from the same locationInfoById lookup - null and
            // UnknownLocationName for the null-ShoppingLocationId bucket (no location row exists
            // to resolve at all) and, defensively, for a non-null id the lookup above could not
            // resolve.
            var locations = rows
                .GroupBy(r => r.ShoppingLocationId)
                .Select(g =>
                {
                    (Guid PublicId, string Name)? resolved = g.Key.HasValue && locationInfoById.TryGetValue(g.Key.Value, out var info)
                        ? info
                        : null;

                    return new LocationSpend
                    {
                        ShoppingLocationPublicId = resolved?.PublicId,
                        LocationName = resolved?.Name ?? UnknownLocationName,
                        ItemCount = g.Sum(r => r.ItemCount),
                        SpendByCurrency = g
                            .Where(r => r.Currency.HasValue && r.PricedCount > 0)
                            .ToDictionary(r => r.Currency!.Value, r => r.Spend ?? 0m)
                    };
                })
                .OrderByDescending(l => l.ItemCount)
                .ThenBy(l => l.LocationName, StringComparer.Ordinal)
                .ToList();

            return new SpendByLocationResponse
            {
                Locations = locations
            };
        }

        /// <summary>
        /// Cache key prefix for <see cref="GetFamilyScoreboardAsync"/> - endpoint-qualified for the
        /// same reason every other insight key is (see <see cref="CompositionCacheKey"/>).
        ///
        /// <para>
        /// The key actually used is <c>$"{ScoreboardCacheKey}:{days}:{ianaTimeZoneId}"</c>, with
        /// <b>no user id</b> - and unlike <see cref="SpendByLocationCacheKey"/> that is correct here
        /// rather than a leak. This result is genuinely family-wide: every counter comes from
        /// <c>Activity.FamilyId == familyId</c>, both streaks are household-wide, and every member
        /// of the family appears in it, so two members of one family are entitled to identical
        /// answers. The timezone is in the key because it decides the day bucketing the streaks are
        /// scanned on (and the reported period bounds), so two members in different zones must not
        /// share an entry.
        /// </para>
        /// </summary>
        private const string ScoreboardCacheKey = "scoreboard";

        /// <summary>
        /// 15 minutes, not the nightly refresh the issue text suggested - see the spec's deviation
        /// note. A leaderboard that only moves overnight reads as broken (a member adds five items
        /// and their own number does not change), and this endpoint is opened deliberately rather
        /// than on every page load, so a shorter TTL costs little. Task 21's nightly pre-warm sits
        /// on top of this rather than replacing it.
        /// </summary>
        private static readonly TimeSpan ScoreboardTtl = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The activity types behind <see cref="MemberScore"/>'s three counters, listed once so the
        /// single grouped query filters on exactly the set the fold knows how to bucket - a type in
        /// the query but not in the fold would be fetched and silently dropped.
        /// </summary>
        private static readonly ActivityType[] CountedActivityTypes =
        [
            ActivityType.ProductInventoryCreate,
            ActivityType.ProductInventoryDecrease,
            ActivityType.ShoppingListItemPurchase,
            ActivityType.ShoppingListItemQuickPurchase
        ];

        /// <summary>
        /// One row of the single grouped counter query: how many activities of one type one member
        /// has in one of the two windows.
        /// </summary>
        private sealed record ActivityCountRow(int UserId, ActivityType ActivityType, bool IsCurrentPeriod, int Count);

        /// <summary>
        /// The family's scoreboard for the last <paramref name="days"/> days: per-member counters
        /// with a comparison against the previous equally-long window, plus the household's
        /// no-expiry and list-cleared streaks. Cached for <see cref="ScoreboardTtl"/> under
        /// <see cref="ScoreboardCacheKey"/>.
        /// </summary>
        /// <param name="userId">
        /// The acting user - used only to resolve the timezone the streaks and the period bounds are
        /// computed in, from their own saved profile. It deliberately does <b>not</b> scope the
        /// result: a scoreboard is the same for everyone in the family.
        /// </param>
        /// <param name="familyId">
        /// The family to report on. <see langword="null"/> short-circuits to an empty scoreboard
        /// <b>without querying or caching anything</b> - a caller with no family has no household to
        /// rank, which is a legitimate empty answer and not an error.
        /// </param>
        /// <param name="days">The window length. Bounds are the controller's job to enforce.</param>
        /// <param name="cancellationToken">Cancellation for this call's own (re)computation.</param>
        public Task<FamilyScoreboardResponse> GetFamilyScoreboardAsync(int userId, int? familyId, int days, CancellationToken cancellationToken)
        {
            // Resolved up front, exactly as GetConsumptionSeriesAsync does it: the zone has to be
            // part of the cache key, and the profile lookup is a cheap cache-backed read.
            var userTimeZone = new UserFunctions(_contextFactory).GetUserProfileByUserId(userId)?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;
            var ianaTimeZoneId = ResolveIanaTimeZoneId(userTimeZone.ToTimeZoneId());

            if (!familyId.HasValue)
            {
                return Task.FromResult(EmptyScoreboard(days, ianaTimeZoneId));
            }

            var key = $"{ScoreboardCacheKey}:{days}:{ianaTimeZoneId}";

            return _cache.GetOrAddAsync(
                familyId.Value,
                key,
                ScoreboardTtl,
                ct => ComputeFamilyScoreboardAsync(familyId.Value, days, ianaTimeZoneId, ct),
                cancellationToken);
        }

        /// <summary>
        /// The answer for a caller with no family: no members and no streaks, but the same truthful
        /// period bounds every other answer carries, so a client can render "nothing yet" without
        /// special-casing a payload that has no window at all.
        /// </summary>
        private static FamilyScoreboardResponse EmptyScoreboard(int days, string ianaTimeZoneId)
        {
            var (periodStart, periodEnd) = LocalPeriodBounds(days, ianaTimeZoneId);

            return new FamilyScoreboardResponse
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };
        }

        /// <summary>
        /// The window as calendar days on the caller's own clock: it ends on their local today and
        /// spans <paramref name="days"/> days inclusive, so a 30-day window is 30 dated days rather
        /// than 29 plus a fraction.
        /// </summary>
        private static (DateOnly PeriodStart, DateOnly PeriodEnd) LocalPeriodBounds(int days, string ianaTimeZoneId)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

            return (localToday.AddDays(-(days - 1)), localToday);
        }

        /// <summary>
        /// Runs the actual aggregation on a cache miss - five queries, none of them per member and
        /// none of them per counter: the family's members; <b>one</b> grouped pass over
        /// <c>Activities</c> covering every counter <em>and</em> both periods (grouped by
        /// <c>(UserId, ActivityType, is-in-current-window)</c>, so the previous period costs nothing
        /// beyond a wider <c>WHERE</c>); the consumption logs behind
        /// <see cref="MemberScore.WasteAvoided"/>; and one query per streak's day set.
        ///
        /// <para>
        /// <b>Scope.</b> Counters read <c>Activity.FamilyId == familyId</c> alone, for the reason
        /// <see cref="ComputeConsumptionSeriesAsync"/> documents at length: every activity is stamped
        /// with the acting user's session family id unconditionally, so that predicate already covers
        /// a member's personal-item actions and needs no union. The two <em>inventory</em> queries do
        /// need the union (<c>item.FamilyId == familyId</c> or the item belongs to one of the
        /// members), because a personal item's own <c>FamilyId</c> really is <see langword="null"/> -
        /// the same rule <see cref="ComputeSpendByLocationAsync"/> applies to one caller, applied here
        /// to the family's members.
        /// </para>
        ///
        /// <para>
        /// <b>Both streaks are scanned over the window only</b>, so <c>Longest</c> means "the longest
        /// run inside this window", never "the longest ever". That keeps these queries bounded like
        /// every other query in this class; an all-time longest is a different question, over the
        /// family's whole history, and would need its own (much more cacheable) endpoint.
        /// </para>
        /// </summary>
        private async Task<FamilyScoreboardResponse> ComputeFamilyScoreboardAsync(int familyId, int days, string ianaTimeZoneId, CancellationToken cancellationToken)
        {
            using var context = _contextFactory.CreateForReading();

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-days);
            var previousFromUtc = fromUtc.AddDays(-days);
            var (periodStart, periodEnd) = LocalPeriodBounds(days, ianaTimeZoneId);

            // Every member of the family, whether or not they did anything in the window - see
            // FamilyScoreboardResponse.Members. UserProfile is optional on User, so the display
            // name falls back to the User row's own name rather than to an empty string.
            var members = await context.Users
                .Where(u => u.FamilyId == familyId)
                .Select(u => new
                {
                    u.Id,
                    u.PublicId,
                    u.Name,
                    ProfileDisplayName = u.Profile != null ? u.Profile.DisplayName : null,
                    IdentityColor = u.Profile != null ? u.Profile.IdentityColor : null
                })
                .ToListAsync(cancellationToken);

            var memberIds = members.Select(member => member.Id).ToList();

            // Every counter, for both windows, in one grouped query. The third grouping key is
            // which window the row falls in - a plain boolean comparison PostgreSQL evaluates in
            // the GROUP BY - so "this period" and "the one before" come back as separate rows of
            // one result rather than as a second round trip.
            var counterRows = await context.Activities
                .Where(a => a.FamilyId == familyId
                            && a.Timestamp >= previousFromUtc && a.Timestamp <= toUtc
                            && CountedActivityTypes.Contains(a.ActivityType))
                .GroupBy(a => new { a.UserId, a.ActivityType, IsCurrentPeriod = a.Timestamp >= fromUtc })
                .Select(g => new ActivityCountRow(g.Key.UserId, g.Key.ActivityType, g.Key.IsCurrentPeriod, g.Count()))
                .ToListAsync(cancellationToken);

            // Waste avoided: items finished on or before their own expiration date, inside the
            // window, attributed to whoever actually finished them. The consumption logs carry the
            // only per-member attribution that exists (the inventory item itself records no
            // consumer), and the winning log per item is the last one - the consume that took it to
            // zero. Folded in memory rather than with a correlated max-per-item subquery: the row
            // count is bounded by one family's consumption inside the window.
            var wasteLogs = await context.ProductConsumptionLogs
                .Where(l => l.ProductInventoryItem.IsFullyConsumed
                            && l.ProductInventoryItem.FullyConsumedAt != null
                            && l.ProductInventoryItem.ExpirationAt != null
                            && l.ProductInventoryItem.FullyConsumedAt <= l.ProductInventoryItem.ExpirationAt
                            && l.ProductInventoryItem.FullyConsumedAt >= fromUtc
                            && l.ProductInventoryItem.FullyConsumedAt <= toUtc
                            && (l.ProductInventoryItem.FamilyId == familyId
                                || (l.ProductInventoryItem.UserId != null && memberIds.Contains(l.ProductInventoryItem.UserId.Value))))
                .Select(l => new { l.ProductInventoryItemId, l.UserId, l.ConsumedAt })
                .ToListAsync(cancellationToken);

            var wasteAvoidedByUser = wasteLogs
                .GroupBy(log => log.ProductInventoryItemId)
                .Select(logsForItem => logsForItem.OrderByDescending(log => log.ConsumedAt).First())
                .Where(log => log.UserId.HasValue)
                .GroupBy(log => log.UserId!.Value)
                .ToDictionary(group => group.Key, group => group.Count());

            // The days something expired unused - the days that BREAK the no-expiry streak. An item
            // counts as expired here when it passed its own expiration date without being finished
            // first; one finished in time is exactly the waste-avoided case above.
            var expirationInstants = await context.ProductInventoryItems
                .Where(i => (i.FamilyId == familyId || (i.UserId != null && memberIds.Contains(i.UserId.Value)))
                            && i.ExpirationAt != null
                            && i.ExpirationAt >= fromUtc && i.ExpirationAt <= toUtc
                            && (!i.IsFullyConsumed || i.FullyConsumedAt == null || i.FullyConsumedAt > i.ExpirationAt))
                .Select(i => i.ExpirationAt!.Value)
                .ToListAsync(cancellationToken);

            // The days a shopping list was cleared. "Cleared" is read off the list's current state -
            // every item on it purchased - and dated by its last purchase. That is the only
            // definition this schema supports: there is no completion event, so a cleared list that
            // later gains a new item stops counting and its historical clearing is forgotten. Worth
            // knowing when reading this streak, and stated here rather than left to be discovered.
            var clearedInstants = await context.ShoppingLists
                .Where(l => l.FamilyId == familyId)
                .Where(l => l.Items!.Any() && l.Items!.All(i => i.PurchasedAt != null))
                .Select(l => l.Items!.Max(i => i.PurchasedAt))
                .ToListAsync(cancellationToken);

            var expiredDays = ToLocalDays(expirationInstants, timeZone);

            // The no-expiry streak's qualifying days are the INVERSE of the expiry days: every day
            // of the window on which nothing expired. A day with no inventory at all qualifies -
            // nothing expired on it, which is the literal claim the streak makes.
            var noExpiryDays = Enumerable
                .Range(0, days)
                .Select(offset => periodStart.AddDays(offset))
                .Where(day => day <= periodEnd && !expiredDays.Contains(day))
                .ToList();

            var clearedDays = ToLocalDays(
                clearedInstants
                    .Where(instant => instant.HasValue && instant.Value >= fromUtc && instant.Value <= toUtc)
                    .Select(instant => instant!.Value),
                timeZone);

            var noExpiryStreak = StreakCalculator.Compute(noExpiryDays, periodEnd);
            var listClearedStreak = StreakCalculator.Compute(clearedDays, periodEnd);

            var scores = members
                .Select(member =>
                {
                    var itemsAdded = CountFor(counterRows, member.Id, ActivityType.ProductInventoryCreate, current: true);
                    var itemsConsumed = CountFor(counterRows, member.Id, ActivityType.ProductInventoryDecrease, current: true);
                    var listItemsPurchased =
                        CountFor(counterRows, member.Id, ActivityType.ShoppingListItemPurchase, current: true)
                        + CountFor(counterRows, member.Id, ActivityType.ShoppingListItemQuickPurchase, current: true);

                    var previousPeriodTotal =
                        CountFor(counterRows, member.Id, ActivityType.ProductInventoryCreate, current: false)
                        + CountFor(counterRows, member.Id, ActivityType.ProductInventoryDecrease, current: false)
                        + CountFor(counterRows, member.Id, ActivityType.ShoppingListItemPurchase, current: false)
                        + CountFor(counterRows, member.Id, ActivityType.ShoppingListItemQuickPurchase, current: false);

                    return new MemberScore
                    {
                        PublicId = member.PublicId,
                        DisplayName = string.IsNullOrWhiteSpace(member.ProfileDisplayName) ? member.Name : member.ProfileDisplayName,
                        IdentityColor = member.IdentityColor,
                        ItemsAdded = itemsAdded,
                        ItemsConsumed = itemsConsumed,
                        ListItemsPurchased = listItemsPurchased,
                        WasteAvoided = wasteAvoidedByUser.TryGetValue(member.Id, out var wasteAvoided) ? wasteAvoided : 0,
                        CurrentPeriodTotal = itemsAdded + itemsConsumed + listItemsPurchased,
                        PreviousPeriodTotal = previousPeriodTotal
                    };
                })
                // Busiest first, then by name, so equal scores order stably instead of following
                // whatever order the database happened to return the members in.
                .OrderByDescending(score => score.CurrentPeriodTotal)
                .ThenBy(score => score.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new FamilyScoreboardResponse
            {
                Members = scores,
                NoExpiryStreak = new StreakInfo { Current = noExpiryStreak.Current, Longest = noExpiryStreak.Longest },
                ListClearedStreak = new StreakInfo { Current = listClearedStreak.Current, Longest = listClearedStreak.Longest },
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };
        }

        /// <summary>
        /// Turns UTC instants into the set of calendar days they fall on in
        /// <paramref name="timeZone"/>. Converted in .NET rather than in SQL on purpose: the row
        /// counts are already bounded by the window, and doing it here keeps the day arithmetic in
        /// the same place the streak scan reads it from. The kind is asserted explicitly because
        /// <see cref="TimeZoneInfo.ConvertTimeFromUtc"/> throws for a <see cref="DateTimeKind.Local"/>
        /// input and a materialised value's kind depends on the provider.
        /// </summary>
        private static HashSet<DateOnly> ToLocalDays(IEnumerable<DateTime> instantsUtc, TimeZoneInfo timeZone) =>
            instantsUtc
                .Select(instant => DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(instant, DateTimeKind.Utc), timeZone)))
                .ToHashSet();

        /// <summary>
        /// One counter out of the single grouped result: this member's rows of this type in this
        /// window, or zero when they have none - which is the common case, and has to be a zero
        /// rather than an absent member.
        /// </summary>
        private static int CountFor(IEnumerable<ActivityCountRow> rows, int userId, ActivityType activityType, bool current) =>
            rows.FirstOrDefault(row => row.UserId == userId && row.ActivityType == activityType && row.IsCurrentPeriod == current)?.Count ?? 0;
    }
}
