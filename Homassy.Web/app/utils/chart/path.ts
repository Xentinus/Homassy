/**
 * SVG path-string builders for the hand-rolled chart layer (R5 "Insight").
 *
 * Sibling to `scale.ts` — same rules apply here: pure maths, no imports, and nothing touches
 * SVG/Vue/DOM directly. Later tasks in this milestone (`DonutChart.vue`, `LineChart.vue`) feed
 * scaled points into the functions below to get the `d` attribute of a `<path>`; this module never
 * renders anything itself, so a geometry bug is caught by the `node`-environment vitest spec long
 * before any chart is actually on screen (see `vitest.config.ts`).
 */

export interface Point { x: number; y: number }

// --- coordinate formatting (private) ------------------------------------------------------------
//
// Every path string below is assembled through these two helpers rather than interpolating numbers
// directly, so the rounding rule is enforced in exactly one place.

/**
 * Path coordinates are rounded to at most this many decimal places. These charts are server
 * rendered and then hydrated on the client; `Math.sin`/`Math.cos` (used by `polarPoint` below) are
 * not specified to be bit-identical across JS engines, so the "same" point computed during SSR and
 * during hydration can differ in a low decimal digit. Left alone, that turns into a real character
 * mismatch in the `d` string and Vue reports a hydration mismatch on a chart that looks completely
 * correct. Rounding to 3 decimals — far coarser than the drift — makes both sides agree byte for
 * byte, and keeps the strings short besides.
 */
const MAX_COORDINATE_DECIMALS = 3
const COORDINATE_ROUNDING_FACTOR = 10 ** MAX_COORDINATE_DECIMALS

const formatNumber = (value: number): string => {
  const rounded = Math.round(value * COORDINATE_ROUNDING_FACTOR) / COORDINATE_ROUNDING_FACTOR
  // Rounding a small negative value (e.g. -0.0001) toward zero produces -0, not 0. They are `===`
  // in JS but `String(-0)` is the text '-0' — a byte-level difference from the '0' the same spot
  // would format to when reached from the positive side, i.e. exactly the SSR/hydration mismatch
  // this whole function exists to prevent.
  return rounded === 0 ? '0' : String(rounded)
}

const formatPoint = (point: Point): string => `${formatNumber(point.x)},${formatNumber(point.y)}`

// --- polarPoint ----------------------------------------------------------------------------------
//
// Converts a (centre, radius, angle) triple into a cartesian point. `arcPath` builds every point it
// needs from this; chart components can also call it directly to place something other than the
// ring itself — a slice label, a hover target — at a specific spot on a donut.

/**
 * Angle 0 is twelve o'clock, increasing clockwise: `x = cx + r·sin(a)`, `y = cy − r·cos(a)`. In
 * SVG's y-down coordinate space that is the direction a donut chart visually fills in as its
 * categories accumulate. `arcPath` depends on this exact convention to pick its sweep flags below.
 */
export const polarPoint = (cx: number, cy: number, r: number, angleRad: number): Point => ({
  x: cx + r * Math.sin(angleRad),
  y: cy - r * Math.cos(angleRad)
})

// --- linePath ------------------------------------------------------------------------------------
//
// A polyline through every point, for a line chart's series.

export const linePath = (points: readonly Point[]): string => {
  // No points means no data for this window — render nothing rather than a bare 'M', which is not
  // a valid `d` value on its own and still paints a stray marker in some renderers.
  if (points.length === 0) return ''
  return points.map((point, index) => `${index === 0 ? 'M' : 'L'}${formatPoint(point)}`).join('')
}

// --- areaPath ------------------------------------------------------------------------------------
//
// The same polyline as `linePath`, closed down to a baseline (typically the chart's zero line) so
// it can be filled rather than just stroked.

export const areaPath = (points: readonly Point[], baselineY: number): string => {
  if (points.length === 0) return ''

  // Both indices are safe because of the length check above; the non-null assertions only tell the
  // compiler what that check already proved (this file runs under `noUncheckedIndexedAccess`).
  const first = points[0]!
  const last = points[points.length - 1]!
  const baseline = formatNumber(baselineY)

  return `${linePath(points)}L${formatNumber(last.x)},${baseline}L${formatNumber(first.x)},${baseline}Z`
}

// --- arcPath -------------------------------------------------------------------------------------
//
// One donut segment: an outer arc, a line inward to the inner radius, an inner arc back out to the
// start, closed. Tracing two radii instead of one is what turns a pie slice into a ring slice.

