<template>
  <UDrawer
    :open="open"
    :dismissible="false"
    :ui="{
      content: 'max-h-[85dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'min-h-0 overflow-y-auto p-4 sm:p-6',
      footer: 'shrink-0 flex flex-row items-center justify-end gap-2 border-t border-default p-4 sm:px-6'
    }"
    @update:open="(value) => emit('update:open', value)"
  >
    <template #header>
      <div ref="headerEl" class="flex items-center gap-3 w-full" style="touch-action: none">
        <UIcon v-if="icon" :name="icon" class="h-7 w-7 shrink-0 text-primary-500" />
        <DrawerTitle class="text-xl sm:text-2xl font-semibold">{{ title }}</DrawerTitle>
        <DrawerDescription class="sr-only">{{ title }}</DrawerDescription>
        <UButton
          class="ml-auto"
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          :aria-label="t('common.close')"
          @click="emit('update:open', false)"
        />
      </div>
    </template>

    <template #body>
      <slot name="body" />
    </template>

    <template #footer>
      <UButton
        :label="t('common.cancel')"
        color="neutral"
        variant="ghost"
        @click="onCancel"
      />
      <UButton
        :label="saveLabel || t('common.save')"
        color="primary"
        icon="i-lucide-save"
        :loading="loading"
        :disabled="saveDisabled"
        @click="emit('save')"
      />
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'

/**
 * Generic form bottom sheet with an explicit Save (name / display name, and any
 * future free-text field). Reuses the WizardDrawer chrome: native dismiss is
 * disabled so an accidental outside-tap / Esc doesn't drop a half-typed value;
 * drag the header down or hit ✕ / Cancel to close. Put inputs in the #body slot.
 */
const props = withDefaults(defineProps<{
  open: boolean
  title: string
  icon?: string
  loading?: boolean
  saveDisabled?: boolean
  saveLabel?: string
}>(), {
  icon: undefined,
  loading: false,
  saveDisabled: false,
  saveLabel: undefined
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  save: []
  cancel: []
}>()

const { t } = useI18n()

const headerEl = ref<HTMLElement | null>(null)
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => props.loading
})

function onCancel() {
  emit('cancel')
  emit('update:open', false)
}
</script>
