/**
 * Day grouping for the family activity timeline (`app/pages/activity/index.vue`).
 *
 * Pure and framework-free, like `app/utils/realtimeStatus.ts` — see that file's own note for the
 * "pure module + thin reactive composable" split this codebase uses. There is no composable
 * wrapping this one: the page calls `groupByDay` straight from a `computed`, since there is no
 * connection state to make reactive here, only a list and a clock.
 *
 * Calendar days are resolved in UTC, not the viewer's IANA timezone. That is deliberate, not an
 * oversight of "local": the API hands back UTC timestamps and this module is never given the
 * viewer's timezone (`now` is just a `Date`, and a `Date`'s local getters silently reflect
 * whatever the *runtime* — the browser, or a CI box — happens to be set to). Using local getters
 * here would make `dayBucketKey` non-deterministic across machines and would not even agree with
 * itself: a family in Budapest (UTC+2) whose evening activity local-crosses midnight before its
 * UTC calendar date does would see that entry drift between "today" and "yesterday" depending on
 * which of the two clocks a given render happened to read. Comparing UTC calendar dates is the
 * one boundary every caller agrees on regardless of where the app is running, which is what makes
 * `dayBucketKey`'s own unit tests deterministic rather than timezone-flaky.
 *
 * The comparison is a **calendar-date** difference, not an elapsed-time (epoch delta) one — that
 * is the actual bug this guards against, not the UTC-vs-local question above. A same-day entry
 * from very early this morning can be right up against 24 hours old by the time "now" is late
 * this evening (see the spec's "not a 24-hour window" case), and a naive
 * `Math.floor((now - timestamp) / MS_PER_DAY)` gets both directions wrong: it can read an entry
 * from 11pm yesterday as "today" (barely any elapsed time, but already a different calendar day)
 * and one from early this morning as still "today" only by chance of the clock, not because it
 * actually is. Converting each side to its own UTC midnight first and differencing *those* turns
 * "how long ago" into "how many calendar days apart", which is the question a day separator is
 * actually asking.
 */

/** 'today' / 'yesterday' for the two nearby buckets, else the entry's own UTC day as YYYY-MM-DD. */
export type DayBucketKey = 'today' | 'yesterday' | string

const MS_PER_DAY = 24 * 60 * 60 * 1000

/** YYYY-MM-DD, zero-padded, from a Date's UTC calendar fields. */
const isoDate = (date: Date): string => {
  const y = date.getUTCFullYear()
  const m = String(date.getUTCMonth() + 1).padStart(2, '0')
  const d = String(date.getUTCDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/**
 * The date's UTC midnight, as a whole number of days since the epoch. Two timestamps on the same
 * UTC calendar day always produce the same number here, however many (or few) hours apart they
 * are — which is the whole point: subtracting these, not the timestamps themselves, is what turns
 * a millisecond delta into a calendar-day delta.
 */
const utcDayNumber = (date: Date): number =>
  Math.floor(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()) / MS_PER_DAY)

/** Which day bucket `timestamp` falls into relative to `now`. Pure — no clock of its own. */
export const dayBucketKey = (timestamp: string, now: Date): DayBucketKey => {
  const date = new Date(timestamp)
  const dayDiff = utcDayNumber(now) - utcDayNumber(date)

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
