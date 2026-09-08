import { describe, expect, it } from 'vitest'
import { dayBucketKey, groupByDay } from '~/utils/activityTimeline'

const NOW = new Date('2026-09-08T10:00:00Z')

describe('dayBucketKey', () => {
  it('labels the current local day today', () => {
    expect(dayBucketKey('2026-09-08T08:30:00Z', NOW)).toBe('today')
  })

  it('labels the previous local day yesterday', () => {
    expect(dayBucketKey('2026-09-07T23:00:00Z', NOW)).toBe('yesterday')
  })

  it('falls back to an ISO day for anything older', () => {
    expect(dayBucketKey('2026-09-01T12:00:00Z', NOW)).toBe('2026-09-01')
  })

  it('buckets by local calendar day, not by a 24-hour window', () => {
    // 25 hours before "now" but still the same local day must not fall into yesterday.
    expect(dayBucketKey('2026-09-08T00:10:00Z', new Date('2026-09-08T23:59:00Z'))).toBe('today')
  })
})

describe('groupByDay', () => {
  it('returns one group per contiguous run and preserves order', () => {
    const entries = [
      { timestamp: '2026-09-08T09:00:00Z' },
      { timestamp: '2026-09-08T08:00:00Z' },
      { timestamp: '2026-09-07T20:00:00Z' },
      { timestamp: '2026-09-01T10:00:00Z' }
    ]
    const groups = groupByDay(entries, NOW)
    expect(groups.map(g => g.key)).toEqual(['today', 'yesterday', '2026-09-01'])
    expect(groups[0]!.entries).toHaveLength(2)
  })

  it('returns an empty array for no entries', () => {
    expect(groupByDay([], NOW)).toEqual([])
  })
})
