/**
 * Pure geometry and axis-label maths for the hand-rolled SVG chart layer (R5 "Insight").
 *
 * Nothing in this file renders anything — no SVG, no Vue, no DOM. Later tasks in this milestone
 * (`LineChart.vue`, `BarChart.vue`) call `linearScale`/`bandScale` to turn data values into pixel
 * positions, and `niceTicks`/`timeTicks` to choose which values get an axis label. Kept
 * dependency-free and framework-free on purpose: this module is exercised by a `node`-environment
 * vitest spec with no Nuxt/Vue runtime (see `vitest.config.ts`), so a maths bug here is caught long
 * before any chart is actually on screen.
 */

// --- linearScale ---------------------------------------------------------------------------
//
// Maps a numeric domain (the data) onto a pixel range (the SVG viewport), continuously.

/**
 * A linear scale is callable — `scale(value)` returns the projected pixel — but also carries the
 * `domain`/`range` it was built from, so a chart component can read them back for axis ticks and
 * gridlines without threading the same two tuples through props a second time.
 */
export interface LinearScale {
  (value: number): number
  domain: [number, number]
  range: [number, number]
}

export const linearScale = (domain: [number, number], range: [number, number]): LinearScale => {
  const [domainStart, domainEnd] = domain
  const [rangeStart, rangeEnd] = range
  const domainSpan = domainEnd - domainStart

  const project = (value: number): number => {
    // A zero-width domain — a single data point, or a metric that has not moved (one reading in
    // a category) — would divide by zero below. Collapse it onto the range start instead of
    // returning NaN, so every caller can invoke the scale unconditionally with no "is this domain
    // degenerate" check of its own.
    if (domainSpan === 0) return rangeStart
    const t = (value - domainStart) / domainSpan
    return rangeStart + t * (rangeEnd - rangeStart)
  }

  return Object.assign(project, { domain, range })
}

// --- bandScale -------------------------------------------------------------------------------
//
// Slices a pixel range into one equal-width band per category key — a bar chart's category axis.

export const bandScale = (
  keys: readonly string[],
  range: [number, number],
  paddingRatio: number = 0
): { position: (key: string) => number; bandwidth: number } => {
  const [rangeStart, rangeEnd] = range
  // An empty key list (a family with zero inventory categories in the selected window) has no
  // band to divide the range into. Falling back to a single step spanning the whole range keeps
  // the division below finite; nothing will call `position` against an empty key set anyway.
  const stepCount = Math.max(keys.length, 1)
  const step = (rangeEnd - rangeStart) / stepCount
  const bandwidth = step * (1 - paddingRatio)

  const position = (key: string): number => {
    const index = keys.indexOf(key)
    // An unknown key — a stale reference after a category is renamed or removed between a
    // render and the next data refresh — lands at the range start rather than propagating
    // `-1 * step` (still a number, but a meaningless off-scale one) into whatever draws from it.
    if (index === -1) return rangeStart
    return rangeStart + index * step
  }

  return { position, bandwidth }
}

// --- niceTicks -------------------------------------------------------------------------------
//
// Chooses round-number axis labels spanning [min, max] using the classic 1/2/5×10^n step rule
// shared by every off-the-shelf charting library — reimplemented here rather than pulled in as a
// dependency for what is a dozen lines of arithmetic.

/**
 * Tick count used when a caller does not care to tune it. This is an implementer's choice, not a
 * spec value — feel free to change it if actual charts suggest a different density.
 */
const DEFAULT_TICK_COUNT = 5

/**
 * Half-width used to widen a flat series that sits exactly on zero (e.g. a week with no
 * consumption at all). There is no magnitude in the value itself to scale a widening off, unlike
 * a non-zero flat series, so this is a fixed fallback rather than a derived one.
 */
const FLAT_AT_ZERO_HALF_SPAN = 1

/**
 * Rounds a rough step up to the nearest 1, 2 or 5 times a power of ten, so labels read as
 * 20/40/60/80/100 instead of 19.4/38.8/58.2/77.6/97. Standard nice-number step selection.
 */
const niceStep = (roughStep: number): number => {
  const magnitude = 10 ** Math.floor(Math.log10(roughStep))
  const normalized = roughStep / magnitude
  if (normalized < 1.5) return magnitude
  if (normalized < 3) return 2 * magnitude
  if (normalized < 7) return 5 * magnitude
  return 10 * magnitude
}

