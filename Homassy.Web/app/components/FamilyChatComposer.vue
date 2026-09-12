<template>
  <!-- What is about to be attached, above the input rather than inside it: these are things, not
       text, and a chip inside a textarea cannot be removed with a backspace. -->
  <div v-if="pending.length > 0" class="flex flex-wrap gap-2 border-t border-default bg-default px-3 pt-3">
    <span
      v-for="reference in pending"
      :key="reference.kind + reference.publicId"
      class="flex max-w-full items-center gap-1 rounded-full bg-elevated py-1 pl-2 pr-1 text-xs"
    >
      <UIcon :name="referenceIcon(reference.kind)" class="h-3.5 w-3.5 shrink-0 text-primary-500" />
      <span class="truncate">{{ reference.label }}</span>
      <UButton
        icon="i-lucide-x"
        color="neutral"
        variant="ghost"
        size="xs"
        :aria-label="t('familyChat.reference.remove', { name: reference.label })"
        @click="removeReference(reference)"
      />
    </span>
  </div>

  <!-- The row is sized for thumbs, not for a desktop form: 44px controls, a 16px input (anything
       smaller makes iOS zoom the page on focus) and a bottom pad that clears the home indicator. -->
  <div
    ref="rootEl"
    class="flex items-end gap-2 border-t border-default bg-default px-3 pt-3"
    :class="pending.length > 0 ? 'border-t-0' : ''"
    style="padding-bottom: calc(0.75rem + env(safe-area-inset-bottom, 0px))"
  >
    <!-- One picker for camera and gallery: `accept="image/*"` on a phone offers both, and a
         separate "take a photo" button would be a second control for the same thing. -->
    <input
      ref="fileInput"
      type="file"
      accept="image/*"
      class="hidden"
      @change="onFilePicked"
    >

    <UButton
      icon="i-lucide-image"
      color="neutral"
      variant="ghost"
      size="lg"
      :aria-label="t('familyChat.composer.attach')"
      class="h-11 w-11 shrink-0 justify-center"
      @click="fileInput?.click()"
    />

    <UButton
      icon="i-lucide-plus"
      color="neutral"
      variant="ghost"
      size="lg"
      :disabled="pending.length >= MAX_REFERENCES"
      :aria-label="t('familyChat.composer.attachReference')"
      class="h-11 w-11 shrink-0 justify-center"
      @click="() => { pickerOpen = true }"
    />

    <UTextarea
      v-model="draft"
      :rows="1"
      :maxrows="6"
      autoresize
      size="lg"
      :placeholder="t('familyChat.composer.placeholder')"
      :aria-label="t('familyChat.composer.placeholder')"
      class="flex-1"
      :ui="{ base: 'resize-none min-h-11 py-2.5 text-base leading-6' }"
      @keydown="onKeyDown"
      @blur="onBlur"
    />

    <UButton
      icon="i-lucide-send"
      color="primary"
      size="lg"
      :disabled="!canSend"
      :aria-label="t('familyChat.composer.send')"
      class="h-11 w-11 shrink-0 justify-center"
      @click="submit"
    />
  </div>

  <FamilyChatReferencePicker v-model:open="pickerOpen" @pick="addReference" />

  <!-- The app's existing cropper, which is also where the "preview before sending" happens: the
       crop screen *is* the preview, so there is no second confirm step to build. -->
  <ImageCropper
    :is-open="cropperOpen"
    :image-src="pickedImageSrc"
    :default-aspect-ratio="0"
    @close="closeCropper"
    @cropped="onCropped"
  />
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import ImageCropper from '~/components/ImageCropper.vue'
import FamilyChatReferencePicker from '~/components/FamilyChatReferencePicker.vue'
import { base64ToBlob, blobToBase64, compressImage } from '~/composables/useImageCrop'
import type { FamilyChatReferenceDraft } from '~/types/familyChat'
import { referenceIcon } from '~/utils/familyChat'

/**
 * The chat's input row (#146).
 *
 * Deliberately dumb: it owns the draft and nothing else. Sending, optimistic appending and
 * failure handling all belong to `useFamilyChat`, and the panel is what wires the two together —
 * so this component stays the one place the *input* behaves, and there is no second copy of the
 * send rules living in a component.
 *
 * Enter sends, Shift+Enter puts in a newline: the desktop convention, and on a touch keyboard the
 * Enter key is a newline key anyway, which is why the send button is always there.
 */

