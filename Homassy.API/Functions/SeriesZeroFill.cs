namespace Homassy.API.Functions
{
    /// <summary>
    /// The bucket granularity a consumption (or other R5 insight) time series is grouped by.
    /// <see cref="Week"/> is always Monday-anchored (ISO week) to agree with PostgreSQL's own
    /// <c>date_trunc('week', ...)</c>, which is what the SQL side of
    /// <see cref="InsightFunctions.GetConsumptionSeriesAsync"/> actually groups by - see that
    /// method's remarks for why the two must never drift apart.
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
        /// </summary>
        /// <param name="sparse">
        /// The aggregated (already-grouped-and-summed-in-SQL) result, in any order and with at
        /// most one entry per bucket in the normal case - though a sparse point's own
        /// <see cref="SeriesPoint.Bucket"/> is treated as a plain calendar day even when
        /// <paramref name="bucket"/> is <see cref="SeriesBucket.Week"/>, and is snapped down to
        /// that day's Monday before being summed into the matching weekly point. That snap is
        /// normally a no-op (the SQL side already groups by <c>date_trunc('week', ...)</c>, which
        /// only ever emits Mondays), but it means a caller does not have to pre-align every point
        /// itself, and it is what "Week bucketing snaps to Monday" is actually asserting.
        /// </param>
        /// <param name="from">
        /// The first calendar day the returned series must cover. When <paramref name="bucket"/>
        /// is <see cref="SeriesBucket.Week"/>, the first returned point is that day's own Monday
        /// (which can fall before <paramref name="from"/> itself) - the point still represents the
        /// week <paramref name="from"/> belongs to.
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
        /// <see cref="SeriesPoint.Bucket"/> falls outside <c>[from, to]</c> (after the week-snap
        /// above, when applicable) is dropped rather than extending the returned range - the
        /// caller asked for a window, not "the window plus whatever stray rows happened to be
        /// there".
        /// </returns>
        public static IReadOnlyList<SeriesPoint> Densify(IEnumerable<SeriesPoint> sparse, DateOnly from, DateOnly to, SeriesBucket bucket)
        {
            ArgumentNullException.ThrowIfNull(sparse);

            var bucketStarts = BuildBucketStarts(from, to, bucket);

            // however many sparse points share a bucket (there should be at most one, since the
            // SQL side already groups by the same expression) their values are summed rather than
            // one silently overwriting another - cheap safety net, never exercised in the normal
            // path.
            var totals = new Dictionary<DateOnly, decimal>();
            foreach (var point in sparse)
            {
                var snappedBucket = bucket == SeriesBucket.Week ? StartOfIsoWeek(point.Bucket) : point.Bucket;
                if (snappedBucket < from || snappedBucket > to)
                {
                    continue; // outside the requested window - dropped, never widens it.
                }

                totals[snappedBucket] = totals.GetValueOrDefault(snappedBucket) + point.Value;
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
