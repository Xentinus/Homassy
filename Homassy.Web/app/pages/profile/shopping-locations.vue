<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <NuxtLink to="/profile">
            <UButton
              icon="i-lucide-arrow-left"
              color="neutral"
              variant="ghost"
            />
          </NuxtLink>
          <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('profile.shoppingLocations.title') }}</h1>
        </div>
        <UButton 
          color="primary" 
          size="sm" 
          trailing-icon="i-lucide-plus"
          @click="handleAddLocation"
        >
          {{ $t('common.add') }}
        </UButton>
      </div>
      
      <!-- Search Input -->
      <div class="w-full">
        <UInput
          v-model="searchQuery"
          trailing-icon="i-lucide-search"
          :placeholder="$t('profile.shoppingLocations.searchPlaceholder')"
          class="w-full"
        />
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-40 px-4 sm:px-8 lg:px-14 pb-6">

    <!-- Loading State -->
    <template v-if="loading">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <USkeleton v-for="i in 6" :key="i" class="h-32 w-full rounded-lg" />
      </div>
    </template>

    <!-- Empty State -->
    <div v-else-if="filteredLocations.length === 0" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-12 text-center">
      <UIcon name="i-lucide-shopping-cart" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
      <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
        {{ searchQuery ? $t('profile.shoppingLocations.noResults') : $t('profile.shoppingLocations.noLocations') }}
      </p>
      <p class="text-gray-600 dark:text-gray-400">
        {{ searchQuery ? $t('profile.shoppingLocations.tryDifferentSearch') : $t('profile.shoppingLocations.addFirstLocation') }}
      </p>
    </div>

    <!-- Locations Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <DetailedShoppingLocationCard
        v-for="location in filteredLocations"
        :key="location.publicId"
        :location="location"
        :search-query="searchQuery"
        @click="handleLocationClick(location)"
        @updated="loadLocations"
        @deleted="loadLocations"
      />
    </div>
    </div>

  <!-- Create Modal -->
  <UModal :open="isCreateModalOpen" @update:open="(val) => isCreateModalOpen = val" :dismissible="false">
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
            v-model="createForm.name"
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
            v-model="createForm.description"
            :placeholder="$t('common.description')"
            class="w-full"
          />
        </div>

        <!-- Color -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('common.color') }}
          </label>
          <div v-if="createForm.color === null" class="flex gap-2 items-center">
            <UButton
              icon="i-lucide-palette"
              :label="$t('common.addColor')"
              color="neutral"
              variant="outline"
              class="flex-1"
              @click="createForm.color = '#3B82F6'"
            />
          </div>
          <div v-else class="flex gap-2 items-center">
            <input
              v-model="createForm.color"
              type="color"
              class="flex-1 h-10 rounded-md border border-gray-300 dark:border-gray-700 bg-white dark:bg-gray-800 cursor-pointer"
            >
            <UButton
              icon="i-lucide-x"
              color="neutral"
              variant="ghost"
              size="sm"
              @click.stop="createForm.color = null"
            />
          </div>
        </div>

        <!-- Address -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('common.address') }}
          </label>
          <UInput
            v-model="createForm.address"
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
            v-model="createForm.city"
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
            v-model="createForm.postalCode"
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
            v-model="createForm.country"
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
            v-model="createForm.website"
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
            v-model="createForm.googleMaps"
            type="url"
            :placeholder="$t('profile.shoppingLocations.googleMaps')"
            class="w-full"
          />
        </div>

        <!-- Is Shared With Family -->
        <div class="flex items-center gap-2">
          <UCheckbox
            v-model="createForm.isSharedWithFamily"
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
          @click="closeCreateModal"
        />
        <UButton
          :label="$t('common.save')"
          :loading="isCreating"
          @click="handleCreate"
        />
      </div>
    </template>
  </UModal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useLocationsApi } from '~/composables/api/useLocationsApi'
import type { ShoppingLocationInfo } from '~/types/location'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getShoppingLocations, createShoppingLocation } = useLocationsApi()
const { t } = useI18n()
const toast = useToast()

const loading = ref(true)
const locations = ref<ShoppingLocationInfo[]>([])
const searchQuery = ref('')

// Create modal state
const isCreateModalOpen = ref(false)
const isCreating = ref(false)

// Create form
const createForm = ref<{
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

// Filtered locations based on search
const filteredLocations = computed(() => {
  if (!searchQuery.value.trim()) {
    return locations.value
  }
  
  const query = searchQuery.value.toLowerCase()
  return locations.value.filter(loc => 
    loc.name.toLowerCase().includes(query) ||
    loc.description?.toLowerCase().includes(query) ||
    loc.address?.toLowerCase().includes(query) ||
    loc.city?.toLowerCase().includes(query) ||
    loc.postalCode?.toLowerCase().includes(query) ||
    loc.country?.toLowerCase().includes(query)
  )
})

// Load locations
async function loadLocations() {
  loading.value = true
  try {
    const response = await getShoppingLocations({ returnAll: true })
    locations.value = response.data?.items || []
  } catch (error) {
    console.error('Failed to load shopping locations:', error)
  } finally {
    loading.value = false
  }
}

function handleLocationClick(location: ShoppingLocationInfo) {
  // Location actions are handled by the card's menu
}

// Create modal functions
const openCreateModal = () => {
  createForm.value = {
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
  }
  isCreateModalOpen.value = true
}

const closeCreateModal = () => {
  isCreateModalOpen.value = false
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

const handleCreate = async () => {
  if (!createForm.value.name.trim()) {
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.nameRequired'),
      color: 'error'
    })
    return
  }

  // Validate URLs
  if (createForm.value.website && !isValidUrl(createForm.value.website)) {
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.invalidWebsite'),
      color: 'error'
    })
    return
  }

  if (createForm.value.googleMaps && !isValidUrl(createForm.value.googleMaps)) {
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.invalidGoogleMaps'),
      color: 'error'
    })
    return
  }

  isCreating.value = true
  try {
    await createShoppingLocation({
      name: createForm.value.name,
      description: createForm.value.description || undefined,
      color: createForm.value.color || undefined,
      address: createForm.value.address || undefined,
      city: createForm.value.city || undefined,
      postalCode: createForm.value.postalCode || undefined,
      country: createForm.value.country || undefined,
      website: createForm.value.website || undefined,
      googleMaps: createForm.value.googleMaps || undefined,
      isSharedWithFamily: createForm.value.isSharedWithFamily
    })

    closeCreateModal()
  } catch (error) {
    console.error('Failed to create shopping location:', error)
    toast.add({
      title: t('common.error'),
      description: t('profile.shoppingLocations.createFailed'),
      color: 'error'
    })
  } finally {
    await loadLocations()
    isCreating.value = false
  }
}

function handleAddLocation() {
  openCreateModal()
}

onMounted(() => {
  loadLocations()
})
</script>
