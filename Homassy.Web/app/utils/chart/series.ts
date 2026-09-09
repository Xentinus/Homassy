/**
 * Chart series colours for the hand-rolled SVG chart layer (R5 "Insight").
 *
 * Sibling to `scale.ts` and `path.ts` in spirit — pure, explicit return types, no semicolons — but
 * not import-free like them: this module exists specifically to bind a chart's series to
 * `MEMBER_COLORS` (`~/utils/memberColors`, R4's member-identity palette), which is the one import
 * this folder allows. Every one of those eight colours already clears 3:1 contrast against both
 * themes' card background (see that file's own comment), so reusing it here is what lets this
 * milestone add chart colour without auditing a single new hex. `ChartCard.vue` builds its legend
 * from `seriesColors`; `LineChart.vue`, `BarChart.vue` and `DonutChart.vue` colour their marks from
 * it.
 */

import { MEMBER_COLORS, type MemberColor } from '~/utils/memberColors'

// --- seriesColor -----------------------------------------------------------------------------
//
// One series's colour, picked by its position among the series drawn together — never by anything
// about the series itself.

/**
 * `%` alone is not quite "wrap": JavaScript's remainder keeps the sign of its left operand, so
 * `-1 % MEMBER_COLORS.length` is `-1`, not the last palette entry. A caller that derives `index`
 * from `Array.prototype.indexOf` (`-1` on a miss) would otherwise index the array out of bounds for
 * an `undefined` "colour" rather than a wrapped-around one. Adding the length back before the
 * second `%` is the standard fix, and costs nothing for the non-negative loop counters every
 * current caller actually passes.
 */
const paletteIndex = (index: number): number =>
  ((index % MEMBER_COLORS.length) + MEMBER_COLORS.length) % MEMBER_COLORS.length

/**
 * Deliberately an index, not the FNV hash `pickMemberColor` uses for a member's identity colour
 * (`memberColors.ts`). The two callers want opposite guarantees: a member must keep the same colour
 * everywhere in the app for as long as they exist — only a hash of something stable about them
 * (their id) gives that — while a chart plotting several series side by side must never assign two
 * of them the same colour, which a hash cannot promise; two unrelated shop names can land on the
 * same bucket by plain bad luck. An index into a fixed-length palette guarantees no collision for as
 * many series as the palette has entries, which is exactly what a legend needs and a hash cannot
 * supply. Resist "fixing" this into a hash; that would reintroduce the exact collision this function
 * exists to rule out.
 *
 * `seriesKey` does not otherwise influence the colour — only `index` does — but stays part of the
 * signature because every real caller already has both a series' key and its position (a series is
 * drawn *from* its key), so dropping it here would just make the call site carry it separately for
 * no benefit.
 */
export const seriesColor = (seriesKey: string, index: number): MemberColor =>
  MEMBER_COLORS[paletteIndex(index)]!

// --- seriesColors ----------------------------------------------------------------------------
//
// One colour per series key, assigned in the given order. The shape a legend and a chart's marks
// both want: look a series up by its key, get back the same colour every other consumer of the
// same key list gets too.

export const seriesColors = (seriesKeys: readonly string[]): Map<string, MemberColor> => {
  const colors = new Map<string, MemberColor>()
  seriesKeys.forEach((key, index) => colors.set(key, seriesColor(key, index)))
  return colors
}
