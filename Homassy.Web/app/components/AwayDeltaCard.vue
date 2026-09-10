<template>
  <button
    v-if="delta"
    type="button"
    class="away-delta-card flex w-full items-center gap-2 rounded-xl border border-default bg-elevated px-3 py-2 text-left"
    @click="openTimeline"
  >
    <UIcon name="i-lucide-history" class="h-4 w-4 shrink-0 text-primary" />
    <span class="min-w-0 flex-1 truncate text-sm text-highlighted">
      <span class="font-semibold">{{ $t('activity.away.changes', { count: delta.total }) }}</span>
      <span v-if="actorLabel" class="text-muted"> · {{ actorLabel }}</span>
    </span>
    <UIcon name="i-lucide-chevron-right" class="h-4 w-4 shrink-0 text-dimmed" />
  </button>
</template>

<script setup lang="ts">
/**
 * One compact line summarising what changed while the user was away (#127) - "3 changes since your
 * last visit · Anna and Bence" - which taps through to the activity timeline over that exact
 * window.
 *
 * It is a `<button>`, not a card with a link inside: the whole line is the target, and a button is
 * what a screen reader should announce for something that navigates on tap without being a
 * document link.
 *
 * The actor list comes from the server already capped at three (see `AwayDeltaResponse.TopActors`),
 * so the join here is only about language: "Anna", "Anna and Bence", "Anna, Bence and Csilla".
 * Built with `Intl.ListFormat`, which gets the conjunction right in all three locales instead of
 * hard-coding "and" / "és" / "und".
 *
 * <b>Acknowledging is what moves the stored last-seen.</b> This component emits it on mount -
 * being mounted is precisely "the summary has been shown" - and never at fetch time, so a reload
 * between the response and the render cannot swallow a delta the user never saw. Once the card
 * emits, the parent clears the delta and the card unmounts, which is why it does not need to
 * remember having done so.
 */
import { computed, onMounted } from 'vue'
import type { AwayDeltaResponse } from '~/types/insights'

const props = defineProps<{
  delta: AwayDeltaResponse | null
}>()

const emit = defineEmits<{
  acknowledge: []
}>()

const { locale } = useI18n()
const router = useRouter()

const actorLabel = computed(() => {
  const names = (props.delta?.topActors ?? []).map(actor => actor.displayName).filter(Boolean)
  if (names.length === 0) return ''

  try {
    return new Intl.ListFormat(locale.value, { style: 'long', type: 'conjunction' }).format(names)
  } catch {
    // A locale Intl.ListFormat does not know is not worth losing the names over.
    return names.join(', ')
  }
})

/**
 * Opens the timeline over the delta's own window - the same `since`/`until` the count was computed
 * from, so the list the user lands on cannot disagree with the number they tapped.
 */
const openTimeline = (): void => {
  const delta = props.delta
  if (!delta) return

  void router.push({ path: '/activity', query: { since: delta.since, until: delta.until } })
  emit('acknowledge')
}

// Mounted means shown, which is when the last-seen may safely move forward.
onMounted(() => {
  if (props.delta) emit('acknowledge')
})
</script>

<style scoped>
/* Arrives with the app's own pop-in, and simply is there under reduced motion - the card is
   information, and the animation is only how it appears. */
.away-delta-card {
  animation: away-delta-in var(--bubble-in) var(--bubble-ease-pop) forwards;
}

@keyframes away-delta-in {
  from { opacity: 0; transform: translateY(-4px); }
  to { opacity: 1; transform: none; }
}

@media (prefers-reduced-motion: reduce) {
  .away-delta-card {
    animation: none;
  }
}
</style>
