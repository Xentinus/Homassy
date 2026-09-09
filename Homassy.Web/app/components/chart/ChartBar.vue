<template>
  <ChartCard :title="title" :loading="loading" :legend="legend" :table-headers="tableHeaders" :table-rows="tableRows" :aspect-ratio="1.6">
    <svg viewBox="0 0 320 200" class="w-full h-auto" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
      <g class="text-muted">
        <line
          v-for="tick in xTickMarks"
          :key="`grid-${tick.value}`"
          :x1="tick.x" :x2="tick.x"
          :y1="PLOT.top" :y2="PLOT.height - PLOT.bottom"
          stroke="currentColor" stroke-width="0.5" class="opacity-40"
        />
      </g>

      <g v-for="bar in barMarks" :key="bar.key">
        <text
          :x="PLOT.left - 6" :y="bar.y"
          text-anchor="end" dominant-baseline="middle" font-size="8"
          fill="currentColor" class="text-muted"
        >{{ bar.label }}</text>

        <line
          :x1="PLOT.left" :x2="bar.endX"
          :y1="bar.y" :y2="bar.y"
          :stroke-width="bar.strokeWidth"
          pathLength="1"
          class="chart-draw stroke-[var(--bar-light)] dark:stroke-[var(--bar-dark)]"
          :style="{ '--chart-draw-length': 1, '--bar-light': bar.colorLight, '--bar-dark': bar.colorDark }"
        />

        <text
          v-if="bar.inside"
          :x="bar.endX - 4" :y="bar.y"
          text-anchor="end" dominant-baseline="middle" font-size="8"
          class="fill-[var(--ui-bg)]"
        >{{ bar.formattedValue }}</text>
        <text
          v-else
          :x="bar.endX + 4" :y="bar.y"
          text-anchor="start" dominant-baseline="middle" font-size="8"
          fill="currentColor" class="text-muted"
        >{{ bar.formattedValue }}</text>
      </g>
    </svg>
  </ChartCard>
</template>

<script setup lang="ts">
/**
 * A horizontal bar chart (R5 "Insight", Task 5) — e.g. top categories by spend or by quantity.
 * Wraps `ChartCard` for its header, loading/empty states and accessible fallback table; this
 * component only turns `bars` into pixel geometry and decides each value label's placement.
 *
 * Filed as `ChartBar.vue`, so it auto-imports cleanly as `<ChartBar>` — see `ChartDonut.vue`'s own
 * note on why (this filename starts with `Chart`, so Nuxt's directory-prefix collapsing applies; it
 * was originally filed as `BarChart.vue`, which auto-imported as the stuttering `<ChartBarChart>`
 * until this rename).
 *
 * `bandScale` slices the y-axis into one row per bar. The x-axis needs two separate domains built
 * from the same `xTicks`, both extracted into `./barGeometry` (`barXTicks`/`barXDomain`/
 * `barValueDomain`, thin wrappers around `niceTicks`) so this domain logic is unit-tested without
 * a Vue runtime — see that file's own module comment (the "why two domains" of it) and
 * `tests/unit/barGeometry.spec.ts`: `xDomain` (→ `xScale`) positions the reference gridlines and
 * follows `xTicks`' own first and last entry — never a hardcoded `0` — the same way `ChartLine.vue`'s
 * `yDomain` follows its own `yTicks`; a bar's own drawn length instead goes through `barScale`,
 * built from the always-zero-anchored `barValueDomain`, so it cannot be pulled off `PLOT.left` by
 * the axis domain widening below zero for an all-zero bar set. Everything here is a `computed` over
 * props and the fixed margin/viewBox constants below — no lifecycle hook, no measurement.
 *
 * Each bar is drawn as a thick `<line>`, not a `<rect>`, specifically so it can reuse `.chart-draw`
 * (`main.css`) — that class's own comment already describes it as tracing "a line/bar stroke",
 * i.e. this is the shape it was written for: the `pathLength="1"` trick grows the stroke from the
 * zero baseline outward, which is what a bar chart's own entrance motion should look like anyway
 * (`.chart-grow`'s scale-from-centre, built for a donut arc, would grow a bar in both directions
 * from its middle instead). Colour is the same static-class-plus-inline-`:style` pattern
 * `ChartCard`'s swatch, `ChartLine` and `ChartDonut` all use — never a script-resolved colour,
 * since the theme is unknown during SSR. Each bar gets its own colour from `seriesColors()`
 * (`~/utils/chart/series`), the same as a donut's slices, which is what makes this component's
 * legend meaningful rather than decorative.
 *
 * The value label sits inside the bar when the bar's own *scaled* length clears
 * `INSIDE_LABEL_MIN_WIDTH`, outside it otherwise — decided purely from that geometry, never from
 * measuring the rendered text (forbidden for this whole milestone; see the module doc on
 * `ChartCard.vue`). Inside, the label paints in `var(--ui-bg)` (the app's own card-background
 * token, not a new colour): every `MEMBER_COLORS` entry is already guaranteed >= 3:1 contrast
 * against its *own* theme's card background (`~/utils/memberColors`'s own comment), and WCAG
 * contrast is symmetric — so a bar's background clearing 3:1 against `--ui-bg` means `--ui-bg`
 * text on that same bar clears exactly the same 3:1 back, in either theme, with nothing new to
 * maintain. Outside the bar there is no colour to sit on top of, so the label just uses the
 * ordinary muted axis text colour.
 */
