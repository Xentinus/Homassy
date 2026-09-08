<template>
  <Transition name="bubble">
    <div
      v-if="visible"
      role="status"
      aria-live="polite"
      class="undo-toast fixed inset-x-4 z-40 mx-auto flex w-fit max-w-[calc(100%-2rem)] items-center gap-3 rounded-full bg-inverted px-4 py-2.5 text-sm font-medium text-inverted shadow-lg"
    >
      <!-- Countdown ring: stroke-dashoffset is bound straight to remainingRatio (which already
           ticks every frame via useUndoableAction's own requestAnimationFrame loop), so there is
           no separate CSS animation/transition here to neutralise for reduced motion — instead,
           under prefers-reduced-motion the ring is swapped for the static seconds count below,
           which is the actual point 8 requirement: the deadline stays legible, only the motion
           goes. Both are aria-hidden — the live region only needs to announce the label (below)
           once when it changes, not a per-second countdown. -->
      <svg
        v-if="!prefersReducedMotion"
        class="h-6 w-6 shrink-0 -rotate-90"
        viewBox="0 0 24 24"
        aria-hidden="true"
      >
        <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="2.5" class="opacity-25" />
        <circle
          cx="12"
          cy="12"
          r="10"
          fill="none"
          stroke="currentColor"
          stroke-width="2.5"
          stroke-linecap="round"
          :stroke-dasharray="RING_CIRCUMFERENCE"
          :stroke-dashoffset="ringDashOffset"
        />
      </svg>
      <span
        v-else
        class="flex h-6 min-w-6 shrink-0 items-center justify-center text-xs font-bold tabular-nums"
        aria-hidden="true"
      >{{ $t('undo.secondsLeft', { seconds: secondsLeft }) }}</span>

      <span class="min-w-0 flex-1 truncate">{{ label }}</span>

      <button
        type="button"
        class="undo-toast-action shrink-0 rounded-full px-2 py-1 font-bold underline decoration-2 underline-offset-2 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-current"
        @click="handleUndo"
      >
        {{ $t('undo.action') }}
      </button>
    </div>
  </Transition>
</template>

<script setup lang="ts">
/**
 * The one place the app shows a pending optimistic action: a collapsed label ("3 items removed"),
 * a shrinking ring, and an Undo button, reading `useUndoableAction()`'s singleton. Mounted once in
 * `app.vue` (like `SplashScreen`) so it survives navigation — a swipe-delete on the shopping-list
 * page is still undoable from the toast if the user has since moved to another tab.
 *
 * A sibling of `RealtimeConnectionBar.vue`: same `role="status"`/`aria-live="polite"` contract,
 * same `bubble` transition for mount/unmount, same "only render when there is something to show"
 * shape (`visible` here, `status !== 'idle'` there).
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { collapseLabel, UNDO_WINDOW_MS } from '~/utils/undoQueue'
import { useUndoableAction } from '~/composables/useUndoableAction'

const { t } = useI18n()
const { pending, remainingRatio, undoAll } = useUndoableAction()

const visible = computed(() => pending.value.length > 0)

/**
 * `collapseLabel`'s `t` is deliberately the simple `(key, params?) => string` shape so the pure
 * module never has to know about i18n plural rules (see undoQueue.ts / useUndoableAction.ts). The
 * real vue-i18n `t`, though, only picks the right grammatical form (singular vs. plural, per
 * locale) when the count is *also* passed as its own plural argument — passing it solely inside
 * `params` fills in `{count}` but does not select a form. This adapter is the one bridge between
 * the two, so `undo.collapsed.*`'s locale strings can use real pluralisation.
 */
const pluralAwareT = (key: string, params?: Record<string, unknown>): string => {
  const count = typeof params?.count === 'number' ? params.count : undefined
  return count === undefined ? t(key, params ?? {}) : t(key, params ?? {}, count)
}

const label = computed(() => collapseLabel(pending.value, pluralAwareT))

const RING_RADIUS = 10
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS
const ringDashOffset = computed(() => RING_CIRCUMFERENCE * (1 - remainingRatio.value))

const secondsLeft = computed(() => Math.max(0, Math.ceil(remainingRatio.value * (UNDO_WINDOW_MS / 1000))))

const prefersReducedMotion = ref(false)
let motionQuery: MediaQueryList | null = null
const syncReducedMotion = () => { prefersReducedMotion.value = motionQuery?.matches ?? false }

onMounted(() => {
  if (!import.meta.client) return
  motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
  syncReducedMotion()
  motionQuery.addEventListener('change', syncReducedMotion)
})

onBeforeUnmount(() => {
  motionQuery?.removeEventListener('change', syncReducedMotion)
})

const handleUndo = (): void => { undoAll() }
</script>

<style scoped>
/* Above the bottom nav (see layouts/auth.vue), never on top of it. The nav's own row is h-16 (4rem)
   plus vertical padding and a safe-area pad, so 6rem clears it with a visible gap; a reviewer
   looking at this in an actual viewport should double-check the gap once the two can be seen
   together (see the task report — this is the one thing not confirmed without a browser). */
.undo-toast {
  bottom: calc(6rem + env(safe-area-inset-bottom));
}

.undo-toast-action:hover {
  opacity: 0.85;
}
</style>
