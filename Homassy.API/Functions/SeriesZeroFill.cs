namespace Homassy.API.Functions
{
    /// <summary>
    /// The bucket granularity a consumption (or other R5 insight) time series is grouped by.
    /// <see cref="Week"/> is always Monday-anchored (ISO week), matching PostgreSQL's own
    /// <c>date_trunc('week', ...)</c> convention - even though the SQL side of
    /// <see cref="InsightFunctions.GetConsumptionSeriesAsync"/> itself only ever groups by day now
    /// (see <see cref="SeriesZeroFill.Densify"/>'s remarks on order of operations - Fix round 1).
    /// Week buckets are summed up here, in C#, from the day-granular rows the query returns, so
    /// <see cref="SeriesZeroFill.Densify"/>'s own Monday-snap is the one place left that has to
    /// keep agreeing with Postgres's ISO-week convention - and with the frontend's <c>timeTicks</c>,
    /// which aligns its weekly axis to Monday specifically to match.
    /// </summary>
    public enum SeriesBucket
    {
        Day,
        Week
    }

    /// <summary>
    /// One point of a bucketed time series: the calendar day the bucket starts on (for
    /// <see cref="SeriesBucket.Week"/>, always a Monday), and the aggregate value for that bucket.
    /// A bucket with no matching source rows still gets a point here (see
    /// <see cref="SeriesZeroFill.Densify"/>) - a chart draws a flat line through zero, not a gap.
    /// </summary>
    public record SeriesPoint
    {
        public DateOnly Bucket { get; init; }
        public decimal Value { get; init; }
    }

    /// <summary>
    /// Fills the gaps a SQL <c>GROUP BY</c> leaves behind: a bucket with no matching rows simply
    /// never appears in the aggregated result set, but a time-series chart needs one point per
    /// bucket in the requested window regardless, or its x-axis has holes where a flat zero line
    /// should be. This class turns that sparse, possibly-empty result into a complete, densely
    /// filled series covering exactly <c>[from, to]</c>.
    ///
    /// <para>
    /// <b>Pure and static on purpose.</b> No <c>DbContext</c>, no clock, nothing but its
    /// parameters - every one of the cases below is a plain input/output fact, provable without a
    /// database or a fixed "now". That is what lets <c>SeriesZeroFillTests</c> assert them
    /// directly, in-process, with no fixture at all.
    /// </para>
    /// </summary>
    public static class SeriesZeroFill
    {
        /// <summary>
        /// Produces one <see cref="SeriesPoint"/> per bucket in <c>[from, to]</c> (inclusive of
        /// both ends), summing whichever <paramref name="sparse"/> points fall into each bucket
        /// and defaulting to <see langword="0"/> for a bucket <paramref name="sparse"/> has no
        /// entry for.
        ///
        /// <para>
        /// <b>Fix round 1 - order of operations is fixed on purpose:</b> every <paramref name="sparse"/>
        /// point is trimmed against <c>[from, to]</c> <em>first</em>, while it is still a plain
        /// calendar day, and only a day that survives that trim is folded into a
        /// <see cref="SeriesBucket.Week"/> bucket afterward. Summing into weeks before trimming
        /// (the previous, buggy order) can only ever accept or reject a whole pre-summed week at
        /// once: a window that opens mid-week would then lose every genuine in-window day whose
        /// week's Monday falls before <paramref name="from"/> (silently zeroing real consumption on
        /// the first partial week), while a day that legitimately lies just past
        /// <paramref name="to"/> - such as one admitted by the deliberate UTC padding in
        /// <see cref="InsightFunctions.ComputeConsumptionSeriesAsync"/> - would ride inside a week
        /// bucket that still falls in range and leak straight through. Trimming by day first means
        /// only in-window days are ever summed, so a partial leading or trailing week is always
        /// exactly its in-window days - see <see cref="BuildBucketStarts"/>'s remarks for why that
        /// partial week is correct and expected, not a bug to "fix" later.
        /// </para>
        /// </summary>
        /// <param name="sparse">
        /// The aggregated (already-grouped-and-summed-in-SQL) result - always grouped by plain
        /// calendar day regardless of <paramref name="bucket"/> (see
        /// <see cref="InsightFunctions.ComputeConsumptionSeriesAsync"/>'s remarks for why the query
        /// itself never groups by week any more), in any order and with at most one entry per day
        /// in the normal case. Every point's own <see cref="SeriesPoint.Bucket"/> is checked against
        /// <c>[from, to]</c> <em>before</em> anything else - see this method's remarks on order of
        /// operations - and only then, for <see cref="SeriesBucket.Week"/>, snapped down to that
        /// day's Monday via <see cref="StartOfIsoWeek"/> and summed into the matching weekly point.
        /// </param>
        /// <param name="from">
        /// The first calendar day the returned series must cover. When <paramref name="bucket"/>
        /// is <see cref="SeriesBucket.Week"/>, the first returned point is that day's own Monday
        /// (which can fall before <paramref name="from"/> itself) - the point still represents the
        /// week <paramref name="from"/> belongs to, carrying only whichever of its days actually
        /// survive the <c>&gt;= from</c> trim.
        /// </param>
        /// <param name="to">
        /// The last calendar day the returned series must cover (inclusive). Must not precede
        /// <paramref name="from"/>; if it does, this returns an empty list rather than throwing,
        /// since an empty series is still a well-formed answer to a malformed range and this class
        /// has no caller context to report an error against.
        /// </param>
        /// <param name="bucket">The bucket granularity - see <see cref="SeriesBucket"/>.</param>
        /// <returns>
        /// A list ordered ascending by <see cref="SeriesPoint.Bucket"/> with no duplicate buckets -
        /// guaranteed structurally by how the bucket sequence itself is built below, not by a
        /// separate sort/distinct pass. A <paramref name="sparse"/> point whose
        /// <see cref="SeriesPoint.Bucket"/> falls outside <c>[from, to]</c> is dropped rather than
        /// extending the returned range - the caller asked for a window, not "the window plus
        /// whatever stray rows happened to be there".
        /// </returns>
        public static IReadOnlyList<SeriesPoint> Densify(IEnumerable<SeriesPoint> sparse, DateOnly from, DateOnly to, SeriesBucket bucket)
        {
            ArgumentNullException.ThrowIfNull(sparse);

            var bucketStarts = BuildBucketStarts(from, to, bucket);

            // Trim to [from, to] FIRST, while every point is still a plain calendar day, and only
            // THEN fold a surviving day into its ISO week for SeriesBucket.Week - see this
            // method's remarks above (Fix round 1) for why that order, and not the reverse, is
            // what keeps a partial leading/trailing week correct and keeps a day just past `to`
            // from leaking in under a week label that still happens to fall in range.
            var totals = new Dictionary<DateOnly, decimal>();
            foreach (var point in sparse)
            {
                if (point.Bucket < from || point.Bucket > to)
                {
                    continue; // outside the requested window - dropped, never widens it.
                }

                // however many sparse points land in the same target bucket (there should be at
                // most one per day, since the query groups by day, but several days can share a
                // week bucket here) their values are summed rather than one overwriting another.
                var targetBucket = bucket == SeriesBucket.Week ? StartOfIsoWeek(point.Bucket) : point.Bucket;
                totals[targetBucket] = totals.GetValueOrDefault(targetBucket) + point.Value;
            }

            return bucketStarts
                .Select(b => new SeriesPoint { Bucket = b, Value = totals.GetValueOrDefault(b) })
                .ToList();
        }

        /// <summary>
        /// The full, gap-free sequence of bucket-start days covering <c>[from, to]</c>: every
        /// calendar day for <see cref="SeriesBucket.Day"/>, or every Monday from <paramref
        /// name="from"/>'s week through <paramref name="to"/>'s week for
        /// <see cref="SeriesBucket.Week"/>. Ascending and duplicate-free by construction - each
        /// step strictly advances the cursor, so no separate ordering or de-duplication is needed
        /// downstream.
        ///
        /// <para>
        /// <b>A partial first (or last) week is correct and expected</b> when <paramref name="from"/>
        /// (or <paramref name="to"/>) does not fall on a Monday - do not "fix" this by shifting the
        /// label forward to the window's own start. If the window opens on, say, a Wednesday, that
        /// first bucket is still labelled with the Monday that starts its ISO week, even though only
        /// Wednesday-through-Sunday of it are actually in range: <see cref="Densify"/> only ever sums
        /// the in-window days into it (its own remarks explain why trimming happens before the
        /// week-fold), so the bucket's value is genuinely just those days, not a whole week's worth.
        /// Keeping the label on the real ISO Monday - rather than moving it to line up with
        /// <paramref name="from"/> - is what keeps every week bucket agreeing with PostgreSQL's own
        /// Monday-anchored <c>date_trunc('week', ...)</c> and with the frontend's <c>timeTicks</c>,
        /// which aligns its axis to Monday specifically to match.
        /// </para>
        /// </summary>
        private static List<DateOnly> BuildBucketStarts(DateOnly from, DateOnly to, SeriesBucket bucket)
        {
            var starts = new List<DateOnly>();
            if (to < from)
            {
                return starts; // malformed range - see Densify's remarks on `to`.
            }

            if (bucket == SeriesBucket.Week)
            {
                var cursor = StartOfIsoWeek(from);
                var lastWeekStart = StartOfIsoWeek(to);
                while (cursor <= lastWeekStart)
                {
                    starts.Add(cursor);
                    cursor = cursor.AddDays(7);
                }
            }
            else
            {
                var cursor = from;
                while (cursor <= to)
                {
                    starts.Add(cursor);
                    cursor = cursor.AddDays(1);
                }
            }

            return starts;
        }

        /// <summary>
        /// The Monday on or before <paramref name="date"/> - deliberately matching PostgreSQL's
        /// own <c>date_trunc('week', ...)</c>, which truncates to the ISO week (Monday-anchored),
        /// not a Sunday- or locale-anchored one. <see cref="DayOfWeek"/> numbers Sunday=0..
        /// Saturday=6; remapping so Monday=0..Sunday=6 before subtracting is what makes this land
        /// on the correct Monday for every day of the week, including Sunday itself (offset 6, so
        /// a Sunday steps back six days to the Monday that started its own week, not forward to
        /// the next one).
        /// </summary>
        private static DateOnly StartOfIsoWeek(DateOnly date)
        {
            var mondayIndexedDayOfWeek = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-mondayIndexedDayOfWeek);
        }
    }
}
