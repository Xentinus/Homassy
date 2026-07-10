<template>
  <div
    class="p-4 rounded-xl shadow-sm transition-all duration-200 bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900"
  >
    <div class="flex items-center gap-3">
      <!-- Passkey Icon -->
      <div class="w-10 h-10 rounded-full bg-primary-100 dark:bg-primary-900/30 flex items-center justify-center flex-shrink-0">
        <UIcon name="i-heroicons-finger-print" class="w-5 h-5 text-primary-600 dark:text-primary-400" />
      </div>

      <!-- Info -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <h3 class="font-semibold text-gray-900 dark:text-white truncate">
            {{ passkey.displayName }}
          </h3>
          <UBadge
            v-if="passkey.canDelete === false"
            color="warning"
            variant="soft"
            size="xs"
          >
            {{ $t('profile.security.lastPasskey') }}
          </UBadge>
        </div>
        <p v-if="formattedDate" class="text-xs text-gray-500 dark:text-gray-400">
          {{ $t('profile.security.addedOn') }} {{ formattedDate }}
        </p>
      </div>

      <!-- Action Menu -->
      <UDropdownMenu :items="dropdownItems" size="md" class="flex-shrink-0">
        <UButton
          icon="i-lucide-ellipsis-vertical"
          size="sm"
          variant="ghost"
        />
      </UDropdownMenu>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { WebAuthnCredential } from '~/composables/useKratos'

interface Props {
  passkey: WebAuthnCredential
}

const props = defineProps<Props>()

// The delete confirmation lives in the parent (SecurityDrawer) as an inline
// view, so this card just requests deletion — no nested modal.
const emit = defineEmits<{
  delete: [id: string]
}>()

const { t, locale } = useI18n()

const formattedDate = computed(() => {
  if (!props.passkey.createdAt) return null
  try {
    return new Date(props.passkey.createdAt).toLocaleDateString(locale.value, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    })
  } catch {
    return null
  }
})

const dropdownItems = computed(() => {
  const canDelete = props.passkey.canDelete !== false

  return [
    [
      {
        label: canDelete ? t('common.delete') : t('profile.security.cannotDeleteLastPasskey'),
        icon: 'i-lucide-trash-2',
        color: canDelete ? 'error' as const : 'neutral' as const,
        disabled: !canDelete,
        onSelect: () => {
          if (canDelete) emit('delete', props.passkey.id)
        }
      }
    ]
  ]
})
</script>
