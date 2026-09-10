/**
 * Pure chart-data transforms for the `/insights` page (R5 "Insight", Task 10 fix round 1).
 *
 * `buildCompositionSlices` and `buildSpendGroups` used to sit inline in
 * `app/pages/insights/index.vue`'s own `<script setup>`. The vitest harness for this milestone
 * (`vitest.config.ts`) runs under a plain `node` environment with no Vue runtime, so a `.vue`
 * single-file component cannot be imported by a spec at all — which meant the Other-merge fold and
 * the currency/unknown-location fold, the two most consequential pieces of logic in Task 10, had no
 * automated protection whatsoever. Extracted here for the same reason Task 5 pulled
 * `donutGeometry.ts`/`barGeometry.ts` out of their components (see `e296e10`, "keep the chart
 * geometry helpers out of the component registry"): `describe`/`it` against an exported function,
 * no component mount required. See `tests/unit/insightsCharts.spec.ts`.
 *
 * Lives in `~/utils/`, never `~/components/`: Nuxt scans `app/components/` for `.ts` files as well
 * as `.vue` ones and registers whatever it finds as a component — exactly what `e296e10` had to
 * undo once already for `barGeometry.ts`/`donutGeometry.ts` themselves.
 *
 * Both functions take their translated strings as parameters — `formatCategory` and `t` below —
 * rather than calling `useI18n()`/`useEnumLabel()` internally. That is what keeps this module
 * importable under the node harness, and mirrors how `toBadgeViews` (Task 23) is specified: a
 * `translate`-shaped parameter, not a runtime composable reached for from inside the pure module.
 */
import type { ProductCategory } from '~/types/enums'
import type { InventoryCompositionResponse, SpendByLocationResponse } from '~/types/insights'
import { OTHER_THRESHOLD_RATIO } from '~/utils/chart/donutGeometry'

// --- Inventory composition (donut) -------------------------------------------------------------

export interface DonutSliceItem { key: string, label: string, value: number }

/**
 * `ChartDonut` merges any slice under 2% of the total into its OWN new trailing "other" slice — it
 * cannot absorb into one already in its input (see that component's own module comment). The
 * backend's `otherCount` is already the single "not individually listed" bucket, so passing it
 * straight through as one more slice would, whenever a ranked slice is independently under 2%,
 * produce a second, separately synthesized "other" wedge alongside it — two wedges both labelled
 * "Other". This folds any ranked slice that would trip that same threshold into `otherCount`
 * itself first, so at most one "other" bucket ever reaches `ChartDonut`.
 *
 * The threshold is `OTHER_THRESHOLD_RATIO`, imported from `donutGeometry.ts` rather than repeated
 * here — it has to match that module's own fold exactly: a looser threshold here would leave a
 * slice `ChartDonut` still folds away on its own, which is harmless (it would only fold into this
 * page's already-merged "other" a second time) but pointless; a tighter one could keep a slice out
 * of `ChartDonut`'s own merge that this page had already folded away, shrinking the ranked list for
 * no reason.
 */
export const buildCompositionSlices = (
  data: InventoryCompositionResponse,
  formatCategory: (category: ProductCategory) => string,
  t: (key: string) => string
): DonutSliceItem[] => {
  const total = data.totalCount
  if (total <= 0) return []

  const kept: DonutSliceItem[] = []
  let mergedOther = data.otherCount

  for (const slice of data.slices) {
    if (slice.count / total < OTHER_THRESHOLD_RATIO) {
      mergedOther += slice.count
    } else {
      kept.push({ key: String(slice.category), label: formatCategory(slice.category), value: slice.count })
    }
  }

  // Always last, so it renders as the trailing wedge whether ChartDonut keeps it as-is (its own
  // share is >= 2%) or folds it again (by then the only "small" slice left, since every other
  // small one was already absorbed above) — see this function's own doc comment.
  if (mergedOther > 0) {
    kept.push({ key: 'other', label: t('chart.donut.otherLabel'), value: mergedOther })
  }

  return kept
}

// --- Spend by location (bar, one per currency) -------------------------------------------------

export interface SpendBarItem { key: string, label: string, value: number }
export interface SpendCurrencyGroup { currencyCode: string, bars: SpendBarItem[] }

/**
 * One `ChartBar` per currency actually present — never one chart mixing currencies. Spend must
 * never be summed or compared across currencies (this milestone has no exchange rate to convert
 * with), and `ChartBar` takes a single `valueFormatter` for the whole chart with no per-bar
 * context, so it has no way to format two bars in two different currencies correctly on one chart
 * anyway. A location with nothing in a given currency simply contributes no bar to that currency's
 * chart, rather than a synthesized zero — a location whose `spendByCurrency` is entirely empty
 * (only unpriced purchases: it still shows up in `data.locations` with its own `itemCount`) is the
 * extreme case of this and, the same way, contributes no bar to any currency's chart at all — an
 * empty `spendByCurrency` means no *priced* purchases, which this milestone treats as different
 * from a real zero.
 */
export const buildSpendGroups = (
  data: SpendByLocationResponse,
  t: (key: string) => string
): SpendCurrencyGroup[] => {
  const currencyCodes = new Set<string>()
  for (const location of data.locations) {
    for (const currencyCode of Object.keys(location.spendByCurrency)) currencyCodes.add(currencyCode)
  }

  return [...currencyCodes].sort().map(currencyCode => ({
    currencyCode,
    bars: data.locations
      .filter(location => location.spendByCurrency[currencyCode] !== undefined)
      .map(location => ({
        key: location.shoppingLocationPublicId ?? 'unknown',
        label: location.shoppingLocationPublicId === null ? t('insights.spend.unknownLocation') : location.locationName,
        value: location.spendByCurrency[currencyCode]!
      }))
      .sort((a, b) => b.value - a.value)
  }))
}