const emit = defineEmits<{
  send: [body: string, references: FamilyChatReferenceDraft[]]
  /** A cropped picture, as a `data:` URL, plus whatever was in the draft as its caption (#147). */
  image: [dataUrl: string, caption: string | undefined]
  /** Every keystroke while there is something to send - what #148 throttles its typing signal off. */
  typing: []
  /** The composer was left empty - nothing is being typed any more (#148). */
  idle: []
}>()

const { t } = useI18n()

const draft = ref('')
const rootEl = ref<HTMLElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const pickedImageSrc = ref('')
const cropperOpen = ref(false)
const pickerOpen = ref(false)

/** Matches `SendFamilyChatMessageRequest.MaxReferences` on the API. */
const MAX_REFERENCES = 5

/** What is attached but not yet sent. The label is only for the chip; the server resolves its own. */
const pending = ref<FamilyChatReferenceDraft[]>([])

// Something attached is something to send, even with nothing typed: "the shop" plus "the milk" is
// a message, and the API accepts a body-less message that carries references.
const canSend = computed(() => draft.value.trim().length > 0 || pending.value.length > 0)

const addReference = (reference: FamilyChatReferenceDraft): void => {
  if (pending.value.length >= MAX_REFERENCES) return
  // The same thing twice is a fumbled tap, not a second attachment.
  if (pending.value.some(r => r.kind === reference.kind && r.publicId === reference.publicId)) return

  pending.value = [...pending.value, reference]
}

const removeReference = (reference: FamilyChatReferenceDraft): void => {
  pending.value = pending.value.filter(r => !(r.kind === reference.kind && r.publicId === reference.publicId))
}

const submit = (): void => {
  if (!canSend.value) return

  emit('send', draft.value.trim(), [...pending.value])
  draft.value = ''
  pending.value = []
  emit('idle')
}

/**
 * Leaving an empty composer means the typing is over; leaving a half-written one does not.
 *
 * Someone who taps away mid-sentence is still writing it, and the server flag expires on its own
 * a few seconds later if they never come back - which is the right outcome either way.
 */
const onBlur = (): void => {
  if (draft.value.trim().length === 0) emit('idle')
}

const onKeyDown = (event: KeyboardEvent): void => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    submit()
  }
}

/**
 * Typing is reported from the *value*, not from keydown.
 *
 * At keydown time the model still holds what was there before the key, so the first character of
 * a message would not count as typing and the last deletion would. Watching the draft also covers
 * paste and dictation, neither of which is a keystroke.
 */
watch(draft, (value, previous) => {
  if (value.trim().length > 0) {
    emit('typing')
  } else if (previous.trim().length > 0) {
    // Emptied the composer: whatever was being written is not being written any more.
    emit('idle')
  }
})

// --- Pictures (#147) -------------------------------------------------------

const onFilePicked = async (event: Event): Promise<void> => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  // Reset immediately, so picking the same file twice in a row still fires `change`.
  input.value = ''

  if (!file) return

  pickedImageSrc.value = await blobToBase64(file)
  cropperOpen.value = true
}

const closeCropper = (): void => {
  cropperOpen.value = false
  pickedImageSrc.value = ''
}

/**
 * The cropper hands back a `data:` URL. It is compressed here rather than sent as-is: a modern
 * phone photo is several megabytes, most of which the 1600px rendition the server stores would
 * throw away anyway - so the bytes are better not put on the wire at all.
 */
const onCropped = async (dataUrl: string): Promise<void> => {
  closeCropper()

  const caption = draft.value.trim() || undefined
  draft.value = ''

  try {
    const compressed = await compressImage(await base64ToBlob(dataUrl), { maxSizePx: 1600, maxSizeMB: 1 })
    emit('image', await blobToBase64(compressed), caption)
  } catch {
    // Compression is an optimisation, not a gate: if it fails, the original still goes - the
    // server validates and resizes it either way.
    emit('image', dataUrl, caption)
  }
}

/** Lets the panel put the caret in the composer when it opens. */
const focus = async (): Promise<void> => {
  await nextTick()
  rootEl.value?.querySelector('textarea')?.focus()
}

defineExpose({ focus })
</script>
