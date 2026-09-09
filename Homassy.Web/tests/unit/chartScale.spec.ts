import { describe, expect, it } from 'vitest'
import { bandScale, linearScale, niceTicks, timeTicks } from '~/utils/chart/scale'

describe('linearScale', () => {
  it('maps the domain ends onto the range ends', () => {
    const s = linearScale([0, 10], [0, 100])
    expect(s(0)).toBe(0)
    expect(s(10)).toBe(100)
    expect(s(5)).toBe(50)
  })

  it('supports an inverted range, which is how SVG y-axes work', () => {
    const s = linearScale([0, 10], [200, 0])
    expect(s(0)).toBe(200)
    expect(s(10)).toBe(0)
  })

  it('collapses a zero-width domain onto the range start instead of dividing by zero', () => {
    const s = linearScale([7, 7], [0, 100])
    expect(Number.isFinite(s(7))).toBe(true)
    expect(s(7)).toBe(0)
  })
})

describe('bandScale', () => {
  it('spaces keys evenly and reports a positive bandwidth', () => {
    const s = bandScale(['a', 'b', 'c'], [0, 300], 0)
    expect(s.bandwidth).toBe(100)
    expect(s.position('a')).toBe(0)
    expect(s.position('c')).toBe(200)
  })

  it('shrinks the bandwidth by the padding ratio', () => {
    const s = bandScale(['a', 'b'], [0, 200], 0.5)
    expect(s.bandwidth).toBe(50)
  })

  it('returns the range start for an unknown key rather than NaN', () => {
    const s = bandScale(['a'], [10, 110], 0)
    expect(s.position('missing')).toBe(10)
  })
})

describe('niceTicks', () => {
  it('produces round numbers spanning the data', () => {
    const ticks = niceTicks(0, 97, 5)
    expect(ticks[0]).toBe(0)
    expect(ticks[ticks.length - 1]).toBeGreaterThanOrEqual(97)
    expect(ticks.every(t => Number.isFinite(t))).toBe(true)
  })

  it('always returns at least two ticks, even for a flat series', () => {
    expect(niceTicks(5, 5, 5).length).toBeGreaterThanOrEqual(2)
  })

  it('handles an all-zero series without emitting duplicates', () => {
    const ticks = niceTicks(0, 0, 5)
    expect(new Set(ticks).size).toBe(ticks.length)
  })
})

describe('timeTicks', () => {
  const day = 24 * 60 * 60 * 1000

  it('never exceeds maxTicks', () => {
    const ticks = timeTicks(0, 90 * day, 'day', 6)
    expect(ticks.length).toBeLessThanOrEqual(6)
  })

  it('starts at or after the window start and ends at or before its end', () => {
    const ticks = timeTicks(day, 30 * day, 'day', 6)
    expect(ticks[0]).toBeGreaterThanOrEqual(day)
    expect(ticks[ticks.length - 1]!).toBeLessThanOrEqual(30 * day)
  })
})
