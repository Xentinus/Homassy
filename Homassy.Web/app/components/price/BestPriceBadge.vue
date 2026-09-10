<template>
  <div class="rounded-lg bg-elevated px-3 py-2">
    <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
      <UIcon name="i-lucide-tag" class="h-4 w-4 shrink-0 text-primary" />
      <span class="text-sm font-medium text-muted">{{ $t('price.best.title') }}</span>
      <span class="text-sm font-semibold text-highlighted">{{ formattedAmount }}</span>
      <span class="text-sm text-muted">{{ basisLabel }}</span>
      <span class="text-sm text-muted">{{ $t('price.best.at', { shop: shopLabel }) }}</span>
      <RelativeTime :date="best.at" class="text-sm text-dimmed" />
    </div>

    <!-- Informational, never an error colour: paying more than usual is a fact worth knowing, not
         a mistake the family made. `text-muted` + an info icon, phrased as an observation. -->
    <p v-if="showAboveAverageHint" class="mt-1.5 flex items-center gap-1.5 text-sm text-muted">
      <UIcon name="i-lucide-info" class="h-4 w-4 shrink-0" />
      {{ $t('price.aboveAverage') }}
    </p>
  </div>
</template>

<script setup lang="ts">
/**
 * The best known price for one product (#128): what the household paid, per unit, where, and how
 * long ago — with an optional "more than you usually pay" line.
 *
 * Deliberately dumb. It renders the `BestKnownPrice` it is handed and formats the amount; it never
 * fetches, and it never works out which basis the amount is per — `basisLabel` comes in already
 * translated from `PriceHistoryCard`, which is the component that holds the groups (a
 * `BestKnownPrice` carries only the machine-readable `seriesKey`, not the unit it names).
 *
 * Auto-imports as `<PriceBestPriceBadge>`, not `<BestPriceBadge>`: Nuxt prefixes a component with
 * its directory and only collapses the prefix when the filename already starts with it (which is
 * why `price/PriceHistoryCard.vue` is plain `<PriceHistoryCard>`). The filename is the one the task
 * specified, so the call site carries the prefixed name rather than the file being renamed to suit
 * the convention.
 *
 * The amount goes through the same `formatCurrency` every other price in the milestone uses, with
 * the currency taken from the payload — `best.currency` is the enum's name (`"Huf"`), a
 * well-formed code `Intl.NumberFormat` accepts, and never a number the client would have to look
 * up in a table of its own.
 */
import { computed } from 'vue'
import { formatCurrency } from '~/utils/chart/format'
import type { BestKnownPrice } from '~/types/insights'

/**
 * How far above its own average the newest purchase has to be before the hint appears. The server
 * deliberately sends the raw ratio rather than a boolean, so this margin is a client decision:
 * 10% is above the noise of ordinary price movement without being so rare that the line never
 * shows.
 */
const ABOVE_AVERAGE_HINT_RATIO = 1.1

const props = defineProps<{
  best: BestKnownPrice
  /** Already-translated basis label, e.g. "per l" — see the module comment. */
  basisLabel: string
  /** `PriceHistoryResponse.latestAboveAverageRatio`, straight through. */
  aboveAverageRatio: number | null
}>()

const { t, locale } = useI18n()

/**
 * The "unknown location" bucket carries the server's own English literal as its name, so it is
 * swapped for the localized string - keyed off the null public id, exactly as PriceHistoryCard and
 * insightsCharts.ts do it.
 */
const shopLabel = computed(() =>
  props.best.shoppingLocationPublicId === null ? t('insights.spend.unknownLocation') : props.best.locationName)

const formattedAmount = computed(() => formatCurrency(props.best.unitPrice, props.best.currency, locale.value))

const showAboveAverageHint = computed(() =>
  props.aboveAverageRatio !== null && props.aboveAverageRatio > ABOVE_AVERAGE_HINT_RATIO)
</script>
