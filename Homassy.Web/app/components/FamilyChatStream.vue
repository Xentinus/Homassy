<template>
  <div class="relative min-h-0 flex-1">
    <div
      ref="scrollEl"
      class="h-full overflow-y-auto px-3 py-3"
      role="log"
      aria-live="polite"
      :aria-label="t('familyChat.title')"
      @scroll="onScroll"
    >
      <div v-if="loadingOlder" class="flex justify-center py-2">
        <UIcon name="i-lucide-loader-circle" class="h-5 w-5 animate-spin text-gray-400" />
      </div>

      <p v-else-if="!hasOlder && messages.length > 0" class="py-2 text-center text-xs text-gray-400">
        {{ t('familyChat.stream.beginning') }}
      </p>

      <div v-if="loading && messages.length === 0" class="space-y-3">
        <USkeleton v-for="i in 4" :key="i" class="h-12 w-2/3 rounded-2xl" />
      </div>

      <!-- Deliberately not `EmptyState`: that component is a full illustration sized for a page,
           and this is a panel the height of a phone screen with a composer under it. -->
      <div v-else-if="messages.length === 0" class="flex h-full flex-col items-center justify-center gap-2 px-6 text-center">
        <UIcon name="i-lucide-message-circle" class="h-10 w-10 text-gray-300 dark:text-gray-600" />
        <p class="text-sm font-medium">{{ t('familyChat.stream.emptyTitle') }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('familyChat.stream.emptyDescription') }}</p>
      </div>

      <div v-else class="space-y-4">
        <section v-for="section in sections" :key="section.key" class="space-y-3">
          <div class="sticky top-0 z-10 flex justify-center">
            <span class="rounded-full bg-elevated/90 px-3 py-1 text-xs text-gray-500 backdrop-blur dark:text-gray-400">
              {{ dayLabel(section.key) }}
            </span>
          </div>

          <FamilyChatMessageGroup
            v-for="run in section.runs"
            :key="run.key"
            :run="run"
            :own="run.senderPublicId === currentUserPublicId"
            @retry="(m) => emit('retry', m)"
            @discard="(m) => emit('discard', m)"
            @delete="(m) => emit('delete', m)"
          />
        </section>
      </div>
    </div>

    <!-- Jump to latest. Only while the reader is actually away from the bottom: a pill that is
         always there is a pill nobody reads. -->
    <Transition name="chat-pill">
      <UButton
        v-if="!atBottom && messages.length > 0"
        icon="i-lucide-arrow-down"
        color="neutral"
        variant="solid"
        size="sm"
        class="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full shadow-lg"
        @click="scrollToBottom(true)"
      >
        {{ t('familyChat.stream.jumpToLatest') }}
      </UButton>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { formatDayBucketDate } from '~/utils/activityTimeline'
import { buildChatSections } from '~/utils/familyChat'
import type { FamilyChatStreamMessage } from '~/types/familyChat'

/**
 * The message stream (#146): day separators, sender runs, and the scroll behaviour that makes a
 * conversation readable in both directions.
 *
 * Newest at the bottom, which is what makes every rule here the way round it is: "load older"
 * fires at the *top*, a send scrolls to the *bottom*, and the jump-to-latest pill only exists
 * while the reader has scrolled away from it.
 *
 * **Windowing is a cap on rendered rows, not a virtual scroller.** A family conversation is long
 * in months, not in rows on screen; what actually hurts is a year of history all mounted at once
 * after a few "load older" pages. So the stream renders the newest `renderLimit` messages and
 * raises that limit as the reader walks backwards. Rows are plain flow content, so scroll
 * anchoring and text selection keep working - both of which a virtualiser would have to
 * reimplement for a list whose rows are not a fixed height.
 */

const props = defineProps<{
  messages: FamilyChatStreamMessage[]
  loading: boolean
  loadingOlder: boolean
  hasOlder: boolean
  currentUserPublicId: string | null
}>()

const emit = defineEmits<{
  loadOlder: []
  /**
   * The newest message is actually on screen (#149).
   *
   * What the panel marks as read off - never "the panel mounted". A panel opened in a background
   * tab, or one scrolled far back through history, has shown the reader nothing new, and clearing
   * the badge there would throw away the only signal that something arrived.
   */
  seenLatest: []
  retry: [message: FamilyChatStreamMessage]
  discard: [message: FamilyChatStreamMessage]
  delete: [message: FamilyChatStreamMessage]
}>()

/** Distance from the bottom that still counts as "at the bottom". */
const BOTTOM_SLACK = 80
/** Distance from the top at which the next older page is fetched. */
const TOP_TRIGGER = 120
/** How many messages are rendered at once, and how much each walk backwards adds. */
const INITIAL_RENDER_LIMIT = 120
const RENDER_LIMIT_STEP = 60

const { t, locale } = useI18n()

const scrollEl = ref<HTMLElement | null>(null)
const atBottom = ref(true)
const renderLimit = ref(INITIAL_RENDER_LIMIT)

/** Height of the scroll content before a prepend, so the reader's position can be restored after it. */
let heightBeforePrepend: number | null = null

const rendered = computed(() => props.messages.slice(-renderLimit.value))

