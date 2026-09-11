<template>
  <Teleport to="body">
    <!-- Tap-outside closes on touch, where there is no Esc key and no window chrome to click.
         On a pointer device the panel stays non-modal: the page behind it is still usable, which
         is the whole point of a chat head rather than a screen. -->
    <Transition name="chat-scrim">
      <div
        v-if="panelOpen && isMobile"
        class="fixed inset-0 z-[58] bg-black/20"
        aria-hidden="true"
        @click="close"
      />
    </Transition>

    <Transition name="chat-panel">
      <div
        v-if="panelOpen"
        ref="cardEl"
        role="dialog"
        :aria-label="t('familyChat.title')"
        class="fixed z-[60] flex flex-col overflow-hidden rounded-2xl border border-default bg-default shadow-2xl"
        :style="cardStyle"
        @keydown="onCardKeyDown"
      >
        <header class="flex shrink-0 items-center gap-2 border-b border-default px-4 py-3">
          <UIcon name="i-lucide-message-circle" class="h-5 w-5 text-primary-500" />
          <h2 class="flex-1 truncate text-sm font-semibold">{{ t('familyChat.title') }}</h2>

          <!-- Who else is in the conversation right now. The bubble carries the same count as a
               badge; here there is room to say what the number means. -->
          <span
            v-if="activeMembers.length > 0"
            class="flex items-center gap-1 text-xs text-gray-500 dark:text-gray-400"
          >
            <span class="h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden="true" />
            {{ t('familyChat.activeCount', { count: activeMembers.length }) }}
          </span>

          <UButton
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            size="xs"
            :aria-label="t('common.close')"
            @click="close"
          />
        </header>

        <FamilyChatStream
          ref="streamRef"
          :messages="messages"
          :loading="loading"
          :loading-older="loadingOlder"
          :has-older="hasOlder"
          :current-user-public-id="currentUserPublicId"
          class="min-h-0 flex-1"
          @load-older="loadOlder"
          @seen-latest="markRead"
          @retry="retry"
          @discard="(m) => discard(m.publicId)"
          @delete="onDelete"
        />
        <FamilyChatTypingIndicator :members="typingMembers" />
        <FamilyChatComposer
          ref="composerRef"
          @send="onSend"
          @image="onSendImage"
          @typing="notifyTyping"
          @idle="stopTyping"
        />
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { FamilyChatReferenceDraft, FamilyChatStreamMessage } from '~/types/familyChat'

/**
 * The family chat panel (#146) — the conversation the bubble opens.
 *
 * **It comes out of the bubble, on every screen size.** The bubble snaps to its nearest corner as
 * the panel opens (see `FamilyChatBubble.snapToCorner`), and the panel is anchored to that corner:
 * below the bubble when it is in a top corner, above it when it is in a bottom one, with its
 * `transform-origin` set to the bubble's own centre so it scales out of the circle that was tapped
 * and collapses back into it on close.
 *
 * This replaced a bottom sheet on mobile. A sheet was the conventional choice and brought
 * `AppDrawer`'s drag-to-close, backdrop and focus trap for free, but it always arrived from the
 * bottom edge regardless of where the chat head was — so the one thing the chat head is for, being
 * the place the conversation lives, was exactly the thing the animation denied. The trade is that
 * Esc handling, the focus trap and tap-outside-to-close are implemented here instead of inherited.
 *
 * Geometry is recomputed against a viewport tick rather than read once: the on-screen keyboard
 * resizes the visual viewport without a `resize` event on some browsers, and a panel measured
 * before the keyboard opened is a panel behind it.
 */

const { t } = useI18n()
const { isMobile } = useDeviceDetection()
const { panelOpen, anchorRect, closePanel } = useFamilyChatBubble()
const {
  messages,
  loading,
  loadingOlder,
  hasOlder,
  currentUserPublicId,
  typingMembers,
  activeMembers,
  notifyTyping,
  stopTyping,
  markRead,
  armInactivityTimeout,
  open,
  close: leaveChat,
  loadOlder,
  send,
  sendImage,
  retry,
  remove,
  discard
} = useFamilyChat()

const cardEl = ref<HTMLElement | null>(null)
const streamRef = ref<{ scrollToBottom: (smooth?: boolean) => void } | null>(null)
const composerRef = ref<{ focus: () => Promise<void> } | null>(null)

/** Gap between the bubble and the panel hanging off it. */
const ANCHOR_GAP = 10
/** Smallest gap between the panel and any viewport edge. */
const VIEWPORT_MARGIN = 12
/** Widest the panel ever gets; below this it takes the viewport minus the margins. */
const MAX_WIDTH = 384
/** Tallest the panel ever gets, whatever room the corner leaves. */
const MAX_HEIGHT = 560
/** Shortest it may be squeezed to before it stops being a conversation. */
const MIN_HEIGHT = 280

/** Bumped on anything that can move or resize the viewport, so the geometry recomputes. */
const viewportTick = ref(0)

const onViewportChange = (): void => { viewportTick.value++ }

/**
 * Where the panel sits: hanging off the bubble's corner, clamped into the viewport.
 *
 * The bubble has already moved to a corner by the time this runs, so "below or above" is decided
 * by which half of the screen it is in, and the panel's own edge lines up with the bubble's side.
 */
