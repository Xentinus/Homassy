<template>
  <Transition name="chat-typing">
    <div
      v-if="members.length > 0"
      class="flex items-center gap-2 px-3 pb-1 text-xs text-gray-500 dark:text-gray-400"
      aria-live="polite"
    >
      <span class="typing-dots inline-flex items-end gap-0.5" aria-hidden="true">
        <span class="typing-dot" />
        <span class="typing-dot" />
        <span class="typing-dot" />
      </span>
      <span>{{ label }}</span>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { FamilyChatTypingMember } from '~/types/familyChat'

/**
 * "Anna is typing…" above the composer (#148).
 *
 * Pinned above the composer rather than appended to the stream, and it fades rather than expanding
 * the layout — an indicator that pushed the message list around every couple of seconds would move
 * the thing the reader is reading.
 *
 * Three phrasings, not a list that grows: one name, two names, and "several people". A chat with
 * four typists does not need four names, and a line that reflows as people start and stop is
 * harder to read than one that says how many.
 */

const props = defineProps<{
  members: FamilyChatTypingMember[]
}>()

const { t } = useI18n()

const label = computed(() => {
  const names = props.members.map(m => m.displayName).filter(Boolean)

  if (names.length === 1) return t('familyChat.typing.one', { name: names[0] })
  if (names.length === 2) return t('familyChat.typing.two', { first: names[0], second: names[1] })
  return t('familyChat.typing.several')
})
</script>

<style scoped>
.typing-dot {
  width: 4px;
  height: 4px;
  border-radius: 9999px;
  background-color: currentColor;
  animation: typing-bounce 1.2s infinite ease-in-out;
}

.typing-dot:nth-child(2) {
  animation-delay: 0.15s;
}

.typing-dot:nth-child(3) {
  animation-delay: 0.3s;
}

@keyframes typing-bounce {
  0%, 60%, 100% {
    transform: translateY(0);
    opacity: 0.4;
  }
  30% {
    transform: translateY(-3px);
    opacity: 1;
  }
}

.chat-typing-enter-active,
.chat-typing-leave-active {
  transition: opacity var(--bubble-out) ease;
}

.chat-typing-enter-from,
.chat-typing-leave-to {
  opacity: 0;
}

/* A static ellipsis instead of the bounce: the dots are decoration, and the sentence next to them
   already carries the whole message. */
@media (prefers-reduced-motion: reduce) {
  .typing-dot {
    animation: none;
    opacity: 0.6;
  }
}
</style>