export const niceTicks = (min: number, max: number, count: number = DEFAULT_TICK_COUNT): number[] => {
  let lo = min
  let hi = max

  if (lo === hi) {
    // A flat series (a week with no consumption, a single-category budget that never moved) has
    // no span to derive a step from. Widen it symmetrically around the value so the algorithm
    // below still produces at least two distinct, evenly spaced ticks instead of repeating the
    // same number `count` times.
    const half = lo === 0 ? FLAT_AT_ZERO_HALF_SPAN : Math.abs(lo) / 2
    lo -= half
    hi += half
  }

  const step = niceStep((hi - lo) / Math.max(count, 1))
  const niceMin = Math.floor(lo / step) * step
  const niceMax = Math.ceil(hi / step) * step

  // Each tick is computed fresh from `niceMin + i * step` rather than accumulated by repeatedly
  // adding `step` to a running total, so floating-point error cannot drift across many iterations
  // and quietly shift the last tick below `niceMax`.
  const stepCount = Math.round((niceMax - niceMin) / step)
  const ticks: number[] = []
  for (let i = 0; i <= stepCount; i++) {
    ticks.push(niceMin + i * step)
  }
  return ticks
}

// --- timeTicks -------------------------------------------------------------------------------
//
// The same idea as niceTicks, but for a time axis: labels must land on whole calendar buckets (a
// day, a week), never on an arbitrary interpolated instant partway through one.

const DAY_MS = 24 * 60 * 60 * 1000
const WEEK_MS = 7 * DAY_MS

/**
 * Epoch day 4 (1970-01-05) is the first Monday after the epoch (1970-01-01 was a Thursday).
 * Re-basing the millisecond axis onto this offset before dividing by `WEEK_MS` is what lets the
 * weekly branch of `timeTicks` find Monday-aligned boundaries with the same "divide, ceil,
 * multiply back" arithmetic the daily branch uses for midnights.
 */
const MONDAY_EPOCH_OFFSET_MS = 4 * DAY_MS

/**
 * Maximum tick count used when a caller does not care to tune it. This is an implementer's
 * choice, not a spec value — feel free to change it if actual charts suggest a different density.
 */
const DEFAULT_MAX_TICKS = 8

/**
 * Evenly spaced timestamps on real day/week boundaries within [fromMs, toMs], thinned to at most
 * `maxTicks` entries.
 *
 * Day boundaries are midnight (00:00:00 UTC). Week boundaries are Monday midnights, aligned to
 * epoch day 4 (1970-01-05 is the first Monday). This alignment is essential: the backend's
 * `date_trunc('week', ...)` in Postgres uses ISO-week (Monday-anchored), so the chart's axis
 * ticks and the server's data buckets must align, or labels sit between data points. This keeps
 * the function pure millisecond arithmetic with no `Date` object and no locale/timezone
 * dependency, matching the rest of this module and the `node`-environment spec it runs under with
 * no Nuxt/Vue runtime.
 */
export const timeTicks = (
  fromMs: number,
  toMs: number,
  bucket: 'day' | 'week',
  maxTicks: number = DEFAULT_MAX_TICKS
): number[] => {
  const bucketMs = bucket === 'week' ? WEEK_MS : DAY_MS

  // Compute the first bucket boundary at or after fromMs. For days, that is straightforward:
  // any midnight. For weeks, we must align to Monday (epoch day 4 mod 7) to match the server's
  // date_trunc('week') which is ISO-week Monday-anchored.
  const firstBoundary = (() => {
    if (bucket === 'day') {
      return Math.ceil(fromMs / DAY_MS) * DAY_MS
    }
    // Week: find the first Monday-midnight at or after fromMs, mirroring the day branch's
    // "divide by the bucket size, ceil, multiply back" shape rather than flooring fromMs down
    // to a whole day first and walking forward by days. The ceil here is load-bearing: flooring
    // to a day discards the sub-day part of fromMs, so whenever fromMs's own day was already a
    // Monday, that approach returned that Monday's midnight even when fromMs was, say, that
    // Monday afternoon — a boundary *before* fromMs. Rounding up in millisecond space instead
    // guarantees the result is always >= fromMs, just like the day branch above and like the
    // "starts at or after the window start" contract every caller of this function relies on.
    return MONDAY_EPOCH_OFFSET_MS + Math.ceil((fromMs - MONDAY_EPOCH_OFFSET_MS) / WEEK_MS) * WEEK_MS
  })()

  const boundaries: number[] = []
  for (let t = firstBoundary; t <= toMs; t += bucketMs) {
    boundaries.push(t)
  }

  // Thin by an integer stride so every surviving tick is still one of the real boundaries above
  // — never an arbitrary evenly-spaced instant that happens to fall mid-bucket. `Math.max(1, …)`
  // guards a non-positive `maxTicks` from producing a zero or negative stride.
  const stride = Math.max(1, Math.ceil(boundaries.length / Math.max(maxTicks, 1)))
  return boundaries.filter((_, index) => index % stride === 0)
}
