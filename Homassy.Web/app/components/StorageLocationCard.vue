<template>
  <div>
    <div
      class="p-4 rounded-xl shadow-sm transition-all duration-200 cursor-pointer hover:shadow-lg hover:-translate-y-0.5 slideInUp"
      :class="[
        isActive
          ? 'bg-gradient-to-br from-primary-50 to-primary-100/50 dark:from-primary-900/30 dark:to-primary-800/20'
          : 'bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900'
      ]"
      @click="handleCardClick"
    >
      <!-- Location Info -->
      <div class="space-y-2">
        <!-- Name with color indicator and freezer icon -->
        <div class="flex items-center gap-2">
          <!-- Color circle (8px diameter) -->
          <div
            v-if="location.color"
            class="w-2 h-2 rounded-full flex-shrink-0"
            :style="{ backgroundColor: location.color }"
          />

          <!-- Name -->
          <h3 
            class="font-semibold text-gray-900 dark:text-white flex-1 line-clamp-2"
            v-html="highlightText(location.name, searchQuery)"
          />

          <!-- Freezer icon -->
          <UIcon
            v-if="location.isFreezer"
            name="i-lucide-snowflake"
            class="h-4 w-4 text-blue-600 dark:text-blue-400 flex-shrink-0"
            :title="$t('profile.storageLocations.freezer')"
          />

          <!-- Family Shared Icon -->
          <UIcon
            v-if="location.isSharedWithFamily"
            name="i-lucide-users"
            class="h-4 w-4 text-primary-600 dark:text-primary-400 flex-shrink-0"
            :title="$t('profile.storageLocations.sharedWithFamily')"
          />

          <!-- Action Menu -->
          <UDropdownMenu :items="dropdownItems" size="md" class="flex-shrink-0">
            <UButton
              icon="i-lucide-ellipsis-vertical"
              size="sm"
              variant="ghost"
              @click.stop
            />
          </UDropdownMenu>
        </div>

        <!-- Description (only if not empty) -->
        <p
          v-if="location.description && location.description.trim() !== ''"
          class="text-sm text-gray-700 dark:text-gray-300 line-clamp-2"
          v-html="highlightText(location.description, searchQuery)"
        />
      </div>
    </div>

    <!-- Delete Modal -->
  <UModal :open="isDeleteModalOpen" @update:open="(val) => isDeleteModalOpen = val">
    <template #title>
      {{ $t('profile.storageLocations.deleteLocation') }}
    </template>

    <template #description>
      {{ $t('profile.storageLocations.deleteLocationWarning') }}
    </template>

    <template #body>
      <div class="space-y-3">
        <!-- Location Name -->
        <div>
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ $t('common.name') }}:
          </span>
          <span class="text-sm ml-2">{{ location.name }}</span>
        </div>

        <!-- Description if exists -->
        <div v-if="location.description">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ $t('common.description') }}:
          </span>
          <span class="text-sm ml-2">{{ location.description }}</span>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="outline"
          @click="closeDeleteModal"
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
import type { StorageLocationInfo } from '~/types/location'
import { useLocationsApi } from '~/composables/api/useLocationsApi'

interface Props {
  location: StorageLocationInfo
  isActive?: boolean
  searchQuery?: string
}

const props = withDefaults(defineProps<Props>(), {
  isActive: false,
  searchQuery: ''
})

const emit = defineEmits<{
  click: []
  edit: [location: StorageLocationInfo]
  deleted: [publicId: string]
}>()

const { t } = useI18n()
const toast = useToast()
const locationsApi = useLocationsApi()

// Modal states
const isDeleteModalOpen = ref(false)

// Loading states
const isDeleting = ref(false)

// Helper function to escape regex special characters
const escapeRegex = (str: string): string => {
  return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

// Helper function to highlight search text
const highlightText = (text: string, query: string): string => {
  if (!query || !text) return text
  
  const normalizedQuery = query.toLowerCase().trim()
  const normalizedText = text.toLowerCase()
  
  if (!normalizedText.includes(normalizedQuery)) return text
  
  const regex = new RegExp(`(${escapeRegex(normalizedQuery)})`, 'gi')
  return text.replace(regex, '<span class="font-bold text-primary-600 dark:text-primary-400 bg-primary-100 dark:bg-primary-900/30 px-1 py-0.5 rounded">$1</span>')
}

// Dropdown menu items
const dropdownItems = computed(() => {
  const items = [
    {
      label: t('common.edit'),
      icon: 'i-lucide-pencil',
      onSelect: () => emit('edit', props.location)
    },
    {
      label: t('common.delete'),
      icon: 'i-lucide-trash-2',
      color: 'error' as const,
      onSelect: openDeleteModal
    }
  ]

  return [items]
})

// Methods
const handleCardClick = (event: MouseEvent) => {
  // Ignore clicks on interactive elements
  const target = event.target as HTMLElement
  if (target.closest('button')) return
  
  emit('click')
}

const openDeleteModal = () => {
  isDeleteModalOpen.value = true
}

const closeDeleteModal = () => {
  isDeleteModalOpen.value = false
}

const handleDelete = async () => {
  isDeleting.value = true
  try {
    await locationsApi.deleteStorageLocation(props.location.publicId)

    closeDeleteModal()
    emit('deleted', props.location.publicId)
  } catch (error) {
    console.error('Failed to delete storage location:', error)
    toast.add({
      title: t('common.error'),
      description: t('profile.storageLocations.deleteFailed'),
      color: 'error'
    })
  } finally {
    isDeleting.value = false
  }
}
</script>
