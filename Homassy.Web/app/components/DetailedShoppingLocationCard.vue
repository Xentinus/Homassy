<template>
  <div>
    <div
      class="p-4 rounded-lg border-2 transition-all duration-200 cursor-pointer hover:shadow-md"
      :class="[
        isActive
          ? 'border-primary-500 bg-primary-50 dark:bg-primary-900/10'
          : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'
      ]"
      @click="handleCardClick"
    >
      <!-- Location Info -->
      <div class="space-y-3">
        <!-- Name with color indicator and icons -->
        <div class="flex items-center gap-2">
          <!-- Color circle (8px diameter) -->
          <div
            v-if="location.color"
            class="w-2 h-2 rounded-full flex-shrink-0"
            :style="{ backgroundColor: location.color }"
          />

          <!-- Name -->
          <h3 class="font-semibold text-sm line-clamp-2 text-gray-900 dark:text-white flex-1">
            {{ location.name }}
          </h3>

          <!-- Family Shared Icon -->
          <UIcon
            v-if="location.isSharedWithFamily"
            name="i-lucide-users"
            class="h-4 w-4 text-primary-500 flex-shrink-0"
            :title="$t('profile.shoppingLocations.sharedWithFamily')"
          />

          <!-- Action Menu -->
          <UDropdownMenu :items="dropdownItems" size="md" class="flex-shrink-0">
            <UButton
              icon="i-lucide-ellipsis-vertical"
              size="sm"
              variant="subtle"
              @click.stop
            />
          </UDropdownMenu>
        </div>

        <!-- Address Block -->
        <div
          v-if="location.country || location.city || location.postalCode || location.address"
          class="text-xs text-gray-600 dark:text-gray-400 flex items-start gap-1"
        >
          <UIcon name="i-lucide-map-pin" class="h-3 w-3 flex-shrink-0 mt-0.5" />
          <div class="flex-1">
            <div v-if="location.country">{{ location.country }}</div>
            <div v-if="location.city || location.postalCode">
              {{ location.city }}{{ location.city && location.postalCode ? ' ' : '' }}{{ location.postalCode }}
            </div>
            <div v-if="location.address">{{ location.address }}</div>
          </div>
        </div>

        <!-- Description -->
        <p
          v-if="location.description && location.description.trim() !== ''"
          class="text-xs text-gray-600 dark:text-gray-400 line-clamp-2"
        >
          {{ location.description }}
        </p>

        <!-- External Links -->
        <div v-if="location.website || location.googleMaps" class="flex gap-2">
          <UButton
            v-if="location.website"
            icon="i-lucide-external-link"
            :label="$t('profile.shoppingLocations.website')"
            size="xs"
            color="neutral"
            variant="outline"
            :to="location.website"
            target="_blank"
            rel="noopener noreferrer"
            @click.stop
          />
          <UButton
            v-if="location.googleMaps"
            icon="i-lucide-map"
            :label="$t('profile.shoppingLocations.googleMaps')"
            size="xs"
            color="neutral"
            variant="outline"
            :to="location.googleMaps"
            target="_blank"
            rel="noopener noreferrer"
            @click.stop
          />
        </div>
      </div>
    </div>

    <!-- Edit Modal -->
    <UModal :open="isEditModalOpen" @update:open="(val) => isEditModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('profile.shoppingLocations.editLocation') }}
      </template>

      <template #description>
        {{ $t('profile.shoppingLocations.editLocationDescription') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Name -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.name') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="editForm.name"
              type="text"
              class="w-full"
              required
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.description') }}
            </label>
            <UTextarea
              v-model="editForm.description"
              :placeholder="$t('common.description')"
              class="w-full"
            />
          </div>

          <!-- Color -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.color') }}
            </label>
            <div v-if="editForm.color === null" class="flex gap-2 items-center">
              <UButton
                icon="i-lucide-palette"
                :label="$t('common.addColor')"
                color="neutral"
                variant="outline"
                class="flex-1"
                @click="editForm.color = '#3B82F6'"
              />
            </div>
            <div v-else class="flex gap-2 items-center">
              <UInput
                v-model="editForm.color"
                type="color"
                class="flex-1"
              />
              <UButton
                icon="i-lucide-x"
                color="neutral"
                variant="ghost"
                size="sm"
                @click="editForm.color = null"
              />
            </div>
          </div>

          <!-- Address -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.address') }}
            </label>
            <UInput
              v-model="editForm.address"
              type="text"
              :placeholder="$t('common.address')"
              class="w-full"
            />
          </div>

          <!-- City -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.city') }}
            </label>
            <UInput
              v-model="editForm.city"
              type="text"
              :placeholder="$t('common.city')"
              class="w-full"
            />
          </div>

          <!-- Postal Code -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.postalCode') }}
            </label>
            <UInput
              v-model="editForm.postalCode"
              type="text"
              :placeholder="$t('common.postalCode')"
              class="w-full"
            />
          </div>

          <!-- Country -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.country') }}
            </label>
            <UInput
              v-model="editForm.country"
              type="text"
              :placeholder="$t('common.country')"
              class="w-full"
            />
          </div>

          <!-- Website -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.website') }}
            </label>
            <UInput
              v-model="editForm.website"
              type="url"
              :placeholder="$t('common.website')"
              class="w-full"
            />
          </div>

          <!-- Google Maps -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('profile.shoppingLocations.googleMaps') }}
            </label>
            <UInput
              v-model="editForm.googleMaps"
              type="url"
              :placeholder="$t('profile.shoppingLocations.googleMaps')"
              class="w-full"
            />
          </div>

          <!-- Is Shared With Family -->
          <div class="flex items-center gap-2">
            <UCheckbox
              v-model="editForm.isSharedWithFamily"
              :label="$t('profile.shoppingLocations.isSharedWithFamily')"
            />
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('common.cancel')"
            color="neutral"
            variant="outline"
            @click="closeEditModal"
          />
          <UButton
            :label="$t('common.save')"
            :loading="isUpdating"
            @click="handleUpdate"
          />
        </div>
      </template>
    </UModal>

    <!-- Delete Modal -->
    <UModal :open="isDeleteModalOpen" @update:open="(val) => isDeleteModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('profile.shoppingLocations.deleteLocation') }}
      </template>

      <template #description>
        {{ $t('profile.shoppingLocations.deleteLocationWarning') }}
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
import type { ShoppingLocationInfo } from '~/types/location'
import { useLocationsApi } from '~/composables/api/useLocationsApi'