const cardStyle = computed(() => {
  // Read so the computed re-runs on resize / keyboard / orientation.
  void viewportTick.value

  const rect = anchorRect.value
  if (!rect || !import.meta.client) {
    return { right: '1rem', bottom: '1rem', width: `${MAX_WIDTH}px`, transformOrigin: 'bottom right' }
  }

  const viewportWidth = window.innerWidth
  const viewportHeight = window.visualViewport?.height ?? window.innerHeight

  const width = Math.min(MAX_WIDTH, viewportWidth - VIEWPORT_MARGIN * 2)
  const openDownwards = rect.top + rect.height / 2 < viewportHeight / 2

  // The room between the bubble and the far edge is all the panel can have.
  const room = openDownwards
    ? viewportHeight - rect.bottom - ANCHOR_GAP - VIEWPORT_MARGIN
    : rect.top - ANCHOR_GAP - VIEWPORT_MARGIN

  const height = Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, room))
  const top = openDownwards
    ? rect.bottom + ANCHOR_GAP
    : Math.max(VIEWPORT_MARGIN, rect.top - ANCHOR_GAP - height)

  // Aligned to the bubble's own side, so the panel grows inwards from the corner rather than
  // across the screen.
  const alignLeft = rect.left + rect.width / 2 < viewportWidth / 2
  const left = alignLeft
    ? Math.min(rect.left, viewportWidth - width - VIEWPORT_MARGIN)
    : Math.max(VIEWPORT_MARGIN, rect.right - width)

  return {
    left: `${Math.max(VIEWPORT_MARGIN, left)}px`,
    top: `${top}px`,
    width: `${width}px`,
    height: `${height}px`,
    // The bubble's centre expressed in the panel's own coordinate space, so the morph runs out of
    // the circle rather than out of whichever corner happens to be nearest it.
    transformOrigin: `${rect.left + rect.width / 2 - left}px ${rect.top + rect.height / 2 - top}px`
  }
})

const close = (): void => {
  closePanel()
}

const onSend = async (body: string, references: FamilyChatReferenceDraft[] = []): Promise<void> => {
  // Sending is interaction: it pushes back the "nobody is really here" timeout that stops a panel
  // left open on a desk from swallowing notifications (#149).
  armInactivityTimeout()
  await send(body, references)
  streamRef.value?.scrollToBottom()
}

/**
 * The composer hands over a cropped `data:` URL; the API wants the base64 payload on its own, and
 * the stream wants the whole URL as its preview - so the split happens here rather than in either
 * of them.
 */
const onSendImage = async (dataUrl: string, caption?: string): Promise<void> => {
  const base64 = dataUrl.includes(',') ? dataUrl.split(',')[1]! : dataUrl
  await sendImage(base64, dataUrl, caption)
  streamRef.value?.scrollToBottom()
}

const onDelete = async (message: FamilyChatStreamMessage): Promise<void> => {
  await remove(message.publicId)
}

/**
 * Esc closes and hands focus back to the bubble; Tab stays inside the panel.
 *
 * The panel is not a modal dialog - it floats over a page that is still usable - so it gets
 * neither a focus trap nor Esc handling from anywhere else, and both are implemented here.
 */
const onCardKeyDown = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') {
    event.preventDefault()
    close()
    return
  }

  if (event.key !== 'Tab' || !cardEl.value) return

  const focusable = cardEl.value.querySelectorAll<HTMLElement>(
    'button, [href], input, textarea, select, [tabindex]:not([tabindex="-1"])'
  )
  if (focusable.length === 0) return

  const first = focusable[0]!
  const last = focusable[focusable.length - 1]!
  const active = document.activeElement

  if (event.shiftKey && active === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && active === last) {
    event.preventDefault()
    first.focus()
  }
}

/** Puts focus back where the user left it - on the bubble that opened the panel. */
const returnFocusToBubble = (): void => {
  if (!import.meta.client) return
  document.querySelector<HTMLElement>('[data-chat-bubble]')?.focus()
}

watch(panelOpen, async (isOpen) => {
  if (isOpen) {
    await open()
    await nextTick()
    streamRef.value?.scrollToBottom()
    // The caret goes in the composer, which is what someone opening a chat is about to use - but
    // not on a touch device, where focusing the input throws up the keyboard over the
    // conversation the moment it appears.
    if (!isMobile.value) await composerRef.value?.focus()
    return
  }

  await leaveChat()
  returnFocusToBubble()
})

onMounted(() => {
  window.addEventListener('resize', onViewportChange)
  window.addEventListener('orientationchange', onViewportChange)
  // The keyboard resizes the visual viewport without necessarily firing `resize` on the window.
  window.visualViewport?.addEventListener('resize', onViewportChange)
  window.visualViewport?.addEventListener('scroll', onViewportChange)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onViewportChange)
  window.removeEventListener('orientationchange', onViewportChange)
  window.visualViewport?.removeEventListener('resize', onViewportChange)
  window.visualViewport?.removeEventListener('scroll', onViewportChange)

  if (panelOpen.value) void leaveChat()
})
</script>

<style scoped>
.chat-panel-enter-active {
  transition: opacity var(--bubble-in) var(--bubble-ease-out), transform var(--bubble-in) var(--bubble-ease-pop);
}

.chat-panel-leave-active {
  transition: opacity var(--bubble-out) ease-in, transform var(--bubble-out) var(--bubble-ease-out);
}

.chat-panel-enter-from,
.chat-panel-leave-to {
  opacity: 0;
  /* Small enough to read as the circle it came from, not as a card that shrank. */
  transform: scale(0.2);
}

.chat-scrim-enter-active,
.chat-scrim-leave-active {
  transition: opacity var(--bubble-out) ease;
}

.chat-scrim-enter-from,
.chat-scrim-leave-to {
  opacity: 0;
}

@media (prefers-reduced-motion: reduce) {
  .chat-panel-enter-active,
  .chat-panel-leave-active {
    transition: opacity var(--bubble-out) ease;
  }

  .chat-panel-enter-from,
  .chat-panel-leave-to {
    transform: none;
  }
}
</style>
