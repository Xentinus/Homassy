<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-flame" class="h-5 w-5 shrink-0 text-primary" />
        <h2 class="truncate text-base font-semibold text-highlighted leading-tight">
          {{ $t('insights.streaks.title') }}
        </h2>
      </div>
    </template>

    <div v-if="loading" class="space-y-2">
      <USkeleton v-for="row in 2" :key="row" class="h-16 w-full" />
    </div>

    <div v-else class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div v-for="streak in streaks" :key="streak.key" class="flex items-center gap-3">
        <span class="relative inline-flex h-14 w-14 shrink-0 items-center justify-center text-primary">
          <svg class="absolute inset-0 -rotate-90" width="56" height="56" viewBox="0 0 100 100" aria-hidden="true">
            <circle cx="50" cy="50" :r="RADIUS" fill="none" stroke="currentColor" :stroke-width="STROKE" class="opacity-15" />
            <circle
              cx="50"
              cy="50"
              :r="RADIUS"
              fill="none"
              stroke="currentColor"
              :stroke-width="STROKE"
              stroke-linecap="round"
              :stroke-dasharray="CIRCUMFERENCE"
              :stroke-dashoffset="dashOffsetFor(streak.fraction)"
              :style="{ transition: animate ? 'stroke-dashoffset 700ms cubic-bezier(0.22, 1, 0.36, 1)' : 'none' }"
            />
          </svg>
          <!-- The number is the information; the ring is decoration on top of it. It is plain text
               at every size and under every motion preference, never rendered only in the arc. -->
          <span class="relative text-sm font-semibold text-highlighted tabular-nums">{{ streak.current }}</span>
        </span>

        <div class="min-w-0">
          <p class="truncate text-sm font-medium text-highlighted">{{ streak.label }}</p>
          <p class="text-sm text-muted">{{ $t('insights.streaks.currentDays', { count: streak.current }) }}</p>
          <p class="text-xs text-dimmed">{{ $t('insights.streaks.longestDays', { count: streak.longest }) }}</p>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
/**
 * The household's two streaks (#109): consecutive days with nothing expiring, and consecutive days
 * with the shopping list cleared. Each gets a flame-headed card row with a progress ring showing
 * the current run against the longest one.
 *
 * The ring is the same pattern `StockRing` uses rather than a new one: an SVG arc whose
 * `stroke-dashoffset` starts empty and transitions to its value on mount, with the transition
 * simply not installed under `prefers-reduced-motion` (so there is nothing for the value change to
 * animate through, rather than an animation that is cancelled). The `setTimeout` beside the
 * `requestAnimationFrame` is load-bearing for the same reason it is there: rAF does not fire in a
 * hidden tab, and without it the ring would sit empty until the tab came back.
 *
 * Under reduced motion - and while the ring is still filling, and at every text size - the number
 * of days is present as text. The arc carries no information the words do not.
 */
import { computed, onMounted, ref } from 'vue'
import type { StreakInfo } from '~/types/insights'

const props = withDefaults(defineProps<{
  noExpiry: StreakInfo
  listCleared: StreakInfo
  loading?: boolean
}>(), {
  loading: false
})

const { t } = useI18n()

const RADIUS = 44
const STROKE = 8
const CIRCUMFERENCE = 2 * Math.PI * RADIUS

const streaks = computed(() => [
  {
    key: 'no-expiry',
    label: t('insights.streaks.noExpiry'),
    current: props.noExpiry.current,
    longest: props.noExpiry.longest,
    fraction: fractionOf(props.noExpiry)
  },
  {
    key: 'list-cleared',
    label: t('insights.streaks.listCleared'),
    current: props.listCleared.current,
    longest: props.listCleared.longest,
    fraction: fractionOf(props.listCleared)
  }
])

/**
 * The current run against the best one, which is what makes the ring mean something: full means
 * "this is your best ever". A household with no streak at all has no denominator, so the ring is
 * empty rather than claiming a proportion of nothing.
 */
function fractionOf(streak: StreakInfo): number {
  if (streak.longest <= 0) return 0
  return Math.min(Math.max(streak.current / streak.longest, 0), 1)
}

const animate = ref(false)
const settled = ref(false)

const dashOffsetFor = (fraction: number): number =>
  CIRCUMFERENCE * (1 - (settled.value ? fraction : 0))

onMounted(() => {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    settled.value = true
    return
  }

  animate.value = true
  requestAnimationFrame(() => { settled.value = true })
  setTimeout(() => { settled.value = true }, 120)
})
</script>