const sections = computed(() => buildChatSections(rendered.value, new Date()))

/**
 * `key` -> a heading. 'today'/'yesterday' are translations; anything older is a plain
 * `YYYY-MM-DD` formatted in the viewer's locale order - the same pair of rules the activity
 * timeline and the notification centre use, through the same helper.
 */
const dayLabel = (key: string): string => {
  if (key === 'today') return t('activity.today')
  if (key === 'yesterday') return t('activity.yesterday')
  return formatDayBucketDate(key, locale.value)
}

const scrollToBottom = (smooth = false): void => {
  const el = scrollEl.value
  if (!el) return

  el.scrollTo({
    top: el.scrollHeight,
    behavior: smooth && !prefersReducedMotion() ? 'smooth' : 'auto'
  })
  atBottom.value = true
  reportSeenLatest()
}

/**
 * Says the newest message is on screen - but only while the document is visible.
 *
 * A background tab is still "scrolled to the bottom"; nobody is looking at it, and marking the
 * conversation read from there is exactly the silent badge-clearing this is meant to avoid.
 */
const reportSeenLatest = (): void => {
  if (!import.meta.client || document.visibilityState !== 'visible') return
  if (props.messages.length === 0) return

  emit('seenLatest')
}

const prefersReducedMotion = (): boolean =>
  import.meta.client && window.matchMedia('(prefers-reduced-motion: reduce)').matches

const onScroll = (): void => {
  const el = scrollEl.value
  if (!el) return

  const wasAtBottom = atBottom.value
  atBottom.value = el.scrollHeight - el.scrollTop - el.clientHeight <= BOTTOM_SLACK

  // Scrolling back down to the newest message is a read, the same as having been there already.
  if (atBottom.value && !wasAtBottom) reportSeenLatest()

  if (el.scrollTop > TOP_TRIGGER) return

  // At the top there are two ways to go further back: raise the render cap over messages
  // already loaded, or ask the server for the next page. Do the cheap one first.
  if (rendered.value.length < props.messages.length) {
    heightBeforePrepend = el.scrollHeight
    renderLimit.value += RENDER_LIMIT_STEP
    return
  }

  if (props.hasOlder && !props.loadingOlder) {
    heightBeforePrepend = el.scrollHeight
    emit('loadOlder')
  }
}

/**
 * Keeps the reader where they were when content is added above them.
 *
 * Without this a "load older" page shoves the whole conversation down by its own height, which
 * reads as the stream jumping backwards at the exact moment the reader is trying to walk back
 * through it.
 */
const restoreAfterPrepend = async (): Promise<void> => {
  const el = scrollEl.value
  if (!el || heightBeforePrepend === null) return

  await nextTick()
  el.scrollTop += el.scrollHeight - heightBeforePrepend
  heightBeforePrepend = null
}

watch(() => props.messages.length, async (next, previous) => {
  if (heightBeforePrepend !== null) {
    // A page that was just fetched has to fit inside the render window, or it is loaded and then
    // not drawn: once the conversation is longer than the cap, the window is the newest N
    // messages and a prepended page falls outside it entirely. The reader would see the spinner
    // stop, nothing appear, and the scroll restore compute a delta of zero. Growing the cap by
    // exactly what arrived keeps the cap meaningful (it still bounds the *initial* render) while
    // guaranteeing that what the reader asked for is what they get.
    if (next > previous) renderLimit.value += next - previous

    await restoreAfterPrepend()
    return
  }

  if (next <= previous) return

  // A new message at the bottom follows the reader's own position: if they were reading the
  // latest, keep them there; if they had scrolled up, leave them alone and let the pill say
  // something arrived. Their own send is the exception - you always follow your own message.
  const last = props.messages.at(-1)
  const isOwnSend = last?.sender.publicId === props.currentUserPublicId

  if (atBottom.value || isOwnSend) {
    await nextTick()
    // `scrollToBottom` reports the read itself: a message that arrives while the reader is at the
    // bottom has been seen the moment it lands.
    scrollToBottom()
  }
})

// Covers the cheap path, where only the cap moved and no message arrived. When the cap is raised
// by the watcher above instead, that one has already restored and cleared the anchor, so this
// fires into `restoreAfterPrepend`'s own null guard and does nothing.
watch(renderLimit, restoreAfterPrepend)

onMounted(async () => {
  await nextTick()
  scrollToBottom()
})

defineExpose({ scrollToBottom })
</script>

<style scoped>
.chat-pill-enter-active,
.chat-pill-leave-active {
  transition: opacity var(--bubble-out) ease, transform var(--bubble-out) var(--bubble-ease-out);
}

/* Only Y here: the horizontal centring is Tailwind's `-translate-x-1/2`, which sets the
   `translate` property rather than `transform`, so the two compose instead of fighting. */
.chat-pill-enter-from,
.chat-pill-leave-to {
  opacity: 0;
  transform: translateY(8px);
}

@media (prefers-reduced-motion: reduce) {
  .chat-pill-enter-active,
  .chat-pill-leave-active {
    transition: opacity var(--bubble-out) ease;
  }

  .chat-pill-enter-from,
  .chat-pill-leave-to {
    transform: none;
  }
}
</style>
