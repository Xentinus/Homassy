<template>
  <ChartCard :title="title" :loading="loading" :legend="legend" :table-headers="tableHeaders" :table-rows="tableRows" :aspect-ratio="1">
    <svg viewBox="0 0 200 200" class="w-full h-auto" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
      <path
        v-for="arc in arcs"
        :key="arc.key"
        :d="arc.d"
        stroke-width="2"
        class="chart-grow fill-[var(--slice-light)] dark:fill-[var(--slice-dark)] stroke-[var(--ui-bg)]"
        :style="{ '--slice-light': arc.colorLight, '--slice-dark': arc.colorDark }"
      />
    </svg>
  </ChartCard>
</template>

<script setup lang="ts">
/**
 * A donut chart for a single category breakdown (R5 "Insight", Task 5) — e.g. spending or stock
 * split by product category. Wraps `ChartCard`, which owns the header, the loading/empty states
 * and the accessible fallback table; this component only computes the ring's geometry and colours.
 *
 * Filed as `ChartDonut.vue`, so it auto-imports cleanly as `<ChartDonut>` — this filename starts
 * with `Chart`, so Nuxt's directory-prefix collapsing (the same rule that makes `chart/ChartCard.vue`
 * itself just `<ChartCard>`) applies here too. It was originally filed as `DonutChart.vue`, which
 * that collapsing does not fire for (the filename has to *start* with the directory name, not just
 * contain it), so it auto-imported as the stuttering `<ChartDonutChart>` until this rename.
 *
 * Geometry is a `computed` over props alone — no lifecycle hook, no measurement of any kind — so
 * this renders identically on the server and after hydration. The fixed `viewBox` plus `w-full
 * h-auto` is the entire responsive story: the ring simply scales with its container.
 *
 * Slice merging (categories under 2% collapse into a trailing "other") and the angle maths live in
 * `~/utils/chart/donutGeometry.ts`, split out purely so that logic can be unit tested without a Vue
 * runtime — see that file's own comment. Colours come from `seriesColors()` (`~/utils/chart/series`), one
 * per rendered slice (including "other"), assigned by position exactly like `ChartLine` and
 * `ChartBar` do for their own marks.
 *
 * Every slice paints its theme-correct colour the way `ChartCard`'s own swatch and
 * `ProductImage.vue`'s category placeholder do: a static `dark:` utility class picks between two
 * CSS custom properties supplied via inline `:style`. Never a computed "resolved" colour — the
 * theme is unknown during SSR, and Tailwind's JIT cannot generate a class for a value it never saw
 * at build time anyway. The thin `stroke="var(--ui-bg)"` between slices is not a new colour: it is
 * the same card-background token `ChartCard`/`ProductImage` already use, reused here purely as a
 * visual gap between adjacent slices, and it flips with the theme on its own for the same reason.
 */
import { arcPath } from '~/utils/chart/path'
import { seriesColors } from '~/utils/chart/series'
import { formatCompact } from '~/utils/chart/format'
import { buildDonutSlices } from '~/utils/chart/donutGeometry'

interface ChartDonutProps {
  slices: { key: string; label: string; value: number }[]
  title: string
  loading?: boolean
}

const props = defineProps<ChartDonutProps>()

const { t, locale } = useI18n()

const RING = { cx: 100, cy: 100, rOuter: 80, rInner: 50 }

const donutSlices = computed(() => buildDonutSlices(props.slices, t('chart.donut.otherLabel')))

const colorByKey = computed(() => seriesColors(donutSlices.value.map(slice => slice.key)))

const arcs = computed(() => donutSlices.value.map((slice) => {
  const color = colorByKey.value.get(slice.key)!
  return {
    key: slice.key,
    label: slice.label,
    d: arcPath(RING.cx, RING.cy, RING.rOuter, RING.rInner, slice.startRad, slice.endRad),
    colorLight: color.light,
    colorDark: color.dark
  }
}))

const legend = computed(() => arcs.value.map(arc => ({
  key: arc.key,
  label: arc.label,
  colorLight: arc.colorLight,
  colorDark: arc.colorDark
})))

// A plain Intl formatter for the fallback table's share column. Not routed through
// `~/utils/chart/format` (which is closed for this task) — that module's memoisation exists for a
// hot axis-tick loop; a handful of donut slices rendered occasionally does not need it, and a
// locale-keyed `computed` already avoids rebuilding the formatter on every render.
const shareFormatter = computed(() => new Intl.NumberFormat(locale.value, { style: 'percent', maximumFractionDigits: 1 }))

const tableHeaders = computed(() => [t('chart.categoryHeader'), t('chart.valueHeader'), t('chart.donut.shareHeader')])

const tableRows = computed(() => donutSlices.value.map(slice => ({
  label: slice.label,
  values: [formatCompact(slice.value, locale.value), shareFormatter.value.format(slice.fraction)]
})))
</script>
