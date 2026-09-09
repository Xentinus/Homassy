import { describe, expect, it } from 'vitest'
import { linearScale, niceTicks } from '~/utils/chart/scale'
import { barValueDomain, barXDomain, barXTicks } from '~/components/chart/barGeometry'

describe('barXTicks', () => {
  it('produces niceTicks(0, max) from the highest bar value', () => {
    const ticks = barXTicks([{ value: 3 }, { value: 42 }, { value: 15 }])
    expect(ticks).toEqual(niceTicks(0, 42))
  })

  it('falls back to niceTicks(0, 0) for an empty bar set', () => {
    expect(barXTicks([])).toEqual(niceTicks(0, 0))
  })
})

describe('barXDomain', () => {
  it('covers [0, max] for an ordinary, non-zero bar set', () => {
    const ticks = barXTicks([{ value: 42 }])
    const domain = barXDomain(ticks)
    expect(domain[0]).toBe(0)
    expect(domain[1]).toBe(ticks[ticks.length - 1])
  })

  it(
    'covers every tick niceTicks returns for an all-zero, non-empty bar set, instead of hardcoding a 0 lower bound',
    () => {
      // Every bar is exactly 0, so the highest value niceTicks sees is 0 too. niceTicks' own
      // flat-series-at-zero widening (see that function's comment in ~/utils/chart/scale) then
      // spreads the ticks symmetrically around zero rather than starting at it — confirmed here
      // rather than assumed, so this test is not silently vacuous if that widening ever changes.
      const bars = [{ value: 0 }, { value: 0 }, { value: 0 }]
      const ticks = barXTicks(bars)
      expect(ticks[0]).toBeLessThan(0)

      const domain = barXDomain(ticks)

      // The real assertion: every tick niceTicks produced must fall inside the domain the x-scale
      // is built from. A hardcoded `[0, ticks[last]]` domain fails this for every negative tick —
      // e.g. [-1, -0.5] land outside a [0, 1] domain and their gridlines project off the viewBox.
      for (const tick of ticks) {
        expect(tick).toBeGreaterThanOrEqual(domain[0])
        expect(tick).toBeLessThanOrEqual(domain[1])
      }
      expect(domain[0]).toBe(ticks[0])
      expect(domain[1]).toBe(ticks[ticks.length - 1])
    }
  )
})

describe('barValueDomain', () => {
  it('matches barXDomain for an ordinary, non-zero bar set — both start at 0', () => {
    const ticks = barXTicks([{ value: 42 }])
    expect(barValueDomain(ticks)).toEqual(barXDomain(ticks))
  })

  it(
    'stays anchored at 0 for an all-zero, non-empty bar set, instead of adopting barXDomain\'s negative lower bound',
    () => {
      const ticks = barXTicks([{ value: 0 }, { value: 0 }])
      expect(ticks[0]).toBeLessThan(0) // sanity: niceTicks really did widen negative here

      const valueDomain = barValueDomain(ticks)
      expect(valueDomain).toEqual([0, ticks[ticks.length - 1]])
      // The whole point: this must differ from the axis domain for the same ticks, or a bar's
      // length would be measured against the widened axis instead of its own zero baseline.
      expect(valueDomain).not.toEqual(barXDomain(ticks))
    }
  )

  it(
    'keeps a zero-value bar at zero length even when the axis domain has widened below zero',
    () => {
      // The regression this guards: if a bar's endX were scaled against barXDomain (like the
      // axis gridlines are) rather than barValueDomain, a value of 0 would land at the *middle*
      // of the plot once the axis domain goes negative — every bar in an all-zero chart would
      // draw as a misleadingly prominent half-length bar instead of collapsing to nothing.
      const ticks = barXTicks([{ value: 0 }, { value: 0 }])
      const range: [number, number] = [92, 310]

      const valueScale = linearScale(barValueDomain(ticks), range)
      expect(valueScale(0)).toBe(range[0])

      const axisScale = linearScale(barXDomain(ticks), range)
      expect(axisScale(0)).not.toBe(range[0])
    }
  )
})
