/**
 * Slice geometry for DonutChart.vue (R5 "Insight", Task 5).
 *
 * Pulled out of the component into its own framework-free module for one reason: the vitest
 * harness for this milestone (`vitest.config.ts`) runs under a plain `node` environment with no
 * Vue runtime, so a `.vue` single-file component cannot be imported by a spec at all. Extracting
 * the one part of DonutChart's own logic worth a dedicated test — turning `{ key, label, value }`
 * inputs into ring-slice angles — into a plain module makes it testable the same way
 * `~/utils/chart/{scale,path,format,series}` are: `describe`/`it` against an exported function,
 * no component mount required. See `tests/unit/donutGeometry.spec.ts`.
 *
 * Deliberately colocated with `DonutChart.vue` rather than added to `~/utils/chart/`: merging
 * small categories into a trailing "other" slice is a donut-specific concern (a bar or a line has
 * no analogous idea of "too thin to hit with a thumb"), not a general primitive the other two
 * chart types would ever call — unlike `arcPath`/`seriesColors`/etc., which genuinely are shared.
 *
 * Everything downstream of "which slice gets how much of the ring, in what order" — the actual
 * `<path>` via `arcPath`, the fill colour via `seriesColors` — stays in the component.
 */

export interface DonutSliceInput {
  key: string
  label: string
  value: number
}

export interface DonutSlice {
  key: string
  label: string
  value: number
  /** This slice's share of the total, in [0, 1]. */
  fraction: number
  /** Radians, same convention as `arcPath`/`polarPoint`: 0 is twelve o'clock, increasing clockwise. */
  startRad: number
  endRad: number
  /** True for the single trailing slice created by merging everything under `OTHER_THRESHOLD_RATIO`. */
  isOther: boolean
}

/**
 * Categories under this share of the total are merged into a trailing "other" slice rather than
 * drawn as their own sliver. A spec value from the task brief ("the ring never shows a sliver too
 * thin to hit with a thumb"), not an implementer's tuning knob — so, unlike `scale.ts`'s
 * `DEFAULT_TICK_COUNT`, it is not exposed as a parameter.
 */
const OTHER_THRESHOLD_RATIO = 0.02

/**
 * Sentinel key for the merged slice. Real categories are caller data and can be anything; this
 * constant is only ever compared against itself, so a caller happening to use the same string
 * for one of its own keys cannot collide with it in practice.
 */
const OTHER_SLICE_KEY = '__other__'

/**
 * Turns raw category values into ring slices, in two passes.
 *
 * 1. Every input gets its own share of the total (`value / total`) and sorts into "kept" (>= 2%)
 *    or "small" (< 2%). The small ones are merged into one trailing slice — summed by *value* for
 *    the merged slice's own `value`, but summed by *share* for its `fraction`. That distinction is
 *    deliberate, not an oversight: it is the realistic way a real donut's angles pick up the kind
 *    of float drift `arcPath`'s `FULL_TURN_EPSILON_RAD` (`~/utils/chart/path`) exists to absorb —
 *    summing many already-divided shares can land a hair short of the `1.0` that summing the same
 *    values first and dividing once would give exactly. `donutGeometry.spec.ts` drives an input
 *    through this exact code path to land there, closing the gap that constant's own comment flags
 *    as untested: "no test exercises it."
 * 2. The kept slices, in their original order, plus the merged "other" (always last, only if
 *    anything was merged) are walked once, accumulating a running share into `startRad`/`endRad`
 *    — the standard cumulative-pie layout, each slice picking up exactly where the last one ended.
 *
 * A non-positive total (no categories, or values summing to zero or less) has no ring to divide
 * and returns no slices; `DonutChart.vue` feeds that same emptiness into `tableRows`, which is what
 * actually drives `ChartCard`'s empty state.
 */
export const buildDonutSlices = (slices: readonly DonutSliceInput[], otherLabel: string): DonutSlice[] => {
  const total = slices.reduce((sum, slice) => sum + slice.value, 0)
  if (total <= 0) return []

  const withShares = slices.map(slice => ({ ...slice, fraction: slice.value / total }))
  const kept = withShares.filter(slice => slice.fraction >= OTHER_THRESHOLD_RATIO)
  const small = withShares.filter(slice => slice.fraction < OTHER_THRESHOLD_RATIO)

  const ordered = kept.map(slice => ({ ...slice, isOther: false }))
  if (small.length > 0) {
    ordered.push({
      key: OTHER_SLICE_KEY,
      label: otherLabel,
      value: small.reduce((sum, slice) => sum + slice.value, 0),
      fraction: small.reduce((sum, slice) => sum + slice.fraction, 0),
      isOther: true
    })
  }

  let cursor = 0
  return ordered.map((slice) => {
    const startRad = cursor * Math.PI * 2
    cursor += slice.fraction
    const endRad = cursor * Math.PI * 2
    return { ...slice, startRad, endRad }
  })
}
