<template>
  <ChartCard :title="title" :loading="loading" :legend="legend" :table-headers="tableHeaders" :table-rows="tableRows" :aspect-ratio="1.6">
    <svg viewBox="0 0 320 200" class="w-full h-auto" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
      <g class="text-muted">
        <line
          v-for="tick in yTickMarks"
          :key="`grid-${tick.value}`"
          :x1="plotLeft" :x2="PLOT.width - PLOT.right"
          :y1="tick.y" :y2="tick.y"
          stroke="currentColor" stroke-width="0.5" class="opacity-40"
        />
        <text
          v-for="tick in yTickMarks"
          :key="`y-${tick.value}`"
          :x="plotLeft - 6" :y="tick.y"
          text-anchor="end" dominant-baseline="middle" font-size="8"
          fill="currentColor"
        >{{ tick.label }}</text>
        <text
          v-for="tick in xTickMarks"
          :key="`x-${tick.value}`"
          :x="tick.x" :y="PLOT.height - PLOT.bottom + 12"
          text-anchor="middle" font-size="8"
          fill="currentColor"
        >{{ tick.label }}</text>
      </g>

      <path
        v-for="line in seriesPaths"
        :key="line.key"
        :d="line.d"
        fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
        pathLength="1"
        class="chart-draw stroke-[var(--series-light)] dark:stroke-[var(--series-dark)]"
        :style="{ '--chart-draw-length': 1, '--series-light': line.colorLight, '--series-dark': line.colorDark }"
      />
    </svg>
  </ChartCard>
</template>

<script setup lang="ts">
/**
 * A multi-series line chart (R5 "Insight", Task 5) — e.g. consumption or spending over time,
 * bucketed by day or week. Wraps `ChartCard` for its header, loading/empty states and accessible
 * fallback table; this component only turns `points` into pixel geometry and axis labels.
 *
 * Filed as `ChartLine.vue`, so it auto-imports cleanly as `<ChartLine>` — see `ChartDonut.vue`'s own
 * note on why (this filename starts with `Chart`, so Nuxt's directory-prefix collapsing applies; it
 * was originally filed as `LineChart.vue`, which auto-imported as the stuttering `<ChartLineChart>`
 * until this rename).
 *
 * All geometry is a `computed` over props and the margin/viewBox constants below — no lifecycle
 * hook, no measurement. The one margin that is not fixed, the left gutter, is still derived purely
 * from the label strings rather than from the DOM; see `plotLeft`. `points[].t` are epoch
 * milliseconds and are handed straight to
 * `linearScale`/`timeTicks`/`formatAxisDate` unconverted: the server already did the one and only
 * timezone conversion, and `formatAxisDate` pins UTC on purpose so the label names the same
 * calendar boundary `timeTicks` computed it to be (see that function's own comment in
 * `~/utils/chart/format` and `~/utils/chart/scale`).
 *
 * The y domain does not use the series' raw min/max directly — it is widened to `niceTicks`' own
 * rounded bounds first, so the gridlines/labels land exactly on the domain's top and bottom edge
 * instead of the axis extremes falling at an arbitrary in-between value.
 *
 * Each series strokes in with the `pathLength="1"` trick (`.chart-draw`, from `main.css` — see
 * that class's own comment), the same one `SplashScreen.vue`'s logo uses, and paints its
 * theme-correct colour the way `ChartCard`'s swatch and `ProductImage.vue` do: a static `dark:`
 * utility class selecting between two CSS custom properties supplied via inline `:style`, never a
 * script-resolved colour (the theme is unknown during SSR). Colours come from `seriesColors()`
 * (`~/utils/chart/series`), one per series by position — with two or more lines on the same axes,
 * colour is the only way to tell them apart, which is exactly what the legend this component
 * builds is for.
 */
import { linearScale, niceTicks, timeTicks } from '~/utils/chart/scale'
import { linePath } from '~/utils/chart/path'
import { seriesColors } from '~/utils/chart/series'
import { formatCompact, formatAxisDate } from '~/utils/chart/format'

interface ChartLineProps {
  series: { key: string; label: string; points: { t: number; v: number }[] }[]
  bucket: 'day' | 'week'
  title: string
  loading?: boolean
  valueFormatter?: (v: number) => string
}

const props = defineProps<ChartLineProps>()

const { t, locale } = useI18n()

const PLOT = { left: 34, right: 10, top: 10, bottom: 22, width: 320, height: 200 }

/**
 * Roughly how wide one character of a y-axis label is at `font-size="8"`, in viewBox units. An
 * estimate on purpose: measuring the rendered text would mean `getBBox`, which needs a mounted SVG
 * and would make this component's geometry depend on a lifecycle hook — the one thing the chart
 * layer is built to avoid (fixed viewBox, no measurement, SSR-safe, no hydration mismatch). Digits
 * and separators at this size sit a little under 5 units, so this over-estimates slightly, which is
 * the safe direction: a hair too much gutter costs nothing, a hair too little clips a label.
 */
