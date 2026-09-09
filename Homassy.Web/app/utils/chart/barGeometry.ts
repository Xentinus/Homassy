/**
 * X-axis ticks and domains for ChartBar.vue (R5 "Insight", Task 5 follow-up).
 *
 * A plain, framework-free module rather than a section of ChartBar.vue's own `<script setup>`,
 * because the vitest harness for this milestone (`vitest.config.ts`) runs under a plain `node`
 * environment with no Vue runtime: a `.vue` single-file component cannot be imported by a spec at
 * all, so any component logic worth a regression test has to live somewhere a spec can reach it.
 * `barXTicks`/`barXDomain`/`barValueDomain` are the one part of ChartBar's own logic that had a bug
 * worth exactly that, so they are extracted here, `describe`/`it`-testable against exported
 * functions, no component mount required. See `tests/unit/barGeometry.spec.ts`.
 *
 * Lives in `~/utils/chart/` alongside `scale.ts`/`path.ts`/`format.ts`/`series.ts` — the other pure
 * modules of this chart layer — but kept as its own file rather than folded into `scale.ts`:
 * `[ticks[0], ticks[last]]` is already exactly how `ChartLine.vue`'s own `yDomain` follows its
 * `yTicks` — the bug fixed here was `ChartBar`'s axis domain not following its own ticks the same
 * way, not anything wrong in `scale.ts`'s generic `linearScale`/`bandScale`/`niceTicks` themselves.
 * These two functions are `ChartBar`-specific derived domains built on top of `niceTicks`, not a
 * shared primitive `ChartLine`/`ChartDonut` would ever call — the same reason its sibling
 * `donutGeometry.ts` stays out of `scale.ts` too.
 *
 * Two *different* domains come out of the same `ticks`, because they answer two different
 * questions, and `ChartBar.vue` needs both:
 *  - `barXDomain` — where do the reference gridlines go? Answer: wherever `niceTicks` put them,
 *    full stop, so every gridline it drew lands inside the `<svg>`'s `viewBox`.
 *  - `barValueDomain` — where does a bar of a given value end? Answer: always measured from a
 *    zero baseline, because that is what a bar's length *means* — `niceTicks` widening the axis
 *    below zero for the all-zero edge case must not move a real bar's own zero point off the
 *    left edge of the plot. See that function's own comment for the regression this avoids.
 */

import { niceTicks } from '~/utils/chart/scale'

/**
 * The greatest bar value, widened by `niceTicks` into round axis ticks starting at 0. An empty
 * `bars` array (or one whose values are all <= 0) still needs *some* ticks — the `0` fallback in
 * the `reduce` guarantees `niceTicks` always receives a real `[0, max]` span, including the
 * all-zero case `niceTicks` itself widens symmetrically around 0 (see that function's own comment
 * on its flat-series branch).
 */
export const barXTicks = (bars: readonly { value: number }[]): number[] => {
  const max = bars.reduce((highest, bar) => Math.max(highest, bar.value), 0)
  return niceTicks(0, max)
}

/**
 * The x-*axis* domain — where the reference gridlines are positioned — taken from `ticks`' own
 * first and last entry, never a hardcoded `0`, the same way `ChartLine.vue`'s `yDomain` is
 * `[ticks[0], ticks[last]]` rather than the series' raw min/max. For any `max > 0` this is
 * `[0, max]` exactly, since `niceTicks(0, max)`'s own lower bound is always `0`. It only differs
 * from a hardcoded `0` when `bars` is non-empty and every value is exactly `0`: `niceTicks`'
 * flat-series widening then returns a domain straddling zero in both directions (e.g.
 * `[-1, -0.5, 0, 0.5, 1]`), and a hardcoded `0` lower bound would put two of those five ticks
 * outside `[0, 1]` — projecting to negative x and falling outside the `<svg>`'s `viewBox` entirely.
 *
 * Deliberately **not** used for a bar's own drawn length — see `barValueDomain` below.
 */
export const barXDomain = (ticks: readonly number[]): [number, number] => [ticks[0]!, ticks[ticks.length - 1]!]

/**
 * The domain a bar's own drawn length is measured against — always anchored at `0`, unlike
 * `barXDomain`. A bar's length has to start at its own zero baseline regardless of how far the
 * *axis* domain widens on either side of it, so this never adopts `ticks[0]` the way `barXDomain`
 * does; it only ever widens the upper end, same as the original (pre-fix) domain did.
 *
 * This one is the reason the two domains cannot be merged into one: for an all-zero, non-empty
 * `bars` set, `ticks` is `niceTicks`' widened `[-1, -0.5, 0, 0.5, 1]`, so `barXDomain(ticks)` is
 * `[-1, 1]`. Scaling a bar's value of `0` against *that* domain would land it at the *middle* of
 * the plot rather than at its left edge — every bar in an all-zero chart would draw as a
 * misleadingly prominent half-length bar instead of collapsing to nothing. `barValueDomain` keeps
 * that from happening: it is identical to `barXDomain` for every real (non-all-zero) bar set,
 * since `ticks[0]` is already `0` there, and only the pathological all-zero case tells them apart.
 */
export const barValueDomain = (ticks: readonly number[]): [number, number] => [0, ticks[ticks.length - 1]!]
