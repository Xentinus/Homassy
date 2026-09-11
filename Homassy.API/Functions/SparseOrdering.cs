namespace Homassy.API.Functions
{
    /// <summary>
    /// The manual-order scheme shared by every reorderable list (shopping list items, storage and
    /// shopping locations, automations).
    ///
    /// Positions are stored as gapped integers rather than 0..n-1 indices. The gaps are what make a
    /// drag cheap: dropping one row between two others usually only needs a value picked from the gap
    /// between its new neighbours, so the write is one row, not a renumber of the whole list. The
    /// reorder endpoints still take the full ordered id list — that is the only shape a client can
    /// send that is unambiguous under concurrent edits — and this class turns it back into the
    /// smallest set of rows that actually has to change.
    ///
    /// Minimality is exact, not approximate: the rows that may keep their value are a longest strictly
    /// increasing subsequence of the requested order, so the number of writes is the smallest possible
    /// for that order. Everything not on that subsequence is given a value from the gap it lands in.
    /// When a gap has no room left (the pathological case after many moves into the same spot), the
    /// whole list is renumbered on <see cref="Gap"/> boundaries, which restores the gaps for later
    /// moves.
    /// </summary>
    public static class SparseOrdering
    {
        /// <summary>Distance between neighbouring positions in a freshly numbered list.</summary>
        public const int Gap = 1000;

        /// <summary>
        /// The position a newly created row gets: one gap past the current highest. New rows append to
        /// the end of the manual order. <paramref name="currentMax"/> is null for an empty list.
        /// </summary>
        public static int Append(int? currentMax)
        {
            if (!currentMax.HasValue) return Gap;
            // Saturate rather than wrap: a list that has somehow reached the ceiling appends at the
            // ceiling and the next reorder renumbers it back into range.
            return currentMax.Value > int.MaxValue - Gap ? int.MaxValue : currentMax.Value + Gap;
        }

        /// <summary>
        /// Plans the writes for a requested order.
        /// </summary>
        /// <param name="currentOrders">
        /// The rows' current <c>SortOrder</c> values, listed in the order the client asked for.
        /// </param>
        /// <returns>
        /// Index (into <paramref name="currentOrders"/>) → new sort order, containing only the rows
        /// whose value has to change. Empty when the requested order is already the stored one.
        /// </returns>
        public static IReadOnlyDictionary<int, int> PlanReorder(IReadOnlyList<int> currentOrders)
        {
            var changes = new Dictionary<int, int>();
            var count = currentOrders.Count;
            if (count == 0) return changes;

            // Rows allowed to keep their value: a longest strictly increasing subsequence of the
            // requested order. Every other row is rewritten, and there is no smaller set that works.
            var anchors = LongestIncreasingSubsequence(currentOrders);

            var runStart = 0;
            for (var a = 0; a <= anchors.Count; a++)
            {
                // The run of rows between two anchors (or before the first / after the last).
                var runEnd = a < anchors.Count ? anchors[a] : count;
                var runLength = runEnd - runStart;

                if (runLength > 0)
                {
                    long? floor = a > 0 ? currentOrders[anchors[a - 1]] : null;
                    long? ceiling = a < anchors.Count ? currentOrders[anchors[a]] : null;

                    if (!TryFillRun(floor, ceiling, runStart, runLength, changes))
                    {
                        return Renumber(count);
                    }
                }

                runStart = runEnd + 1;
            }

            return changes;
        }

        /// <summary>
        /// Assigns strictly increasing values to <paramref name="runLength"/> consecutive rows that
        /// must sit strictly between <paramref name="floor"/> and <paramref name="ceiling"/> (either
        /// of which is null at the ends of the list). False when the gap cannot hold them, which is
        /// the caller's signal to renumber instead.
        /// </summary>
        private static bool TryFillRun(long? floor, long? ceiling, int runStart, int runLength, Dictionary<int, int> changes)
        {
            if (floor.HasValue && ceiling.HasValue)
            {
                // Between two anchors: only the integers strictly between them are available.
                var room = ceiling.Value - floor.Value - 1;
                if (room < runLength) return false;

                var step = (ceiling.Value - floor.Value) / (runLength + 1);
                for (var k = 0; k < runLength; k++)
                {
                    changes[runStart + k] = (int)(floor.Value + step * (k + 1));
                }
                return true;
            }

            if (floor.HasValue)
            {
                // Tail of the list: walk up from the last anchor on full gaps.
                if (floor.Value + (long)Gap * runLength > int.MaxValue) return false;
                for (var k = 0; k < runLength; k++)
                {
                    changes[runStart + k] = (int)(floor.Value + (long)Gap * (k + 1));
                }
                return true;
            }

            if (ceiling.HasValue)
            {
                // Head of the list: walk down from the first anchor. Negative positions are fine —
                // only the relative order is meaningful.
                if (ceiling.Value - (long)Gap * runLength < int.MinValue) return false;
                for (var k = 0; k < runLength; k++)
                {
                    changes[runStart + k] = (int)(ceiling.Value - (long)Gap * (runLength - k));
                }
                return true;
            }

            // No anchors at all (an empty list never reaches here, so this is a list of one run).
            for (var k = 0; k < runLength; k++)
            {
                changes[runStart + k] = (k + 1) * Gap;
            }
            return true;
        }

        /// <summary>Rewrites every row onto clean <see cref="Gap"/> boundaries.</summary>
        private static Dictionary<int, int> Renumber(int count)
        {
            var changes = new Dictionary<int, int>(count);
            for (var i = 0; i < count; i++)
            {
                changes[i] = (i + 1) * Gap;
            }
            return changes;
        }

        /// <summary>
        /// Indices of a longest strictly increasing subsequence of <paramref name="values"/>, in
        /// ascending index order. Patience sorting — O(n log n).
        /// </summary>
        private static List<int> LongestIncreasingSubsequence(IReadOnlyList<int> values)
        {
            var count = values.Count;
            // tailIndex[l] = index of the smallest tail value among increasing subsequences of length l+1.
            var tailIndex = new List<int>();
            var previous = new int[count];

            for (var i = 0; i < count; i++)
            {
                previous[i] = -1;

                // First position whose tail value is >= values[i] (strictly increasing, so >= is replaced).
                var lo = 0;
                var hi = tailIndex.Count;
                while (lo < hi)
                {
                    var mid = (lo + hi) / 2;
                    if (values[tailIndex[mid]] < values[i]) lo = mid + 1;
                    else hi = mid;
                }

                if (lo > 0) previous[i] = tailIndex[lo - 1];
                if (lo == tailIndex.Count) tailIndex.Add(i);
                else tailIndex[lo] = i;
            }

            var result = new List<int>(tailIndex.Count);
            for (var i = tailIndex.Count > 0 ? tailIndex[^1] : -1; i >= 0; i = previous[i])
            {
                result.Add(i);
            }
            result.Reverse();
            return result;
        }
    }
}
