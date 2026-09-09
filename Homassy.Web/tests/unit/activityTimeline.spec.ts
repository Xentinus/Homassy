import { describe, expect, it } from 'vitest'
import { dayBucketKey, groupByDay, mergeLiveEntries } from '~/utils/activityTimeline'

/**
 * Builds a `Date` from local wall-clock components — never a `Z`/UTC string — so every case below
 * holds regardless of which timezone the process (a developer's machine, or CI) happens to run
 * in. `dayBucketKey` compares the *local* calendar day (see `activityTimeline.ts`'s header
 * comment), so a case built from a hardcoded `Z` timestamp would only assert the right thing on a
 * machine at one particular offset — exactly the bug this spec used to have. Month is 0-indexed to
 * match `Date`'s own constructor (8 = September).
 */
const local = (year: number, month: number, day: number, hour = 0, minute = 0): Date =>
  new Date(year, month, day, hour, minute)

/**
 * `dayBucketKey`'s first parameter is the string shape an API response actually carries, so every
 * input below goes through `.toISOString()`. That round-trips losslessly on any machine: `local`
 * builds an absolute instant by interpreting its components as *this process's* local time, and
 * `dayBucketKey` later reads that same instant back through this same process's local getters — so
 * whatever the offset is, it cancels out, and only the wall-clock components below ever matter.
 */
const at = (year: number, month: number, day: number, hour = 0, minute = 0): string =>
  local(year, month, day, hour, minute).toISOString()

const NOW = local(2026, 8, 8, 10, 0) // 2026-09-08, 10:00 local

describe('dayBucketKey', () => {
  it('labels a timestamp earlier on the same local day as today, however many hours apart', () => {
    // ~24 hours apart, but both still 2026-09-08 local — must not roll into "yesterday" just
    // because the gap is close to a full day.
    const now = local(2026, 8, 8, 23, 59)
    expect(dayBucketKey(at(2026, 8, 8, 0, 10), now)).toBe('today')
  })

  it('labels a timestamp late on the previous local day as yesterday', () => {
    expect(dayBucketKey(at(2026, 8, 7, 23, 0), NOW)).toBe('yesterday')
  })

  it('falls back to the local ISO day for anything older', () => {
    expect(dayBucketKey(at(2026, 8, 1, 12, 0), NOW)).toBe('2026-09-01')
  })

  it('buckets by local calendar date, not by a 24-hour window', () => {
    // Only 15 minutes before "now", but already the previous local day — proves the function
    // tracks the calendar date crossed, not an elapsed-time threshold. (The original spec reached
    // for the opposite — 24+ hours apart yet still "today" — but that combination is impossible:
    // two instants more than a day apart can never share a local calendar day.)
    const now = local(2026, 8, 8, 0, 5)
    expect(dayBucketKey(at(2026, 8, 7, 23, 50), now)).toBe('yesterday')
  })
})

describe('groupByDay', () => {
  it('returns one group per contiguous run and preserves order', () => {
    const entries = [
      { timestamp: at(2026, 8, 8, 9, 0) },
      { timestamp: at(2026, 8, 8, 8, 0) },
      { timestamp: at(2026, 8, 7, 20, 0) },
      { timestamp: at(2026, 8, 1, 10, 0) }
    ]
    const groups = groupByDay(entries, NOW)
    expect(groups.map(g => g.key)).toEqual(['today', 'yesterday', '2026-09-01'])
    expect(groups[0]!.entries).toHaveLength(2)
  })

  it('returns an empty array for no entries', () => {
    expect(groupByDay([], NOW)).toEqual([])
  })
})

describe('mergeLiveEntries', () => {
  const single = (publicId: string) => ({ publicId, count: 1 })
  const run = (publicId: string, itemIds: string[]) =>
    ({ publicId, count: itemIds.length, items: itemIds.map(id => ({ publicId: id })) })

  it('prepends a genuinely new entry with nothing to subsume', () => {
    const existing = [single('pX')]
    const fresh = [single('pNew')]

    expect(mergeLiveEntries(fresh, existing)).toEqual([single('pNew'), single('pX')])
  })

  it('returns existing unchanged (same reference) when there is nothing fresh', () => {
    const existing = [single('pX')]
    expect(mergeLiveEntries([], existing)).toBe(existing)
  })

  // The regression this closes: Anna deletes A (entry pA, count 1); 30 seconds later she deletes
  // B, within the same actor+type+5-minute bucket, so the run re-anchors to B (entry pB, count 2,
  // items [B, A]). pB is a "new" publicId, but A's row is not new — it is now just the run's
  // oldest row. The stale standalone pA entry must be dropped, not left duplicating A alongside
  // the new aggregated card.
  it('drops an existing standalone entry whose row is now the oldest row of an incoming run', () => {
    const existing = [single('pA')]
    const fresh = [run('pB', ['pB', 'pA'])]

    expect(mergeLiveEntries(fresh, existing)).toEqual([run('pB', ['pB', 'pA'])])
  })

  it('leaves an unrelated existing entry untouched, ordered after the fresh one', () => {
    const existing = [single('pOther')]
    const fresh = [run('pB', ['pB', 'pA'])]

    const result = mergeLiveEntries(fresh, existing)
    expect(result.map(e => e.publicId)).toEqual(['pB', 'pOther'])
  })

  it('a single-count fresh entry only ever subsumes its own id, never an unrelated existing one', () => {
    const existing = [single('pOther')]
    const fresh = [single('pNew')]

    expect(mergeLiveEntries(fresh, existing).map(e => e.publicId)).toEqual(['pNew', 'pOther'])
  })
})
