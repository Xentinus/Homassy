<template>
  <div class="space-y-3">
    <!-- Loading: one skeleton in the exact box a chart will occupy, from ChartCard's own
         aspect-ratio reservation - so the drawer does not shift when the data lands. -->
    <ChartLine
      v-if="loading"
      :series="[]"
      :bucket="AXIS_BUCKET"
      :title="$t('price.history.title')"
      :loading="true"
    />

    <!-- No priced purchase in the window. Its own message rather than ChartCard's generic
         "no data": the reason is specific and actionable (record a price next time), and a
         product with purchases that simply carry no price is the common case here. -->
    <UCard v-else-if="groupsToRender.length === 0">
      <template #header>
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-trending-up" class="h-5 w-5 shrink-0 text-dimmed" />
          <h2 class="truncate text-base font-semibold text-highlighted leading-tight">
            {{ $t('price.history.title') }}
          </h2>
        </div>
      </template>
      <p class="text-sm text-muted">{{ $t('price.history.empty') }}</p>
    </UCard>

    <template v-else>
      <!-- The currency strip exists only for a household that actually shops in more than one
           currency. A single-currency family never sees chrome for a problem they do not have. -->
      <UTabs
        v-if="currencies.length > 1"
        :items="currencyTabs"
        :model-value="selectedCurrency"
        @update:model-value="value => selectedCurrency = String(value)"
      />

      <!-- The badge belongs to exactly one currency and one basis (the server picks the dominant
           pair), so it is shown only while that currency is the one on screen - never re-labelled
           under a currency it was not paid in. -->
      <PriceBestPriceBadge
        v-if="badge"
        :best="badge.best"
        :basis-label="badge.basisLabel"
        :above-average-ratio="history?.latestAboveAverageRatio ?? null"
      />

      <div v-for="group in groupsToRender" :key="group.seriesKey" class="space-y-1.5">
        <ChartLine
          :series="seriesFor(group)"
          :bucket="AXIS_BUCKET"
          :title="basisLabelFor(group)"
          :value-formatter="valueFormatter"
        />
        <!-- A countable basis is comparable within itself but says nothing about weight or
             volume, so the label reads "per pack" and this line says why. Never "per kg". -->
        <p v-if="!group.normalized" class="px-1 text-sm text-muted">
          {{ $t('price.basis.packageNote') }}
        </p>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
/**
 * One product's purchase price history (#128): a per-shop line chart of the price per canonical
 * unit, one section per comparison basis, with a currency strip above it and the best known price
 * as a badge.
 *
 * Presentational. The drawer that hosts it owns the fetch and hands the response in, exactly as it
 * does for `ProductHistoryList` - so this component has no loading logic of its own beyond
 * forwarding the flag, and re-renders on a refetch without owning any request state.
 *
 * Three rules from the API are honoured here rather than re-decided:
 *
 * - **Currencies are never mixed.** `byCurrency` may hold several; one is shown at a time and
 *   nothing on screen combines two. There is no conversion anywhere in this milestone.
 * - **Bases are never blended.** Each `PriceBasisGroup` gets its own chart with its own y axis,
 *   because a price per kilogram and a price per pack are not points on one scale.
 * - **`normalized: false` is labelled from the unit itself** ("per pack", "per piece") and carries
 *   a line saying the comparison is per package. The flag is the whole reason the server bothers
 *   to keep those groups apart, so it must reach the label.
 *
 * The unit label comes from `enums.unit.*`, keyed by the unit's numeric enum value - which is
 * exactly how `canonicalUnit` arrives on the wire, so no mapping table is needed on this side.
 */
import { computed, ref, watch } from 'vue'
import { formatCurrency } from '~/utils/chart/format'
import type { PriceBasisGroup, PriceHistoryResponse } from '~/types/insights'

const props = defineProps<{
  history: PriceHistoryResponse | null
  loading?: boolean
}>()

const { t, locale } = useI18n()

/**
 * What `ChartLine` labels its x axis by. It does not bucket the data - the points are individual
 * purchases at whatever irregular dates they happened - it only decides where the ticks land and
 * how they are named. 'week' rather than 'day' because this chart's windows are 90 to 365 days
 * long: `formatAxisDate` names a day tick with its weekday ("júl. 13., H"), and eight of those
 * across a three-month axis collide into an unreadable smear, while a week tick is the date alone.
 */
const AXIS_BUCKET = 'week' as const

/** The "unknown location" bucket's series key - `shoppingLocationPublicId` is null for it. */
const UNKNOWN_SHOP_KEY = 'unknown'

const currencies = computed(() => Object.keys(props.history?.byCurrency ?? {}).sort())

const currencyTabs = computed(() => currencies.value.map(currency => ({
  value: currency,
  label: currency.toUpperCase()
})))

/**
 * Which currency's charts are on screen. Defaults to the one the best known price was paid in -
 * the server already picked that as the currency with the most data points, so the badge and the
 * chart below it agree on the first render without this component repeating that ranking.
 */
const selectedCurrency = ref('')

watch(
  () => props.history,
  (history) => {
    const available = Object.keys(history?.byCurrency ?? {})
    if (available.length === 0) {
      selectedCurrency.value = ''
      return
    }
    // Keep the viewer's own pick across a refetch, as long as it still exists.
    if (available.includes(selectedCurrency.value)) return
    selectedCurrency.value = history?.bestKnown?.currency && available.includes(history.bestKnown.currency)
      ? history.bestKnown.currency
      : available.slice().sort()[0]!
  },
  { immediate: true }
)

const groupsToRender = computed<PriceBasisGroup[]>(() =>
  props.history?.byCurrency[selectedCurrency.value] ?? [])

const unitLabel = (unit: number): string => t(`enums.unit.${unit}`)

const basisLabelFor = (group: PriceBasisGroup): string =>
  t('price.basis.perUnit', { unit: unitLabel(group.canonicalUnit) })

const valueFormatter = computed(() => {
  const currency = selectedCurrency.value
  return (value: number): string => formatCurrency(value, currency, locale.value)
})

/**
 * A shop's label. The "unknown location" bucket arrives with the server's own English literal for
 * a name (there is no location row to take one from), so it is replaced with the localized string
 * here - the same substitution `insightsCharts.ts` makes for the spend chart, keyed off the null
 * public id rather than off matching that literal.
 */
const shopLabel = (locationPublicId: string | null, locationName: string): string =>
  locationPublicId === null ? t('insights.spend.unknownLocation') : locationName

const seriesFor = (group: PriceBasisGroup) => group.shops.map(shop => ({
  key: shop.shoppingLocationPublicId ?? UNKNOWN_SHOP_KEY,
  label: shopLabel(shop.shoppingLocationPublicId, shop.locationName),
  points: shop.points.map(point => ({ t: Date.parse(point.purchasedAt), v: point.unitPrice }))
}))

/**
 * The badge, plus the basis label its `seriesKey` names - resolved from the groups of the currency
 * it was actually paid in, not from whichever currency happens to be selected. Absent while
 * another currency is on screen, so a per-litre price in forints is never captioned under euros.
 */
const badge = computed(() => {
  const best = props.history?.bestKnown
  if (!best || best.currency !== selectedCurrency.value) return null

  const group = groupsToRender.value.find(candidate => candidate.seriesKey === best.seriesKey)
  if (!group) return null

  return { best, basisLabel: basisLabelFor(group) }
})
</script>
