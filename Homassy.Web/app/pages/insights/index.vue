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

      <!-- #109. Its own fetch and its own loading flag, like every card above it: the leaderboard
           and the streaks come from one response, so they share one, and a slow scoreboard never
           blanks the charts. -->
      <InsightsLeaderboardCard
        :members="scoreboard?.members ?? []"
        :loading="scoreboardLoading"
        :period-start="scoreboard?.periodStart"
        :period-end="scoreboard?.periodEnd"
      />

      <InsightsStreakCard
        :no-expiry="scoreboard?.noExpiryStreak ?? EMPTY_STREAK"
        :list-cleared="scoreboard?.listClearedStreak ?? EMPTY_STREAK"
        :loading="scoreboardLoading"
      />

      <div class="md:col-span-2">
        <InsightsBadgeGrid :badges="badges" :loading="badgesLoading" />
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
import type {
  BadgeStateDto,
  ConsumptionSeriesResponse,
  FamilyScoreboardResponse,
  InventoryCompositionResponse,
  ScoreboardWindowDays,
  SpendByLocationResponse,
  StreakInfo
} from '~/types/insights'
import { formatCurrency } from '~/utils/chart/format'
import { buildCompositionSlices, buildSpendGroups } from '~/utils/insightsCharts'
import type { DonutSliceItem, SpendCurrencyGroup } from '~/utils/insightsCharts'

definePageMeta({ layout: 'auth' })

const { t, locale } = useI18n()
const { formatProductCategory } = useEnumLabel()
const { getInventoryComposition, getConsumption, getSpendByLocation, getScoreboard, getBadges } = useInsightsApi()

usePageHeader(() => ({
  icon: 'i-lucide-bar-chart-3',
  title: t('insights.title')
}))

// --- Inventory composition (donut) -------------------------------------------------------------

const compositionResponse = ref<InventoryCompositionResponse | null>(null)
const compositionLoading = ref(true)

/**
 * The Other-merge fold — any ranked slice under `OTHER_THRESHOLD_RATIO` of the total gets folded
 * into the backend's `otherCount` before this reaches `ChartDonut`, so at most one "other" bucket
 * ever reaches it — now lives in `~/utils/insightsCharts.ts` as `buildCompositionSlices`, a plain
 * function the vitest harness can import without a Vue runtime. It imports the same
 * `OTHER_THRESHOLD_RATIO` this page used to mirror by value, straight from `donutGeometry.ts`, so
 * the two folds can no longer drift apart. See that module's own comment and
 * `tests/unit/insightsCharts.spec.ts`.
 */
const compositionSlices = computed<DonutSliceItem[]>(() =>
  compositionResponse.value ? buildCompositionSlices(compositionResponse.value, formatProductCategory, t) : [])

const loadComposition = async (): Promise<void> => {
  compositionLoading.value = true
  try {
    const response = await getInventoryComposition()
    if (response.success && response.data) compositionResponse.value = response.data
  } catch (error) {
    console.error('Failed to load inventory composition:', error)
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
  } catch (error) {
    console.error('Failed to load consumption:', error)
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

/**
 * The per-currency fold — one `ChartBar` per currency actually present, spend never summed or
 * compared across currencies, and the unknown-location label for a null
 * `shoppingLocationPublicId` — now lives in `~/utils/insightsCharts.ts` as `buildSpendGroups`, a
 * plain function the vitest harness can import without a Vue runtime. See that module's own
 * comment and `tests/unit/insightsCharts.spec.ts`.
 */
const spendGroups = computed<SpendCurrencyGroup[]>(() =>
  spendResponse.value ? buildSpendGroups(spendResponse.value, t) : [])

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
  } catch (error) {
    console.error('Failed to load spend by location:', error)
  } finally {
    spendLoading.value = false
  }
}

// --- Scoreboard: leaderboard + household streaks (#109) -----------------------------------------

// The window the leaderboard covers. A named constant rather than an inlined literal, and
// deliberately without a control of its own for now: Task 22 asks for the cards, not a third
// segmented control on a page that already has one.
const SCOREBOARD_WINDOW_DAYS: ScoreboardWindowDays = 30

/** What the streak card renders before the response lands, so it never receives undefined. */
const EMPTY_STREAK: StreakInfo = { current: 0, longest: 0 }

const scoreboard = ref<FamilyScoreboardResponse | null>(null)
const scoreboardLoading = ref(true)

const loadScoreboard = async (): Promise<void> => {
  scoreboardLoading.value = true
  try {
    const response = await getScoreboard(SCOREBOARD_WINDOW_DAYS)
    if (response.success && response.data) scoreboard.value = response.data
  } catch (error) {
    console.error('Failed to load the family scoreboard:', error)
  } finally {
    scoreboardLoading.value = false
  }
}

// --- Badges (#109) --------------------------------------------------------------------------------

const badges = ref<BadgeStateDto[]>([])
const badgesLoading = ref(true)

/**
 * Fetched exactly once per page visit, from onMounted below — never on a watcher, a poll or a
 * retry. The request has a side effect: the server records any threshold just crossed and reports
 * `justUnlocked` on that one response, so a second speculative call would consume an unlock the
 * user never gets to see.
 */
const loadBadges = async (): Promise<void> => {
  badgesLoading.value = true
  try {
    const response = await getBadges()
    if (response.success && response.data) badges.value = response.data.badges
  } catch (error) {
    console.error('Failed to load badges:', error)
  } finally {
    badgesLoading.value = false
  }
}

// --- Initial load ---------------------------------------------------------------------------------

onMounted(() => {
  void loadComposition()
  void loadConsumption()
  void loadSpend()
  void loadScoreboard()
  void loadBadges()
})
</script>
