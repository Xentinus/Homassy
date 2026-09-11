<template>
  <div ref="rootEl" class="flex items-end gap-2 border-t border-default bg-default px-3 py-2">
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
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'

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
  /** Every keystroke while there is something to send - what #148 throttles its typing signal off. */
  typing: []
}>()

const { t } = useI18n()

const draft = ref('')
const rootEl = ref<HTMLElement | null>(null)

const canSend = computed(() => draft.value.trim().length > 0)

const submit = (): void => {
  if (!canSend.value) return

  emit('send', draft.value.trim())
  draft.value = ''
}

const onKeyDown = (event: KeyboardEvent): void => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    submit()
    return
  }

  if (draft.value.trim().length > 0) emit('typing')
}

/** Lets the panel put the caret in the composer when it opens. */
const focus = async (): Promise<void> => {
  await nextTick()
  rootEl.value?.querySelector('textarea')?.focus()
}

defineExpose({ focus })
</script>