interface Props {
  location: ShoppingLocationInfo
  isActive?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  isActive: false
})

const emit = defineEmits<{
  click: []
  updated: []
  deleted: []
}>()

const { t } = useI18n()
const toast = useToast()
const locationsApi = useLocationsApi()

// Modal states
const isEditModalOpen = ref(false)
const isDeleteModalOpen = ref(false)

// Loading states
const isUpdating = ref(false)
const isDeleting = ref(false)

// Edit form
const editForm = ref<{
  name: string
  description: string
  color: string | null
  address: string
  city: string
  postalCode: string
  country: string
  website: string
  googleMaps: string
  isSharedWithFamily: boolean
}>({
  name: '',
  description: '',
  color: null,
  address: '',
  city: '',
  postalCode: '',
  country: '',
  website: '',
  googleMaps: '',
  isSharedWithFamily: false
})

// Dropdown menu items
const dropdownItems = computed(() => {
  const items = [
    {
      label: t('common.edit'),
      icon: 'i-lucide-pencil',
      onSelect: openEditModal
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
  if (target.closest('button') || target.closest('a')) return
  
  emit('click')
}

const isValidUrl = (url: string): boolean => {
  if (!url.trim()) return true // Empty is valid
  try {
    new URL(url)
    return true
  } catch {
    return false
  }
}

const openEditModal = () => {
  editForm.value = {
    name: props.location.name,
    description: props.location.description || '',
    color: props.location.color || null,
    address: props.location.address || '',
    city: props.location.city || '',
    postalCode: props.location.postalCode || '',
    country: props.location.country || '',
    website: props.location.website || '',
    googleMaps: props.location.googleMaps || '',
    isSharedWithFamily: props.location.isSharedWithFamily
  }
  isEditModalOpen.value = true
}

const closeEditModal = () => {
  isEditModalOpen.value = false
}

const handleUpdate = async () => {
  if (!editForm.value.name.trim()) {
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.nameRequired'),
      color: 'error'
    })
    return
  }

  // Validate URLs
  if (editForm.value.website && !isValidUrl(editForm.value.website)) {
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.invalidWebsite'),
      color: 'error'
    })
    return
  }

  if (editForm.value.googleMaps && !isValidUrl(editForm.value.googleMaps)) {
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.invalidGoogleMaps'),
      color: 'error'
    })
    return
  }

  isUpdating.value = true
  try {
    await locationsApi.updateShoppingLocation(props.location.publicId, {
      name: editForm.value.name,
      description: editForm.value.description || undefined,
      color: editForm.value.color === null ? '' : editForm.value.color,
      address: editForm.value.address || undefined,
      city: editForm.value.city || undefined,
      postalCode: editForm.value.postalCode || undefined,
      country: editForm.value.country || undefined,
      website: editForm.value.website || undefined,
      googleMaps: editForm.value.googleMaps || undefined,
      isSharedWithFamily: editForm.value.isSharedWithFamily
    })

    closeEditModal()
    emit('updated')
  } catch (error) {
    console.error('Failed to update shopping location:', error)
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.updateFailed'),
      color: 'error'
    })
  } finally {
    isUpdating.value = false
  }
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
    await locationsApi.deleteShoppingLocation(props.location.publicId)

    closeDeleteModal()
    emit('deleted')
  } catch (error) {
    console.error('Failed to delete shopping location:', error)
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.deleteFailed'),
      color: 'error'
    })
  } finally {
    isDeleting.value = false
  }
}
</script>
