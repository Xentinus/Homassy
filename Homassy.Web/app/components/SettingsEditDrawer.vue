<template>
  <AppDrawer
    :open="open"
    :title="title"
    :icon="icon"
    :loading="loading"
    @update:open="(value) => emit('update:open', value)"
  >
    <slot name="body" />

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
  </AppDrawer>
</template>

<script setup lang="ts">
/**
 * Generic form bottom sheet with an explicit Save (name / display name, and any
 * future free-text field). Reuses the WizardDrawer chrome: native dismiss is
 * disabled so an accidental outside-tap / Esc doesn't drop a half-typed value;
 * drag the header down or hit ✕ / Cancel to close. Put inputs in the #body slot.
 */
withDefaults(defineProps<{
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

function onCancel() {
  emit('cancel')
  emit('update:open', false)
}
</script>
