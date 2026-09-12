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
          <!-- A picture (#147). The box is reserved from the stored dimensions before the bytes
               arrive, so the stream does not reflow as images load. -->
          <button
            v-if="message.kind === FamilyChatMessageKind.Image && imageSrc(message)"
            type="button"
            class="relative -mx-1 -mt-1 mb-1 block overflow-hidden rounded-xl"
            :style="{ aspectRatio: aspectRatio(message), width: '15rem', maxWidth: '100%' }"
            :aria-label="t('familyChat.image.open')"
            @click="openImage(message)"
          >
            <img
              :src="imageSrc(message)!"
              :alt="message.body || t('familyChat.image.alt')"
              class="h-full w-full object-cover"
              crossorigin="use-credentials"
              loading="lazy"
            >

            <!-- The upload has no byte-level progress (see useFamilyChat.sendImage); what this
                 says is "this one is still on its way", which is the part that matters. -->
            <span
              v-if="message.sendState === 'pending'"
              class="absolute inset-0 flex items-center justify-center bg-black/40"
            >
              <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-white" />
            </span>
          </button>

          <!-- Rendered as text nodes and anchors, never v-html: the body is user input, and a
               link is the only markup this app derives from it (#147). -->
          <p v-if="message.body" class="whitespace-pre-wrap break-words">
            <template v-for="(segment, index) in segmentsOf(message.body)" :key="index">
              <a
                v-if="segment.type === 'link'"
                :href="segment.href"
                target="_blank"
                rel="noopener noreferrer nofollow"
                class="underline underline-offset-2"
                :title="segment.href"
              >{{ segment.label }}</a>
              <template v-else>{{ segment.value }}</template>
            </template>
          </p>

          <!-- Things this message points at. Under the text, because the sentence is what was
               said and these are what it was about. -->
          <div v-if="message.references?.length" class="mt-1.5 flex flex-wrap gap-1">
            <component
              :is="reference.isAvailable ? 'button' : 'span'"
              v-for="reference in message.references"
              :key="reference.kind + reference.publicId"
              :type="reference.isAvailable ? 'button' : undefined"
              class="flex max-w-full items-center gap-1 rounded-full px-2 py-1 text-xs"
              :class="[
                own ? 'bg-white/15 text-white' : 'bg-default text-default',
                reference.isAvailable ? 'cursor-pointer' : 'cursor-default opacity-70'
              ]"
              @click="reference.isAvailable && openReference(reference)"
            >
              <UIcon :name="referenceIcon(reference.kind)" class="h-3.5 w-3.5 shrink-0" />
              <span class="truncate">{{ reference.label }}</span>
            </component>
          </div>

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

  <!-- The app's existing viewer, which is where pinch-zoom and the open/close transition already
       live. One image at a time: a chat is not a gallery, and the picture the reader tapped is
       the one they meant. -->
  <ImageLightbox
    v-model:open="lightboxOpen"
    :images="lightboxImages"
    :origin="lightboxOrigin"
  />

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
        <UButton color="neutral" variant="ghost" class="w-full justify-center" @click="() => { actionsOpen = false }">
          {{ t('common.cancel') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ImageLightbox, { type LightboxImage } from '~/components/ImageLightbox.vue'
import type { FamilyChatReference, FamilyChatStreamMessage } from '~/types/familyChat'
import { FamilyChatMessageKind } from '~/types/enums'
import { referenceIcon, referenceRoute, type SenderRun } from '~/utils/familyChat'
import { linkifySegments, type ChatTextSegment } from '~/utils/linkify'

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
const { mediaUrl } = useMediaUrl()
const haptics = useHaptics()
const { closePanel } = useFamilyChatBubble()

const actionsOpen = ref(false)
const actionTarget = ref<FamilyChatStreamMessage | null>(null)
const lightboxOpen = ref(false)
const lightboxImages = ref<LightboxImage[]>([])
const lightboxOrigin = ref<HTMLElement | null>(null)
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

// --- Pictures and links (#147) ---------------------------------------------

/** Message text split into plain and link segments. Never HTML — see `utils/linkify`. */
const segmentsOf = (body: string): ChatTextSegment[] => linkifySegments(body)

/**
 * What to draw for an image message: the uploaded thumbnail, or - while it is still uploading -
 * the local preview the sender picked, so their own photo is on screen immediately.
 */
const imageSrc = (message: FamilyChatStreamMessage): string | null => {
  if (message.localPreview) return message.localPreview
  // `mediaUrl` answers undefined for a path it cannot resolve; the lightbox and the template
  // both speak null, so it is normalised here rather than in three call sites.
  return message.imageUrl ? (mediaUrl(message.imageUrl) ?? null) : null
}

/**
 * The picture's own shape, so its box is the right size before the bytes arrive.
 *
 * A message with no stored dimensions (an optimistic row, whose picture has not been through the
 * server yet) falls back to 4:3 rather than collapsing to nothing - the box is replaced by the
 * real one as soon as the committed message lands.
 */
const aspectRatio = (message: FamilyChatStreamMessage): string => {
  if (message.imageWidth && message.imageHeight) {
    return `${message.imageWidth} / ${message.imageHeight}`
  }
  return '4 / 3'
}

const openImage = (message: FamilyChatStreamMessage): void => {
  const full = message.imageFullUrl ? mediaUrl(message.imageFullUrl) : imageSrc(message)
  if (!full) return

  lightboxImages.value = [{
    thumb: imageSrc(message),
    full,
    alt: message.body || null
  }]
  lightboxOpen.value = true
}

// --- References ------------------------------------------------------------

// The chip icon and route tables live in `utils/familyChat`, with the grouping rules: the
// composer renders chips too, and two copies of a map keyed by an enum are two places to forget
// a new kind.

/**
 * Opens what a chip points at, and closes the chat on the way.
 *
 * The panel floats over the page it was opened from, so leaving it up while navigating underneath
 * would land the reader on a screen they cannot see. A chip is a request to go and look at the
 * thing.
 */
const openReference = async (reference: FamilyChatReference): Promise<void> => {
  const route = referenceRoute(reference.kind, reference.publicId)
  if (!route) return

  haptics.tap()
  closePanel()
  await navigateTo(route)
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
