<template>
  <!-- Mobile: the app's standard bottom sheet, so drag-down closes it exactly the way every other
       sheet does. -->
  <AppDrawer
    v-if="isMobile"
    :open="panelOpen"
    :title="t('familyChat.title')"
    icon="i-lucide-message-circle"
    :padded="false"
    @update:open="onOpenChange"
  >
    <div class="flex h-full min-h-0 flex-col" :style="sheetMorphStyle">
      <FamilyChatStream
        ref="streamRef"
        :messages="messages"
        :loading="loading"
        :loading-older="loadingOlder"
        :has-older="hasOlder"
        :current-user-public-id="currentUserPublicId"
        @load-older="loadOlder"
        @retry="retry"
        @discard="(m) => discard(m.publicId)"
        @delete="onDelete"
      />
      <FamilyChatTypingIndicator :members="typingMembers" />
      <FamilyChatComposer ref="composerRef" @send="onSend" @image="onSendImage" @typing="notifyTyping" @idle="stopTyping" />
    </div>
  </AppDrawer>

  <!-- Desktop: a card anchored beside the bubble, morphing out of it. -->
  <Teleport v-else to="body">
    <Transition name="chat-panel">
      <div
        v-if="panelOpen"
        ref="cardEl"
        role="dialog"
        :aria-label="t('familyChat.title')"
        class="fixed z-[60] flex w-[min(24rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-2xl border border-default bg-default shadow-2xl"
        :style="cardStyle"
        @keydown="onCardKeyDown"
      >
        <header class="flex items-center gap-2 border-b border-default px-4 py-3">
          <UIcon name="i-lucide-message-circle" class="h-5 w-5 text-primary-500" />
          <h2 class="flex-1 text-sm font-semibold">{{ t('familyChat.title') }}</h2>
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
          class="h-[26rem]"
          @load-older="loadOlder"
          @retry="retry"
          @discard="(m) => discard(m.publicId)"
          @delete="onDelete"
        />
        <FamilyChatTypingIndicator :members="typingMembers" />
        <FamilyChatComposer ref="composerRef" @send="onSend" @image="onSendImage" @typing="notifyTyping" @idle="stopTyping" />
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import type { FamilyChatStreamMessage } from '~/types/familyChat'

/**
 * The family chat panel (#146) — the conversation the bubble opens.
 *
 * Two presentations, because the two platforms disagree about what a floating conversation is:
 *
 * - **Mobile** is a bottom sheet through `AppDrawer`, so drag-down-to-close, the backdrop and the
 *   focus trap are the app's existing ones rather than a second implementation of each. The morph
 *   is applied to the sheet's *contents*, anchored at the bubble's own x: vaul owns the transform
 *   on the sheet element itself while it animates, so a second transform on that element would
 *   fight it, and the result reads as the panel growing out of the bubble while the sheet arrives.
 * - **Desktop** is a card anchored beside the bubble, where the full morph is available: the
 *   card's `transform-origin` is the bubble's live centre, so it scales and fades out of exactly
 *   the circle that was tapped, and collapses back into it on close.
 *
 * The bubble deliberately stays on screen while the panel is open — it is the panel's handle, and
 * the thing the morph is anchored to. `FamilyChatBubble` makes that exception explicit.
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
  notifyTyping,
  stopTyping,
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

/** Gap between the bubble and the card anchored next to it. */
const ANCHOR_GAP = 12
/** Card size used for placement before it has been measured. */
const CARD_WIDTH = 384
const CARD_HEIGHT = 560

const anchorCentre = computed(() => {
  const rect = anchorRect.value
  if (!rect) return null
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 }
})

/**
 * The sheet's contents grow from the bubble's horizontal position rather than from the middle, so
 * a bubble parked on the left opens leftwards and one on the right opens rightwards.
 */
const sheetMorphStyle = computed(() => {
  const centre = anchorCentre.value
  if (!centre || !import.meta.client) return undefined

  // Under reduced motion the sheet simply arrives; vaul's own slide is already suppressed by the
  // user's setting at the browser level, and a growth animation here would put back the movement
  // they asked not to see.
  if (prefersReducedMotion()) return undefined

  const originX = Math.round((centre.x / window.innerWidth) * 100)
  return {
    transformOrigin: `${originX}% 100%`,
    animation: 'chat-panel-grow var(--bubble-in) var(--bubble-ease-pop) both'
  }
})

const prefersReducedMotion = (): boolean =>
  import.meta.client && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Where the desktop card sits: beside the bubble, on whichever side has room, and clamped into
 * the viewport so it is never half off-screen.
 */
const cardStyle = computed(() => {
  const rect = anchorRect.value
  if (!rect || !import.meta.client) {
    return { right: '1.5rem', bottom: '1.5rem', transformOrigin: 'bottom right' }
  }

  const openLeft = rect.left + rect.width / 2 > window.innerWidth / 2
  const left = openLeft
    ? Math.max(16, rect.left - ANCHOR_GAP - CARD_WIDTH)
    : Math.min(window.innerWidth - CARD_WIDTH - 16, rect.right + ANCHOR_GAP)

  const top = Math.min(
    Math.max(16, rect.top + rect.height / 2 - CARD_HEIGHT / 2),
    Math.max(16, window.innerHeight - CARD_HEIGHT - 16)
  )

  // The origin is the bubble's own centre expressed in the card's coordinate space, so the
  // morph runs out of the circle rather than out of a corner near it.
  return {
    left: `${left}px`,
    top: `${top}px`,
    transformOrigin: `${rect.left + rect.width / 2 - left}px ${rect.top + rect.height / 2 - top}px`
  }
})

const onOpenChange = (value: boolean): void => {
  if (!value) close()
}

const close = (): void => {
  closePanel()
}

const onSend = async (body: string): Promise<void> => {
  await send(body)
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
 * Esc closes and hands focus back to the bubble; Tab stays inside the card.
 *
 * The desktop card is not a modal dialog - it floats over a page that is still usable - so it
 * gets neither Reka's focus trap nor its Esc handling for free, and both are implemented here.
 * The mobile sheet needs none of this: `AppDrawer` is a real dialog and already does it.
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
    // The caret goes in the composer, which is what someone opening a chat is about to use.
    await composerRef.value?.focus()
    return
  }

  await leaveChat()
  returnFocusToBubble()
})

onBeforeUnmount(() => {
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
