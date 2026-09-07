<template>
  <div class="min-h-screen flex flex-col items-center justify-center gap-6 bg-default px-6 py-16 text-center">
    <EmptyStateIllustration :name="kind" class="w-28 h-28 sm:w-32 sm:h-32 text-muted" />

    <div class="space-y-2">
      <p v-if="statusCode" class="text-xs font-semibold uppercase tracking-widest text-muted tabular-nums">
        {{ statusCode }}
      </p>
      <h1 class="text-2xl font-bold text-highlighted">{{ copy.title }}</h1>
      <p class="mx-auto max-w-md text-sm text-muted">{{ copy.description }}</p>
    </div>

    <div class="flex w-full max-w-xs flex-col items-stretch gap-2 pt-2">
      <UButton
        v-for="(action, index) in actions"
        :key="action.key"
        block
        :size="index === 0 ? 'lg' : 'md'"
        :color="index === 0 ? 'primary' : 'neutral'"
        :variant="index === 0 ? 'solid' : 'ghost'"
        :icon="action.icon"
        :label="action.label"
        @click="action.run"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The branded error surface, shared by app/error.vue and app/pages/offline.vue.
 *
 * It renders on its own — no layout, no header, no bottom nav — because it has
 * to work when the rest of the app does not: it reads nothing from the auth
 * store and makes no API call. The background is `bg-default` (`--ui-bg`), the
 * same token the boot splash paints, so an error during startup does not flash
 * a different colour.
 *
 * The primary action differs per case, which is the whole point of splitting the
 * three: retrying is right when the network or the server hiccuped, and useless
 * when the URL is simply wrong.
 */
import type { EmptyStateIllustrationName } from '~/types/emptyState'

const props = defineProps<{
  /** Doubles as the illustration name — the three overlap deliberately. */
  kind: Extract<EmptyStateIllustrationName, 'notFound' | 'serverError' | 'offline'>
  /** Shown above the title when the failure carried an HTTP status. */
  statusCode?: number
}>()

const { t } = useI18n()
const router = useRouter()

const CALENDAR_PATH = '/calendar'

const copy = computed(() => ({
  title: t(`errorPage.${props.kind}.title`),
  description: t(`errorPage.${props.kind}.description`)
}))

/**
 * vue-router keeps the previous entry on the history state, so we can offer
 * "Back" as a redirect target instead of calling `router.back()` after
 * `clearError()` — clearing without a target re-renders the route that just
 * failed, which would flash the error again on the way out.
 */
const backPath = computed(() => {
  const back = router.options.history.state?.back
  return typeof back === 'string' && back !== CALENDAR_PATH ? back : null
})

/** Re-runs the navigation that failed: this screen is served at its URL. */
const tryAgain = () => {
  if (import.meta.client) window.location.reload()
}

const actions = computed(() => {
  const tryAgainAction = { key: 'tryAgain', label: t('errorPage.tryAgain'), icon: 'i-lucide-rotate-cw', run: tryAgain }
  const backAction = backPath.value
    ? { key: 'back', label: t('common.back'), icon: 'i-lucide-arrow-left', run: () => clearError({ redirect: backPath.value! }) }
    : null
  const calendarAction = {
    key: 'calendar',
    label: t('errorPage.goToCalendar'),
    icon: 'i-lucide-calendar-days',
    run: () => clearError({ redirect: CALENDAR_PATH })
  }

  // A wrong address is not worth retrying, so 404 leads with somewhere to go.
  const ordered = props.kind === 'notFound'
    ? [calendarAction, backAction, tryAgainAction]
    : [tryAgainAction, backAction, calendarAction]

  return ordered.filter(action => action !== null)
})
</script>
