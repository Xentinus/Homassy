<template>
  <div>
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
          <h3 class="font-semibold text-gray-900 dark:text-white truncate">
            {{ passkey.displayName }}
          </h3>
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

    <!-- Delete Confirmation Modal -->
    <UModal :open="isDeleteModalOpen" @update:open="(val) => isDeleteModalOpen = val">
      <template #title>
        {{ $t('profile.security.deletePasskey') }}
      </template>

      <template #description>
        {{ $t('profile.security.deletePasskeyWarning') }}
      </template>

      <template #body>
        <div class="space-y-3">
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('common.name') }}:
            </span>
            <span class="text-sm ml-2">{{ passkey.displayName }}</span>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('common.cancel')"
            color="neutral"
            variant="outline"
            @click="isDeleteModalOpen = false"
          />
          <UButton
            :label="$t('common.delete')"
            color="error"
            :loading="isDeleting"
            @click="handleDelete"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { WebAuthnCredential } from '~/composables/useKratos'

interface Props {
  passkey: WebAuthnCredential
}

const props = defineProps<Props>()

const emit = defineEmits<{
  delete: [id: string]
}>()

const { t, locale } = useI18n()

// Modal state
const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

// Format date based on locale
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

// Dropdown menu items
const dropdownItems = computed(() => [
  [
    {
      label: t('common.delete'),
      icon: 'i-lucide-trash-2',
      color: 'error' as const,
      onSelect: () => {
        isDeleteModalOpen.value = true
      }
    }
  ]
])

async function handleDelete() {
  isDeleting.value = true
  try {
    emit('delete', props.passkey.id)
  } finally {
    isDeleting.value = false
    isDeleteModalOpen.value = false
  }
}
</script>
