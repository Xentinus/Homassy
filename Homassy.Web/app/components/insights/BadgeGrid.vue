<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-award" class="h-5 w-5 shrink-0 text-primary" />
        <div class="min-w-0 flex-1">
          <h2 class="truncate text-base font-semibold text-highlighted leading-tight">
            {{ $t('insights.badges.title') }}
          </h2>
          <p class="truncate text-sm text-muted">
            {{ $t('insights.badges.earnedCount', { earned: earnedCount, total: views.length }) }}
          </p>
        </div>
      </div>
    </template>

    <div v-if="loading" class="grid grid-cols-2 gap-3 sm:grid-cols-3">
      <USkeleton v-for="tile in 6" :key="tile" class="h-24 w-full" />
    </div>

    <p v-else-if="views.length === 0" class="text-sm text-muted">{{ $t('insights.badges.empty') }}</p>

    <div v-else class="grid grid-cols-2 gap-3 sm:grid-cols-3">
      <div
        v-for="view in views"
        :key="view.id"
        class="badge-tile relative flex flex-col items-center gap-1.5 overflow-hidden rounded-xl border border-default p-3 text-center"
        :class="view.earned ? 'bg-elevated' : 'bg-default'"
      >
        <!-- The unlock celebration: a dozen spans thrown outward from the tile's centre. Rendered
             only for a badge that arrived with justUnlocked, so it cannot replay on a reload - the
             server reports that flag on exactly one response (see the API's own test). -->
        <span v-if="view.justUnlocked" class="badge-burst" aria-hidden="true">
          <span v-for="particle in BURST_PARTICLES" :key="particle" :style="{ '--burst-index': particle }" />
        </span>

        <span
          class="relative flex h-12 w-12 shrink-0 items-center justify-center rounded-full"
          :class="view.earned ? 'bg-primary/15 text-primary' : 'bg-elevated text-dimmed'"
        >
          <!-- The ring is the progress; the numbers below repeat it as text. A locked badge shows
               how far along it is rather than nothing at all. -->
          <svg class="absolute inset-0 -rotate-90" width="48" height="48" viewBox="0 0 100 100" aria-hidden="true">
            <circle
              cx="50" cy="50" :r="RADIUS" fill="none" stroke="currentColor" :stroke-width="STROKE"
              stroke-linecap="round"
              :stroke-dasharray="CIRCUMFERENCE"
              :stroke-dashoffset="dashOffsetFor(view)"
              :class="view.earned ? 'opacity-90' : 'opacity-60'"
            />
          </svg>
          <UIcon :name="view.icon" class="relative h-6 w-6" :class="view.earned ? '' : 'grayscale'" />
        </span>

        <p class="text-sm font-semibold text-highlighted" :class="view.earned ? '' : 'text-muted'">{{ view.title }}</p>
        <p class="text-xs text-muted line-clamp-2">{{ view.description }}</p>

        <!-- Under reduced motion the burst is neutralised in CSS and this stamp is what says the
             badge is new instead - the same information, with no motion. -->
        <span v-if="view.justUnlocked" class="badge-unlocked-stamp text-xs font-semibold text-primary">
          {{ $t('insights.badges.justUnlocked') }}
        </span>
        <p v-else-if="view.earned" class="text-xs text-dimmed">{{ $t('insights.badges.earned') }}</p>
        <p v-else class="text-xs text-dimmed tabular-nums">{{ view.progress }} / {{ view.threshold }}</p>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
/**
 * The badge grid (#109): every badge in the catalog, unlocked ones in colour with their earned
 * state and locked ones desaturated with a progress ring.
 *
 * <b>A locked badge still shows its title, its description and its threshold.</b> A mystery box
 * tells the family nothing about what to aim for, and the whole point of the grid is that the next
 * badge is legible.
 *
 * <b>Text resolution is not this component's job.</b> `toBadgeViews` (`~/utils/badgeState`) resolves
 * each badge's title and description from the locale files, falling back to the English the server
 * shipped with the badge - which is what lets a badge added server-side render on a client that has
 * never heard of it. This component only draws what it is handed.
 *
 * The celebration fires from `justUnlocked`, which the API sets on exactly one response per badge
 * ever, so a reload cannot replay it - the "fire once" guarantee lives in the server's durable
 * badge row, not in a flag this component would have to remember. The haptic goes with it, from R2's
 * fixed vocabulary (`success` - an operation completed) rather than a duration of its own.
 */
import { computed, watch } from 'vue'
import { toBadgeViews } from '~/utils/badgeState'
import type { BadgeStateDto } from '~/types/insights'

const props = withDefaults(defineProps<{
  badges: BadgeStateDto[]
  loading?: boolean
}>(), {
  loading: false
})

const { t, te } = useI18n()
const { success } = useHaptics()

/** How many particles the burst throws. A dozen reads as celebratory without becoming confetti. */
const BURST_PARTICLES = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]

const RADIUS = 44
const STROKE = 7
const CIRCUMFERENCE = 2 * Math.PI * RADIUS

/**
 * `te` is vue-i18n's own "does this key exist" check, which is exactly the `hasTranslation`
 * predicate `toBadgeViews` asks for - so the fallback rule is decided by the locale files rather
 * than by string-matching whatever `t()` returned.
 */
const views = computed(() => toBadgeViews(props.badges, key => t(key), key => te(key)))

const earnedCount = computed(() => views.value.filter(view => view.earned).length)

const dashOffsetFor = (view: { progress: number, threshold: number, earned: boolean }): number => {
  const fraction = view.earned ? 1 : view.threshold > 0 ? Math.min(view.progress / view.threshold, 1) : 0
  return CIRCUMFERENCE * (1 - fraction)
}

// The haptic half of the celebration. Watches the arriving payload rather than firing in
// `onMounted`, because the badges land after the page's own fetch resolves - and only fires when
// something actually just unlocked, never on an ordinary load.
watch(
  () => props.badges.some(badge => badge.justUnlocked),
  (hasUnlock) => {
    if (hasUnlock) success()
  },
  { immediate: true }
)
</script>
