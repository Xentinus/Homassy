<template>
  <div class="px-4 sm:px-8 lg:px-14 pb-6">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <ChartDonut
        :slices="compositionSlices"
        :title="t('insights.composition.title')"
        :loading="compositionLoading"
      />

      <div class="space-y-2">
        <div class="flex justify-end">
          <div class="inline-flex items-center gap-0.5 rounded-lg border border-default p-0.5">
            <button
              v-for="option in consumptionDayOptions"
              :key="option.value"
              type="button"
              class="px-2.5 py-1 rounded-md text-sm font-medium transition-colors"
              :class="consumptionDays === option.value
                ? 'bg-primary-500 text-white'
                : 'text-muted hover:text-default'"
              :aria-pressed="consumptionDays === option.value"
              @click="consumptionDays = option.value"
            >
              {{ option.label }}
            </button>
          </div>
        </div>
        <ChartLine
          :series="consumptionSeries"
          :bucket="consumptionBucket"
          :title="t('insights.consumption.title')"
          :loading="consumptionLoading"
        />
      </div>

      <div v-for="group in spendGroupsToRender" :key="group.currencyCode || 'empty'">
        <ChartBar
          :bars="group.bars"
          :title="spendCardTitle(group.currencyCode)"
          :loading="spendLoading"
          :value-formatter="formatSpendValue(group.currencyCode)"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The `/insights` page (R5 "Insight", issue #99) — where the inventory-composition, consumption
 * and spend-by-location endpoints (Tasks 6-9) become three charts. #109 adds a leaderboard,
 * streaks and badges to this same page later, which is why each section below fetches and
 * renders independently: a new card is another grid cell plus its own loading ref, never a
 * change to how the existing three work.
 *
 * Every chart owns its own `ref<boolean>` loading flag and its own fetch function, fired
 * together from `onMounted` (never awaited in sequence), so one slow query never blanks the
 * other two.
 *
 * Page identity (icon + title) lives in the persistent AppHeader via `usePageHeader`, matching
 * every other page under the `auth` layout — no separate in-body heading.
 */
import { ref, computed, watch, onMounted } from 'vue'
import type { ConsumptionSeriesResponse, InventoryCompositionResponse, SpendByLocationResponse } from '~/types/insights'
import { formatCurrency } from '~/utils/chart/format'

definePageMeta({ layout: 'auth' })

const { t, locale } = useI18n()
const { formatProductCategory } = useEnumLabel()
const { getInventoryComposition, getConsumption, getSpendByLocation } = useInsightsApi()

usePageHeader(() => ({
  icon: 'i-lucide-bar-chart-3',
  title: t('insights.title')
}))

// --- Inventory composition (donut) -------------------------------------------------------------

const compositionResponse = ref<InventoryCompositionResponse | null>(null)
const compositionLoading = ref(true)

interface DonutSliceItem { key: string, label: string, value: number }

/**
 * `ChartDonut` merges any slice under 2% of the total into its OWN new trailing "other" slice —
 * it cannot absorb into one already in its input (see that component's own module comment). The
 * backend's `otherCount` is already the single "not individually listed" bucket, so passing it
 * straight through as one more slice would, whenever a ranked slice is independently under 2%,
 * produce a second, separately synthesized "other" wedge alongside it — two wedges both labelled
 * "Other". This folds any ranked slice that would trip that same threshold into `otherCount`
 * itself first, so at most one "other" bucket ever reaches `ChartDonut`.
 *
 * 0.02 mirrors `OTHER_THRESHOLD_RATIO` in `~/utils/chart/donutGeometry.ts` — not exported, and
 * that file is closed for this task, so it is repeated here rather than imported. It has to
 * match that value: a looser threshold here would leave a slice `ChartDonut` still folds away on
 * its own, which is harmless (it would only fold into this page's already-merged "other" a
 * second time) but pointless; a tighter one could keep a slice out of `ChartDonut`'s own merge
 * that this page had already folded away, shrinking the ranked list for no reason.
 */
const OTHER_MERGE_THRESHOLD = 0.02

const buildCompositionSlices = (data: InventoryCompositionResponse): DonutSliceItem[] => {
  const total = data.totalCount
  if (total <= 0) return []

  const kept: DonutSliceItem[] = []
  let mergedOther = data.otherCount

  for (const slice of data.slices) {
    if (slice.count / total < OTHER_MERGE_THRESHOLD) {
      mergedOther += slice.count
    } else {
      kept.push({ key: String(slice.category), label: formatProductCategory(slice.category), value: slice.count })
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

const compositionSlices = computed<DonutSliceItem[]>(() =>
  compositionResponse.value ? buildCompositionSlices(compositionResponse.value) : [])

const loadComposition = async (): Promise<void> => {
  compositionLoading.value = true
  try {
    const response = await getInventoryComposition()
    if (response.success && response.data) compositionResponse.value = response.data
  } finally {
    compositionLoading.value = false
  }
}

// --- Consumption (line) --------------------------------------------------------------------------

const consumptionResponse = ref<ConsumptionSeriesResponse | null>(null)
const consumptionLoading = ref(true)
const consumptionDays = ref<30 | 90>(30)

// A day-granular 90-point line would crowd the axis, so the coarser bucket comes along with the
// wider window automatically. Task 10's brief scopes the segmented control to `days` alone, with
// no separate control for `bucket`.
const consumptionBucket = computed<'day' | 'week'>(() => (consumptionDays.value === 30 ? 'day' : 'week'))

const consumptionDayOptions = computed<{ value: 30 | 90, label: string }[]>(() => [
  { value: 30, label: t('insights.consumption.last30Days') },
  { value: 90, label: t('insights.consumption.last90Days') }
])

/**
 * `DateOnly` arrives as a plain "yyyy-MM-dd" calendar date — the server has already done the one
 * and only timezone conversion (see `ConsumptionSeriesResponse.timeZoneId`). Built with
 * `Date.UTC`, never `new Date(y, m, d)`: the local constructor would reinterpret that calendar
 * date in the viewer's own offset and shift every point, and `formatAxisDate` pins `timeZone:
 * 'UTC'` specifically so its label still names the same day this produces.
 */
const parseDateOnlyUtc = (value: string): number => {
  const [year, month, day] = value.split('-').map(Number)
  return Date.UTC(year!, month! - 1, day!)
}

const consumptionSeries = computed(() => {
  if (!consumptionResponse.value) return []
  return [{
    key: 'consumption',
    label: t('insights.consumption.seriesLabel'),
    points: consumptionResponse.value.points.map(point => ({ t: parseDateOnlyUtc(point.bucket), v: point.value }))
  }]
})

const loadConsumption = async (): Promise<void> => {
  consumptionLoading.value = true
  try {
    const response = await getConsumption(consumptionDays.value, consumptionBucket.value)
    if (response.success && response.data) consumptionResponse.value = response.data
  } finally {
    consumptionLoading.value = false
  }
}

watch(consumptionDays, loadConsumption)

// --- Spend by location (bar, one per currency) ---------------------------------------------------

// No control for this in Task 10 — the brief's segmented control is scoped to the consumption
// chart only (see consumptionDayOptions above). A named constant, not an inlined literal, so a
// later task can wire up its own control without touching the fetch or shape logic below.
const SPEND_WINDOW_DAYS = 30

const spendResponse = ref<SpendByLocationResponse | null>(null)
const spendLoading = ref(true)

interface SpendBarItem { key: string, label: string, value: number }
interface SpendCurrencyGroup { currencyCode: string, bars: SpendBarItem[] }

/**
 * One `ChartBar` per currency actually present — never one chart mixing currencies. Spend must
 * never be summed or compared across currencies (this milestone has no exchange rate to convert
 * with), and `ChartBar` takes a single `valueFormatter` for the whole chart with no per-bar
 * context, so it has no way to format two bars in two different currencies correctly on one
 * chart anyway. A location with nothing in a given currency simply contributes no bar to that
 * currency's chart, rather than a synthesized zero — an empty `spendByCurrency` means no *priced*
 * purchases, which this milestone treats as different from a real zero.
 */
const buildSpendGroups = (data: SpendByLocationResponse): SpendCurrencyGroup[] => {
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

const spendGroups = computed<SpendCurrencyGroup[]>(() =>
  spendResponse.value ? buildSpendGroups(spendResponse.value) : [])

// Always at least one card, even with zero purchases in the window — an empty `spendGroups`
// would otherwise render no card at all instead of ChartCard's own empty state.
const spendGroupsToRender = computed<SpendCurrencyGroup[]>(() =>
  spendGroups.value.length > 0 ? spendGroups.value : [{ currencyCode: '', bars: [] }])

const spendCardTitle = (currencyCode: string): string =>
  currencyCode ? t('insights.spend.titleWithCurrency', { currency: currencyCode }) : t('insights.spend.title')

const formatSpendValue = (currencyCode: string) => {
  return (value: number): string => formatCurrency(value, currencyCode, locale.value)
}

const loadSpend = async (): Promise<void> => {
  spendLoading.value = true
  try {
    const response = await getSpendByLocation(SPEND_WINDOW_DAYS)
    if (response.success && response.data) spendResponse.value = response.data
  } finally {
    spendLoading.value = false
  }
}

// --- Initial load ---------------------------------------------------------------------------------

onMounted(() => {
  void loadComposition()
  void loadConsumption()
  void loadSpend()
})
</script>
