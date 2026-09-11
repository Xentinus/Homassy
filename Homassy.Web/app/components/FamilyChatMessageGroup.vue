<template>
  <div class="flex gap-2" :class="own ? 'flex-row-reverse' : 'flex-row'">
    <!-- One avatar per run, not per message: a run is one person speaking once, and repeating
         their face on every line is what makes a chat look like a log. Own messages get none at
         all - you know who you are, and the side of the screen already says so. -->
    <div v-if="!own" class="w-8 shrink-0 self-end">
      <UserAvatar
        :src="sender.profilePictureUrl ?? undefined"
        :name="sender.displayName"
        :public-id="sender.publicId"
        :identity-color="sender.identityColor"
        :size="32"
      />
    </div>

    <div class="flex min-w-0 max-w-[78%] flex-col gap-1" :class="own ? 'items-end' : 'items-start'">
      <!-- The run's header: who, and how long ago they started. Own runs carry only the age,
           since the name would be your own. -->
      <div class="flex items-center gap-2 px-1 text-xs text-gray-500 dark:text-gray-400">
        <span v-if="!own" class="font-medium" :style="nameStyle">{{ sender.displayName }}</span>
        <RelativeTime :date="run.startedAt" />
      </div>

      <div
        v-for="message in run.messages"
        :key="message.publicId"
        class="group relative w-full"
        :class="own ? 'flex justify-end' : 'flex justify-start'"
      >
        <div
          class="max-w-full rounded-2xl px-3 py-2 text-sm shadow-sm"
          :class="bubbleClass(message)"
          :style="own ? undefined : otherBubbleStyle"
          @pointerdown="onPointerDown(message, $event)"
          @pointerup="cancelLongPress"
          @pointercancel="cancelLongPress"
          @pointerleave="cancelLongPress"
          @contextmenu.prevent="openActions(message)"
        >
          <!-- Rendered as a text node, never v-html: the body is user input, and the only markup
               this app derives from it is the link handling in #147. -->
          <p class="whitespace-pre-wrap break-words">{{ message.body }}</p>

          <div
            class="mt-0.5 flex items-center justify-end gap-1 text-[10px] leading-none"
            :class="own ? 'text-white/70' : 'text-gray-500 dark:text-gray-400'"
          >
            <span>{{ formatTime(message.sentAt) }}</span>
            <UIcon
              v-if="message.sendState === 'pending'"
              name="i-lucide-clock"
              class="h-3 w-3"
              :aria-label="t('familyChat.state.pending')"
            />
            <UIcon
              v-else-if="message.sendState === 'failed'"
              name="i-lucide-alert-circle"
              class="h-3 w-3 text-error-200"
              :aria-label="t('familyChat.state.failed')"
            />
          </div>
        </div>

        <!-- A failed message keeps what was typed and offers the two ways out of it, rather than
             disappearing and losing the text. -->
        <div v-if="message.sendState === 'failed'" class="absolute -bottom-5 right-0 flex items-center gap-2 text-xs">
          <UButton size="xs" variant="link" color="error" @click="emit('retry', message)">
            {{ t('familyChat.state.retry') }}
          </UButton>
          <UButton size="xs" variant="link" color="neutral" @click="emit('discard', message)">
            {{ t('common.delete') }}
          </UButton>
        </div>
      </div>
    </div>
  </div>

  <UModal v-model:open="actionsOpen" :title="t('familyChat.actions.title')">
    <template #body>
      <div class="space-y-2">
        <UButton
          color="error"
          variant="soft"
          icon="i-lucide-trash-2"
          class="w-full justify-center"
          @click="confirmDelete"
        >
          {{ t('familyChat.actions.delete') }}
        </UButton>
        <UButton color="neutral" variant="ghost" class="w-full justify-center" @click="actionsOpen = false">
          {{ t('common.cancel') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { FamilyChatStreamMessage } from '~/types/familyChat'
import type { SenderRun } from '~/utils/familyChat'

/**
 * One sender run in the chat stream (#146): the avatar and name once, then that person's
 * consecutive messages under it.
 *
 * Own messages sit right without an avatar, everyone else's left with one — the arrangement every
 * chat uses, because the side of the screen answers "is this mine" before any colour or label
 * does. The sender's accent comes from the family identity colour (#114) rather than a palette
 * invented here: a member is one colour everywhere in this app, and a second scheme for the chat
 * would make the same person two different people.
 */

const props = defineProps<{
  run: SenderRun<FamilyChatStreamMessage>
  /** Whether this run is the current user's own. */
  own: boolean
}>()

const emit = defineEmits<{
  retry: [message: FamilyChatStreamMessage]
  discard: [message: FamilyChatStreamMessage]
  delete: [message: FamilyChatStreamMessage]
}>()

/** How long a press has to be held before the message's actions open. */
const LONG_PRESS_MS = 450

const { t } = useI18n()
const { formatTime } = useDateFormat()
const { accentStyle } = useMemberColor()
const haptics = useHaptics()

const actionsOpen = ref(false)
const actionTarget = ref<FamilyChatStreamMessage | null>(null)
let longPressTimer: ReturnType<typeof setTimeout> | null = null

const sender = computed(() => props.run.messages[0]!.sender)

const nameStyle = computed(() => ({
  ...accentStyle(sender.value.publicId, sender.value.identityColor),
  color: 'var(--member-color)'
}))

/** The speaker's accent as a left edge on their bubbles — the same cue the activity rows use. */
const otherBubbleStyle = computed(() => ({
  ...accentStyle(sender.value.publicId, sender.value.identityColor),
  borderLeftColor: 'var(--member-color)'
}))

const bubbleClass = (message: FamilyChatStreamMessage): string => {
  if (props.own) {
    const base = 'bg-primary-500 text-white rounded-br-sm'
    if (message.sendState === 'pending') return `${base} opacity-70`
    if (message.sendState === 'failed') return 'bg-error-500 text-white rounded-br-sm'
    return base
  }

  return 'bg-elevated text-default rounded-bl-sm border-l-2'
}

// --- Message actions -------------------------------------------------------

const openActions = (message: FamilyChatStreamMessage): void => {
  // Only your own messages have an action worth offering: deleting is the only one, and it is
  // own-messages-only on the server anyway (#144).
  if (!props.own || message.sendState === 'pending' || message.sendState === 'failed') return

  actionTarget.value = message
  actionsOpen.value = true
  haptics.select()
}

const onPointerDown = (message: FamilyChatStreamMessage, event: PointerEvent): void => {
  // Only the primary button/touch: a right-click already has `contextmenu` above.
  if (event.button !== 0) return

  cancelLongPress()
  longPressTimer = setTimeout(() => openActions(message), LONG_PRESS_MS)
}

const cancelLongPress = (): void => {
  if (longPressTimer) {
    clearTimeout(longPressTimer)
    longPressTimer = null
  }
}

const confirmDelete = (): void => {
  const target = actionTarget.value
  actionsOpen.value = false
  actionTarget.value = null

  if (target) {
    haptics.warning()
    emit('delete', target)
  }
}
</script>