const AXIS_CHAR_WIDTH = 4.8

/** Breathing room between the viewBox's left edge and the start of the widest label. */
const PLOT_LEFT_PADDING = 10

/** Never narrower than the original fixed gutter, and never so wide the plot itself disappears. */
const MIN_PLOT_LEFT = PLOT.left
const MAX_PLOT_LEFT = 120

const formatValue = computed(() => props.valueFormatter ?? ((v: number) => formatCompact(v, locale.value)))

const allPoints = computed(() => props.series.flatMap(series => series.points))

const yTicks = computed<number[]>(() => {
  if (allPoints.value.length === 0) return [0, 1]
  const values = allPoints.value.map(point => point.v)
  return niceTicks(Math.min(...values), Math.max(...values))
})

const yDomain = computed<[number, number]>(() => {
  const ticks = yTicks.value
  return [ticks[0]!, ticks[ticks.length - 1]!]
})

const xDomain = computed<[number, number]>(() => {
  if (allPoints.value.length === 0) return [0, 1]
  const timestamps = allPoints.value.map(point => point.t)
  return [Math.min(...timestamps), Math.max(...timestamps)]
})

const xTicks = computed<number[]>(() => timeTicks(xDomain.value[0], xDomain.value[1], props.bucket))

const yScale = computed(() => linearScale(yDomain.value, [PLOT.height - PLOT.bottom, PLOT.top]))

const yTickMarks = computed(() => yTicks.value.map(value => ({
  value,
  y: yScale.value(value),
  label: formatValue.value(value)
})))

/**
 * The left gutter, sized to the widest y-axis label rather than fixed at `PLOT.left`.
 *
 * The labels are right-anchored at `plotLeft - 6`, so a label wider than the gutter is clipped by
 * the viewBox's own left edge — which is exactly what happened the first time a caller passed a
 * currency `valueFormatter` (#128's price chart): "0,80 EUR" needs about 38 units, the fixed
 * 34-unit gutter cut it down to "80 EUR", and every label on the axis became a different, wrong
 * number. `formatCompact`'s short labels ("1,2k") still land on the old 34 via `MIN_PLOT_LEFT`, so
 * no chart that existed before this moves.
 *
 * Declared here rather than next to `PLOT` because it depends on `yTickMarks` — the labels have to
 * exist before they can be measured — and `xScale` below depends on it in turn.
 */
const plotLeft = computed(() => {
  const widestLabel = yTickMarks.value.reduce((widest, tick) => Math.max(widest, tick.label.length), 0)
  return Math.min(MAX_PLOT_LEFT, Math.max(MIN_PLOT_LEFT, PLOT_LEFT_PADDING + widestLabel * AXIS_CHAR_WIDTH))
})

const xScale = computed(() => linearScale(xDomain.value, [plotLeft.value, PLOT.width - PLOT.right]))

const xTickMarks = computed(() => xTicks.value.map(value => ({
  value,
  x: xScale.value(value),
  label: formatAxisDate(value, locale.value, props.bucket)
})))

const colorByKey = computed(() => seriesColors(props.series.map(series => series.key)))

const seriesPaths = computed(() => props.series.map((series) => {
  const color = colorByKey.value.get(series.key)!
  const scaledPoints = series.points.map(point => ({ x: xScale.value(point.t), y: yScale.value(point.v) }))
  return {
    key: series.key,
    label: series.label,
    d: linePath(scaledPoints),
    colorLight: color.light,
    colorDark: color.dark
  }
}))

const legend = computed(() => seriesPaths.value.map(line => ({
  key: line.key,
  label: line.label,
  colorLight: line.colorLight,
  colorDark: line.colorDark
})))

// The union of every timestamp actually plotted by any series, sorted — not just the first
// series' own points. Series are not guaranteed to share identical buckets (a member added
// partway through the window has a shorter history), so a row's value at a given column can be
// legitimately missing rather than misaligned.
const tableTimestamps = computed<number[]>(() => {
  const timestamps = new Set<number>()
  for (const series of props.series) {
    for (const point of series.points) timestamps.add(point.t)
  }
  return Array.from(timestamps).sort((a, b) => a - b)
})

const tableHeaders = computed(() => [
  t('chart.line.seriesHeader'),
  ...tableTimestamps.value.map(timestamp => formatAxisDate(timestamp, locale.value, props.bucket))
])

const tableRows = computed(() => {
  if (allPoints.value.length === 0) return []
  return props.series.map((series) => {
    const valueByTime = new Map(series.points.map(point => [point.t, point.v]))
    return {
      label: series.label,
      values: tableTimestamps.value.map((timestamp) => {
        const value = valueByTime.get(timestamp)
        return value === undefined ? '—' : formatValue.value(value)
      })
    }
  })
})
</script>
