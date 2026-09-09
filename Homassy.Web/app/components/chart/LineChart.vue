<template>
  <ChartCard :title="title" :loading="loading" :legend="legend" :table-headers="tableHeaders" :table-rows="tableRows" :aspect-ratio="1.6">
    <svg viewBox="0 0 320 200" class="w-full h-auto" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
      <g class="text-muted">
        <line
          v-for="tick in yTickMarks"
          :key="`grid-${tick.value}`"
          :x1="PLOT.left" :x2="PLOT.width - PLOT.right"
          :y1="tick.y" :y2="tick.y"
          stroke="currentColor" stroke-width="0.5" class="opacity-40"
        />
        <text
          v-for="tick in yTickMarks"
          :key="`y-${tick.value}`"
          :x="PLOT.left - 6" :y="tick.y"
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
 * Auto-imported as `<ChartLineChart>`, not `<LineChart>` — see `DonutChart.vue`'s own note on why
 * (this filename does not start with `Chart`, so Nuxt's directory-prefix collapsing does not
 * apply).
 *
 * All geometry is a `computed` over props and the two fixed margin/viewBox constants below — no
 * lifecycle hook, no measurement. `points[].t` are epoch milliseconds and are handed straight to
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

interface LineChartProps {
  series: { key: string; label: string; points: { t: number; v: number }[] }[]
  bucket: 'day' | 'week'
  title: string
  loading?: boolean
  valueFormatter?: (v: number) => string
}

const props = defineProps<LineChartProps>()

const { t, locale } = useI18n()

const PLOT = { left: 34, right: 10, top: 10, bottom: 22, width: 320, height: 200 }

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

const xScale = computed(() => linearScale(xDomain.value, [PLOT.left, PLOT.width - PLOT.right]))
const yScale = computed(() => linearScale(yDomain.value, [PLOT.height - PLOT.bottom, PLOT.top]))

const yTickMarks = computed(() => yTicks.value.map(value => ({
  value,
  y: yScale.value(value),
  label: formatValue.value(value)
})))

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