/** The large-arc-flag / sweep-flag pair SVG's `A` command takes; both are always 0 or 1. */
type ArcFlag = 0 | 1

/**
 * SVG's elliptical-arc command draws whichever of the two arcs joining its start and end point the
 * flags select — it has no way to say "go all the way around." Once the end point coincides
 * exactly with the start, the segment is zero-length and, per spec, renders nothing. A donut chart
 * legitimately needs a full ring (a single category holding 100% of the total), so that case can't
 * go through the single-arc code path below at all; `fullCircleArc` traces it as two exact
 * half-turns that meet at the far side of the circle instead of one arc asked to return to its own
 * start.
 */
const FULL_TURN_RAD = Math.PI * 2

/**
 * How close a sweep must be to a full turn to be routed through `fullCircleArc`. A donut chart's
 * caller typically derives `endRad` by summing each category's share of `2π`; that sum can land a
 * hair short of a true full turn through ordinary floating-point drift even when the shares add up
 * to exactly 100%. Left unhandled, that near-miss would take the single-arc path below instead, and
 * once its start and end points are rounded to 3 decimals (see `formatNumber`) they format as the
 * same coordinate anyway — landing right back in the zero-length-segment case this constant exists
 * to avoid, only now by accident. The tolerance is many orders of magnitude larger than realistic
 * float drift and many orders smaller than any gap a chart would actually intend to render, so it
 * only ever catches the former.
 */
const FULL_TURN_EPSILON_RAD = 1e-9

const isFullTurn = (sweepRad: number): boolean => sweepRad >= FULL_TURN_RAD - FULL_TURN_EPSILON_RAD

/** One `A` command tracing a circular (rx === ry) arc with no x-axis rotation. */
const arcCommand = (r: number, largeArcFlag: ArcFlag, sweepFlag: ArcFlag, to: Point): string =>
  `A${formatNumber(r)},${formatNumber(r)} 0 ${largeArcFlag} ${sweepFlag} ${formatPoint(to)}`

/**
 * A full circle of radius `r`, starting and ending at `fromRad`, traced as two half-turns rather
 * than one impossible 360° command (see `FULL_TURN_RAD` above). Each half spans exactly π, so the
 * two arcs the large-arc flag would otherwise pick between are the same length either way — 0 is
 * the conventional value for that case. `sweepFlag` alone decides the direction, and the closing
 * angle is computed as exactly `fromRad ± 2π` rather than reusing whatever `endRad` the caller
 * passed in, so the shape always closes precisely on its own start point regardless of how that
 * sum drifted.
 */
const fullCircleArc = (cx: number, cy: number, r: number, fromRad: number, sweepFlag: ArcFlag): string => {
  const direction = sweepFlag === 1 ? 1 : -1
  const midRad = fromRad + direction * Math.PI
  const closingRad = fromRad + direction * FULL_TURN_RAD
  return arcCommand(r, 0, sweepFlag, polarPoint(cx, cy, r, midRad)) +
    arcCommand(r, 0, sweepFlag, polarPoint(cx, cy, r, closingRad))
}

export const arcPath = (
  cx: number,
  cy: number,
  rOuter: number,
  rInner: number,
  startRad: number,
  endRad: number
): string => {
  const sweepRad = endRad - startRad
  // Shared by both boundaries on purpose: the ring slice is one wedge, so whether it counts as
  // "large" (more than half the circle) cannot differ between its outer and inner edge without the
  // two arcs disagreeing about which side of the wedge they bulge toward.
  const largeArcFlag: ArcFlag = sweepRad > Math.PI ? 1 : 0
  const fullTurn = isFullTurn(sweepRad)

  const outerStart = polarPoint(cx, cy, rOuter, startRad)
  const outerArc = fullTurn
    ? fullCircleArc(cx, cy, rOuter, startRad, 1)
    : arcCommand(rOuter, largeArcFlag, 1, polarPoint(cx, cy, rOuter, endRad))

  const innerAtEnd = polarPoint(cx, cy, rInner, endRad)
  const innerArc = fullTurn
    ? fullCircleArc(cx, cy, rInner, endRad, 0)
    : arcCommand(rInner, largeArcFlag, 0, polarPoint(cx, cy, rInner, startRad))

  return `M${formatPoint(outerStart)}${outerArc}L${formatPoint(innerAtEnd)}${innerArc}Z`
}
