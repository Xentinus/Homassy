<template>
  <div ref="rootEl" class="flex items-end gap-2 border-t border-default bg-default px-3 py-2">
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
      :aria-label="t('familyChat.composer.attach')"
      class="mb-0.5 shrink-0"
      @click="fileInput?.click()"
    />

    <UTextarea
      v-model="draft"
      :rows="1"
      :maxrows="5"
      autoresize
      :placeholder="t('familyChat.composer.placeholder')"
      :aria-label="t('familyChat.composer.placeholder')"
      class="flex-1"
      :ui="{ base: 'resize-none' }"
      @keydown="onKeyDown"
      @blur="onBlur"
    />

    <UButton
      icon="i-lucide-send"
      color="primary"
      :disabled="!canSend"
      :aria-label="t('familyChat.composer.send')"
      class="mb-0.5 shrink-0"
      @click="submit"
    />
  </div>

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
import { base64ToBlob, blobToBase64, compressImage } from '~/composables/useImageCrop'

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
  send: [body: string]
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

const canSend = computed(() => draft.value.trim().length > 0)

const submit = (): void => {
  if (!canSend.value) return

  emit('send', draft.value.trim())
  draft.value = ''
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
