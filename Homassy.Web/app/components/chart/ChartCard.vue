<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-3">
        <slot name="icon" />
        <div class="min-w-0 flex-1">
          <h2 class="truncate text-base font-semibold text-highlighted leading-tight">{{ title }}</h2>
          <p v-if="subtitle" class="truncate text-sm text-muted">{{ subtitle }}</p>
        </div>
      </div>
    </template>

    <div class="space-y-4">
      <!-- Legend. Colour is never the only distinction: every swatch carries its own label text,
           and the fallback table below repeats the same labels again. -->
      <div v-if="legend.length > 0" class="flex flex-wrap gap-2">
        <UBadge v-for="entry in legend" :key="entry.key" color="neutral" variant="soft" size="sm">
          <span class="flex items-center gap-1.5">
            <span class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ backgroundColor: entry.color }" />
            {{ entry.label }}
          </span>
        </UBadge>
      </div>

      <!-- Exactly one of: the loading skeleton, the empty state, or the chart itself (default slot). -->
      <div v-if="loading" class="h-48">
        <SkeletonCard :lines="0" :footer-lines="0" />
      </div>
      <div
        v-else-if="tableRows.length === 0"
        class="flex h-48 items-center justify-center text-center text-sm text-muted"
      >
        {{ emptyLabel || $t('chart.noData') }}
      </div>
      <div v-else>
        <slot />
      </div>

      <!--
        The accessible fallback table. Always in the markup — server-rendered, no v-if on `loading`,
        `import.meta.client` or a mounted flag — because this one element is what gives a no-JS
        visitor, the raw SSR HTML, and a screen reader the real numbers for every chart built on this
        shell (issue #99's "degrade to a table-like list"). Gating it on anything client-only would
        only satisfy that requirement after hydration, which defeats the point of it.
      -->
      <details class="border-t border-default pt-3" :open="isDetailsOpen" @toggle="onToggleDetails">
        <summary
          class="flex cursor-pointer list-none items-center gap-1.5 text-sm font-medium text-muted [&::-webkit-details-marker]:hidden"
        >
          <UIcon
            name="i-lucide-chevron-right"
            class="chart-details-chevron h-4 w-4 shrink-0"
            :class="{ 'rotate-90': isDetailsOpen }"
          />
          {{ isDetailsOpen ? $t('chart.hideData') : $t('chart.showData') }}
        </summary>

        <div class="mt-3 overflow-x-auto">
          <table class="w-full text-left text-sm">
            <caption class="sr-only">{{ $t('chart.dataTableCaption') }}</caption>
            <thead v-if="tableHeaders.length > 0">
              <tr class="border-b border-default">
                <th
                  v-for="(header, index) in tableHeaders"
                  :key="index"
                  scope="col"
                  class="py-1.5 pr-3 font-medium text-muted"
                >
                  {{ header }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="tableRows.length === 0">
                <td class="py-1.5 text-muted" :colspan="Math.max(tableHeaders.length, 1)">
                  {{ emptyLabel || $t('chart.noData') }}
                </td>
              </tr>
              <tr
                v-for="(row, rowIndex) in tableRows"
                :key="`${rowIndex}-${row.label}`"
                class="border-b border-default last:border-b-0"
              >
                <th scope="row" class="py-1.5 pr-3 text-left font-medium text-highlighted">{{ row.label }}</th>
                <td v-for="(value, valueIndex) in row.values" :key="valueIndex" class="py-1.5 pr-3 text-muted">
                  {{ value }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </details>
    </div>
  </UCard>
</template>

<script setup lang="ts">
/**
 * The one shell every hand-rolled SVG chart in the "Insight" milestone sits inside (R5, issue #99).
 * `DonutChart`/`LineChart`/`BarChart` (Task 5) each wrap themselves in this, passing their own `<svg>`
 * into the default slot plus a `legend` and `tableRows` derived from the same data the chart itself
 * plots — so the three of them share one header idiom, one loading/empty state, and — the reason
 * this component exists as its own file rather than being copy-pasted three times — exactly one
 * accessible fallback implementation instead of three slightly-different ones.
 *
 * Colour is a prop-in, `background-color` style-out passthrough: the `color` on each legend entry
 * already is the final CSS colour (a caller resolves it via `seriesColors()` in `~/utils/chart/series`
 * from `MEMBER_COLORS`), so this component never needs to know anything about the palette itself.
 */
import { ref } from 'vue'

interface ChartCardProps {
  title: string
  subtitle?: string
  loading?: boolean
  /** Legend entries; empty array hides the legend. */
  legend?: { key: string; label: string; color: string }[]
  /** Rows for the accessible fallback table. Empty array renders the empty state. */
  tableRows?: { label: string; values: string[] }[]
  tableHeaders?: string[]
  emptyLabel?: string
}

withDefaults(defineProps<ChartCardProps>(), {
  subtitle: undefined,
  loading: false,
  legend: () => [],
  tableRows: () => [],
  tableHeaders: () => [],
  emptyLabel: undefined
})

/**
 * Mirrors the native `<details>`'s own open/closed state. Purely cosmetic — it only decides which
 * of the two translated labels the summary shows and which way the chevron points — never what is
 * in the DOM: the element above is bound with `:open`/`@toggle` rather than a `v-if`, so it starts
 * (and, with JS disabled, stays) exactly as valid without this ref ever changing.
 */
const isDetailsOpen = ref(false)

const onToggleDetails = (event: Event): void => {
  isDetailsOpen.value = (event.target as HTMLDetailsElement).open
}
</script>

<style scoped>
/* The chevron's direction is the information (open vs. closed) — restated in words by the
   showData/hideData label right next to it — and the rotation is decoration on top of that, reusing
   the app's own toggle-speed token rather than a one-off duration. Same approach as
   AggregatedActivityCard.vue's .expand-chevron and RealtimeConnectionBar.vue's .realtime-spin, and
   for the same reason: a blanket rule on the `rotate-90` utility would also strip transitions from
   unrelated call sites that use it. */
.chart-details-chevron {
  transition: transform var(--bubble-out) var(--bubble-ease-out);
}

@media (prefers-reduced-motion: reduce) {
  .chart-details-chevron {
    transition: none;
  }
}
</style>
