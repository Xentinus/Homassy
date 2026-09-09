import { describe, expect, it } from 'vitest'
import { arcPath } from '~/utils/chart/path'
import { buildDonutSlices } from '~/components/chart/donutGeometry'

describe('buildDonutSlices', () => {
  it('gives a single category the whole ring', () => {
    const slices = buildDonutSlices([{ key: 'a', label: 'A', value: 10 }], 'Other')
    expect(slices).toHaveLength(1)
    expect(slices[0]!.startRad).toBe(0)
    expect(slices[0]!.endRad).toBe(Math.PI * 2)
    expect(slices[0]!.isOther).toBe(false)
  })

  it('lays out kept slices back to back in their original order', () => {
    const slices = buildDonutSlices([
      { key: 'a', label: 'A', value: 50 },
      { key: 'b', label: 'B', value: 50 }
    ], 'Other')
    expect(slices).toHaveLength(2)
    expect(slices[0]!.startRad).toBe(0)
    expect(slices[0]!.endRad).toBeCloseTo(Math.PI)
    expect(slices[1]!.startRad).toBeCloseTo(Math.PI)
    expect(slices[1]!.endRad).toBeCloseTo(Math.PI * 2)
  })

  it('merges categories under 2% into one trailing "other" slice', () => {
    const slices = buildDonutSlices([
      { key: 'big', label: 'Big', value: 97 },
      { key: 'tiny1', label: 'Tiny 1', value: 1 },
      { key: 'tiny2', label: 'Tiny 2', value: 1 },
      { key: 'tiny3', label: 'Tiny 3', value: 1 }
    ], 'Other')

    expect(slices).toHaveLength(2)
    expect(slices[0]!.key).toBe('big')
    expect(slices[0]!.isOther).toBe(false)
    expect(slices[1]!.isOther).toBe(true)
    expect(slices[1]!.label).toBe('Other')
    expect(slices[1]!.value).toBe(3)
    // The merged slice is last, so it is the one that closes the ring.
    expect(slices[1]!.endRad).toBeCloseTo(Math.PI * 2)
  })

  it('leaves a slice sitting exactly on the 2% boundary out of "other"', () => {
    const slices = buildDonutSlices([
      { key: 'main', label: 'Main', value: 98 },
      { key: 'boundary', label: 'Boundary', value: 2 }
    ], 'Other')

    expect(slices).toHaveLength(2)
    expect(slices.some(s => s.isOther)).toBe(false)
  })

  it('returns no slices for a non-positive total rather than dividing by zero', () => {
    expect(buildDonutSlices([], 'Other')).toEqual([])
    expect(buildDonutSlices([{ key: 'a', label: 'A', value: 0 }], 'Other')).toEqual([])
  })

  it(
    'closes the ring instead of leaving a hairline gap when many merged shares sum a hair short of a full turn',
    () => {
      // 53 equal categories, each ~1.887% of the total — every one of them lands under the 2%
      // threshold, so all 53 are merged into a single "other" slice that is meant to span the
      // entire ring. Its `fraction` is summed from 53 individually-divided shares (1/53 each)
      // rather than derived as one fresh division, which is exactly the accumulation pattern
      // `arcPath`'s own `FULL_TURN_EPSILON_RAD` comment describes: summed this way, the total
      // lands at 0.9999999999999999 in IEEE754 double precision — a hair under 1, not exactly it.
      // This is not a hand-picked constant; it is what actually falls out of summing 1/53 fifty-
      // three times, verified once against a plain Node repl before writing this test.
      const categories = Array.from({ length: 53 }, (_, i) => ({ key: `c${i}`, label: `C${i}`, value: 1 }))
      const slices = buildDonutSlices(categories, 'Other')

      expect(slices).toHaveLength(1)
      const [other] = slices
      expect(other!.isOther).toBe(true)

      // Confirm real float drift actually occurred here, rather than an accidental exact match —
      // otherwise this test would not be exercising the tolerance at all.
      expect(other!.endRad).toBeLessThan(Math.PI * 2)
      expect(other!.endRad).toBeGreaterThan(Math.PI * 2 - 1e-9)

      // The interesting assertion: feed that "just under a full turn" sweep into the real
      // `arcPath` and confirm it still renders as a closed ring. `arcPath` draws a full turn as
      // two half-turn arcs per radius (`fullCircleArc`); a single arc command instead would mean
      // the tolerance did not catch this case, and — once its start/end points round to the same
      // 3-decimal coordinate — the "gap" degenerates into a zero-length arc that renders nothing
      // at all (see `arcPath`'s own module comment), not merely a visible sliver.
      const d = arcPath(100, 100, 80, 50, other!.startRad, other!.endRad)
      const outerArcCount = (d.match(/A80,80/g) ?? []).length
      const innerArcCount = (d.match(/A50,50/g) ?? []).length
      expect(outerArcCount).toBe(2)
      expect(innerArcCount).toBe(2)
    }
  )
})