import { linearScale, bandScale } from '~/utils/chart/scale'
import { seriesColors } from '~/utils/chart/series'
import { formatCompact } from '~/utils/chart/format'
import { barValueDomain, barXDomain, barXTicks } from './barGeometry'

interface ChartBarProps {
  bars: { key: string; label: string; value: number }[]
  title: string
  loading?: boolean
  valueFormatter?: (v: number) => string
}

const props = defineProps<ChartBarProps>()

const { t, locale } = useI18n()

const PLOT = { left: 92, right: 10, top: 8, bottom: 8, width: 320, height: 200 }

/**
 * Minimum scaled bar length (viewBox units) for its value label to sit inside rather than outside.
 * An implementer's choice, not a spec value — like `scale.ts`'s `DEFAULT_TICK_COUNT` — picked by
 * eye against the 8-unit font size used for that label, not derived from any measurement.
 */
const INSIDE_LABEL_MIN_WIDTH = 34

const formatValue = computed(() => props.valueFormatter ?? ((v: number) => formatCompact(v, locale.value)))

const yBand = computed(() => bandScale(props.bars.map(bar => bar.key), [PLOT.top, PLOT.height - PLOT.bottom], 0.3))

const xTicks = computed<number[]>(() => barXTicks(props.bars))

// The axis domain — where the reference gridlines land — follows `xTicks`' own bounds; see
// `barXDomain`'s own comment in `./barGeometry` for why this can no longer be hardcoded at `0`.
const xDomain = computed<[number, number]>(() => barXDomain(xTicks.value))
const xScale = computed(() => linearScale(xDomain.value, [PLOT.left, PLOT.width - PLOT.right]))

// A bar's own drawn length is measured against a *separate*, always-zero-anchored domain —
// `barValueDomain`, not `xDomain` — so `niceTicks` widening the axis below zero for an all-zero
// bar set can never pull a bar's own zero point away from `PLOT.left`. The two domains (and so
// `xScale`/`barScale`) agree exactly for every real, non-all-zero bar set.
const barScale = computed(() => linearScale(barValueDomain(xTicks.value), [PLOT.left, PLOT.width - PLOT.right]))

const xTickMarks = computed(() => xTicks.value.map(value => ({ value, x: xScale.value(value) })))

const colorByKey = computed(() => seriesColors(props.bars.map(bar => bar.key)))

const barMarks = computed(() => props.bars.map((bar) => {
  const color = colorByKey.value.get(bar.key)!
  const y = yBand.value.position(bar.key) + yBand.value.bandwidth / 2
  const endX = barScale.value(bar.value)
  const scaledWidth = endX - PLOT.left
  return {
    key: bar.key,
    label: bar.label,
    formattedValue: formatValue.value(bar.value),
    y,
    endX,
    strokeWidth: yBand.value.bandwidth,
    inside: scaledWidth >= INSIDE_LABEL_MIN_WIDTH,
    colorLight: color.light,
    colorDark: color.dark
  }
}))

const legend = computed(() => barMarks.value.map(bar => ({
  key: bar.key,
  label: bar.label,
  colorLight: bar.colorLight,
  colorDark: bar.colorDark
})))

const tableHeaders = computed(() => [t('chart.categoryHeader'), t('chart.valueHeader')])

const tableRows = computed(() => props.bars.map(bar => ({
  label: bar.label,
  values: [formatValue.value(bar.value)]
})))
</script>
