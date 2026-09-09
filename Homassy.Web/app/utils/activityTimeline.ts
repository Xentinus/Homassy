/**
 * Day grouping for the family activity timeline (`app/pages/activity/index.vue`).
 *
 * Pure and framework-free, like `app/utils/realtimeStatus.ts` — see that file's own note for the
 * "pure module + thin reactive composable" split this codebase uses. There is no composable
 * wrapping this one: the page calls `groupByDay` straight from a `computed`, since there is no
 * connection state to make reactive here, only a list and a clock.
 *
 * Calendar days are resolved in the *runtime's local* timezone, not UTC. This is client-only code
 * (`app/pages/activity/index.vue`), so "the runtime" always means the viewer's own device, and the
 * day separators are a statement about the viewer's own wall clock — they read "Today" /
 * "Yesterday" / a date, and "today" can only sensibly mean the viewer's today. The API hands back
 * UTC timestamps, but bucketing by UTC instead mislabels a band of hours around *local* midnight
 * for every viewer not on UTC: someone in UTC+2 reading the timeline at 00:30 local expects an
 * activity from 23:00 local *yesterday* to read "Yesterday" — even though, at that moment, both
 * instants already share the same UTC calendar date, so a UTC comparison would call it "today".
 * The reverse mislabelling happens on the other side of local midnight too. Comparing local
 * calendar dates via the runtime's own local getters (`getFullYear` / `getMonth` / `getDate`) is
 * what makes "Today"/"Yesterday" actually track the viewer's own today and yesterday instead of
 * whichever side of UTC's midnight they happen to be standing on.
 *
 * The comparison is a **calendar-date** difference, not an elapsed-time (epoch delta) one — that
 * is the other bug this guards against, independent of the local-vs-UTC question above. A
 * same-day entry from very early this morning can be right up against 24 hours old by the time
 * "now" is late this evening (see the spec's "not a 24-hour window" case), and a naive
 * `Math.floor((now - timestamp) / MS_PER_DAY)` gets both directions wrong: it can read an entry
 * from 11pm yesterday as "today" (barely any elapsed time, but already a different calendar day)
 * and one from early this morning as still "today" only by chance of the clock, not because it
 * actually is. Converting each side to its own local midnight first and differencing *those* turns
 * "how long ago" into "how many calendar days apart", which is the question a day separator is
 * actually asking. That difference is *rounded*, not floored: the calendar day either side of a
 * DST transition is 23 or 25 hours long, not exactly 24, so rounding the whole gap between the two
 * midnights absorbs that, where flooring each midnight's own epoch value independently would not.
 */

/** 'today' / 'yesterday' for the two nearby buckets, else the entry's own local day as YYYY-MM-DD. */
export type DayBucketKey = 'today' | 'yesterday' | string

const MS_PER_DAY = 24 * 60 * 60 * 1000

/** YYYY-MM-DD, zero-padded, from a Date's *local* calendar fields — the viewer's own day. */
const isoDate = (date: Date): string => {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/**
 * `date`'s own local midnight, as a `Date`. Two timestamps on the same local calendar day always
 * produce an identical value here, however many (or few) hours apart they are — which is the
 * whole point: differencing these, not the timestamps themselves, is what turns a millisecond
 * delta into a calendar-day delta.
 */
const localMidnight = (date: Date): Date => new Date(date.getFullYear(), date.getMonth(), date.getDate())

/** Which day bucket `timestamp` falls into relative to `now`. Pure — no clock of its own. */
export const dayBucketKey = (timestamp: string, now: Date): DayBucketKey => {
  const date = new Date(timestamp)
  const dayDiff = Math.round((localMidnight(now).getTime() - localMidnight(date).getTime()) / MS_PER_DAY)

  if (dayDiff === 0) return 'today'
  if (dayDiff === 1) return 'yesterday'
  return isoDate(date)
}

/** One day bucket's worth of entries, in the order `groupByDay` encountered them. */
export interface DayGroup<T> {
  key: DayBucketKey
  entries: T[]
}

/**
 * Buckets `entries` (assumed newest-first, as the timeline endpoint returns them) into one group
 * per contiguous run of the same `dayBucketKey`, preserving input order both across and within
 * groups. Not a `groupBy`-into-a-map: the timeline is a single scrolling list, so two runs of the
 * same day separated by a different day in between (impossible for a newest-first feed, but not
 * assumed here) stay two separate sections rather than being merged back together.
 */
export const groupByDay = <T extends { timestamp: string }>(entries: T[], now: Date): DayGroup<T>[] => {
  const groups: DayGroup<T>[] = []

  for (const entry of entries) {
    const key = dayBucketKey(entry.timestamp, now)
    const current = groups.at(-1)

    if (current && current.key === key) {
      current.entries.push(entry)
    } else {
      groups.push({ key, entries: [entry] })
    }
  }

  return groups
}

/**
 * Every individual activity row `entry` represents on the wire — itself when `count === 1`, or
 * every row in `items` for a collapsed run. `items` always lists the run's *full* membership,
 * never truncated to the page size (see `Homassy.API.Functions.ActivityFunctions.BuildTimelineEntry`),
 * which is what makes `mergeLiveEntries` below correct rather than merely best-effort.
 */
const rowIdsOf = (entry: { publicId: string; count: number; items?: { publicId: string }[] }): string[] =>
  entry.count > 1 && entry.items ? entry.items.map(item => item.publicId) : [entry.publicId]

/**
 * Reconciles a fresh first-page fetch against what `app/pages/activity/index.vue`'s live-prepend
 * already has on screen. `fresh` must already be filtered down to entries whose own `publicId` is
 * not one the page has seen before (the page's existing `knownIds` check) — this function's job
 * starts one step further in: even a "new" `publicId` can be a run that already had a standalone
 * entry on screen a moment ago, just re-anchored.
 *
 * `entry.publicId` identifies a run by its *newest* row, which moves every time the run grows —
 * a re-anchor. Deduping only on that moving id (the bug this closes) treats a grown run's new
 * anchor as wholly unrelated to what was on screen before: the old, now-stale standalone entry for
 * what is now merely the run's *oldest* row stays put, so that row renders twice — once on its
 * own, once folded into the new aggregated card.
 *
 * The fix: drop any existing entry whose own row is now subsumed by an incoming one — i.e. its
 * `publicId` shows up among the rows an incoming entry represents (`rowIdsOf`, above). This is
 * only as complete as `fresh` reaches: a run that grew enough to push its now-stale predecessor
 * off the first page entirely (many unrelated runs all changing at once between one refresh and
 * the next) is not reconciled by this or any check working from page-1 data alone — a residual
 * case no client-side dedup can see, since the payload for it was never fetched.
 */
export const mergeLiveEntries = <T extends { publicId: string; count: number; items?: { publicId: string }[] }>(
  fresh: T[],
  existing: T[]
): T[] => {
  if (fresh.length === 0) return existing

  const subsumedIds = new Set(fresh.flatMap(rowIdsOf))
  const survivors = existing.filter(entry => !subsumedIds.has(entry.publicId))
  return [...fresh, ...survivors]
}
