<template>
  <div>
    <!-- Fixed Header with search + filters -->
    <ListFilterBar
      v-model:search="searchQuery"
      :title="$t('profile.storageLocations.title')"
      icon="i-lucide-warehouse"
      back-to="/profile/data"
      :search-placeholder="$t('profile.storageLocations.searchPlaceholder')"
      :active-filters="activeFilters"
      :filter-count="activeFilterCount"
      :result-count="filteredLocations.length"
      @clear-all="clearAllFilters"
    >
      <template #filters>
        <FilterChipGroup
          v-model="freezerFilter"
          :label="$t('profile.storageLocations.filterLabels.type')"
          :options="freezerOptions"
        />
        <FilterChipGroup
          v-model="sharedFilter"
          :label="$t('profile.storageLocations.filterLabels.shared')"
          :options="sharedOptions"
        />
      </template>
    </ListFilterBar>

    <!-- Content Section -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6">

    <PullToRefreshIndicator
      :pull-distance="pullDistance"
      :is-pulling="isPulling"
      :is-refreshing="isRefreshing"
      :is-ready="isReady"
    />

    <!-- Loading State -->
    <template v-if="loading">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <USkeleton v-for="i in 6" :key="i" class="h-32 w-full rounded-lg" />
      </div>
    </template>

    <!-- Empty State -->
    <div v-else-if="filteredLocations.length === 0" class="rounded-lg p-12 text-center">
      <UIcon name="i-lucide-warehouse" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
      <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
        {{ hasActiveQuery ? $t('profile.storageLocations.noResults') : $t('profile.storageLocations.noLocations') }}
      </p>
      <p class="text-gray-600 dark:text-gray-400">
        {{ hasActiveQuery ? $t('profile.storageLocations.tryDifferentSearch') : $t('profile.storageLocations.addFirstLocation') }}
      </p>
    </div>

    <!-- Locations Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <StorageLocationCard
        v-for="location in filteredLocations"
        :key="location.publicId"
        :location="location"
        :search-query="searchQuery"
        @click="handleLocationClick(location)"
        @edit="openEditDrawer"
        @deleted="onDeleted"
      />
    </div>
    </div>

  <!-- Create / edit bottom sheet -->
  <StorageLocationFormDrawer
    :open="drawerOpen"
    :location="editingLocation"
    @update:open="(v) => drawerOpen = v"
    @saved="onSaved"
  />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useLocationsApi } from '~/composables/api/useLocationsApi'
import type { StorageLocationInfo } from '~/types/location'
import type { MasterDataDeletedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getStorageLocations } = useLocationsApi()
const masterDataSocket = useMasterDataSocket()
const { t } = useI18n()

// Add-action lives on the dynamic nav FAB instead of an inline header button.
useFabActions(() => [
  {
    label: t('common.add'),
    icon: 'i-lucide-plus',
    handler: () => openCreateDrawer()
  }
])

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(loadStorageLocations)

const loading = ref(true)
const locations = ref<StorageLocationInfo[]>([])
const searchQuery = ref('')

// Filter state
const freezerFilter = ref('all')
const sharedFilter = ref('all')

// Create / edit drawer state
const drawerOpen = ref(false)
const editingLocation = ref<StorageLocationInfo | null>(null)

// Filter options
const freezerOptions = computed(() => [
  { label: t('common.filters.all'), value: 'all' },
  { label: t('profile.storageLocations.freezer'), value: 'freezer' },
  { label: t('profile.storageLocations.filters.notFreezer'), value: 'notFreezer' }
])

const sharedOptions = computed(() => [
  { label: t('common.filters.all'), value: 'all' },
  { label: t('common.family'), value: 'shared' },
  { label: t('common.personal'), value: 'personal' }
])

// Filtered locations based on search + filters
const filteredLocations = computed(() => {
  let result = locations.value

  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(loc =>
      loc.name.toLowerCase().includes(query)
      || loc.description?.toLowerCase().includes(query)
    )
  }

  if (freezerFilter.value === 'freezer') {
    result = result.filter(loc => loc.isFreezer)
  } else if (freezerFilter.value === 'notFreezer') {
    result = result.filter(loc => !loc.isFreezer)
  }

  if (sharedFilter.value === 'shared') {
    result = result.filter(loc => loc.isSharedWithFamily)
  } else if (sharedFilter.value === 'personal') {
    result = result.filter(loc => !loc.isSharedWithFamily)
  }

  return result
})

// Active filter chips
const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (freezerFilter.value !== 'all') {
    const opt = freezerOptions.value.find(o => o.value === freezerFilter.value)
    chips.push({ key: 'freezer', label: opt?.label ?? '', clear: () => { freezerFilter.value = 'all' } })
  }
  if (sharedFilter.value !== 'all') {
    const opt = sharedOptions.value.find(o => o.value === sharedFilter.value)
    chips.push({ key: 'shared', label: opt?.label ?? '', clear: () => { sharedFilter.value = 'all' } })
  }
  return chips
})

const activeFilterCount = computed(() => activeFilters.value.length)
const hasActiveQuery = computed(() => !!searchQuery.value.trim() || activeFilterCount.value > 0)

function clearAllFilters() {
  freezerFilter.value = 'all'
  sharedFilter.value = 'all'
}

// Load locations
async function loadStorageLocations() {
  loading.value = true
  try {
    const response = await getStorageLocations({ returnAll: true })
    locations.value = response.data?.items || []
  } catch (error) {
    console.error('Failed to load storage locations:', error)
  } finally {
    loading.value = false
  }
}

function handleLocationClick(_location: StorageLocationInfo) {
  // Location actions are handled by the card's menu
}

// Create / edit drawer functions
function openCreateDrawer() {
  editingLocation.value = null
  drawerOpen.value = true
}

function openEditDrawer(location: StorageLocationInfo) {
  editingLocation.value = location
  drawerOpen.value = true
}

// Idempotent local patch (upsert / delete) so the acting client updates instantly; the realtime
// socket delivers the same change to other family members.
function upsertLocation(location: StorageLocationInfo) {
  const idx = locations.value.findIndex(l => l.publicId === location.publicId)
  if (idx >= 0) locations.value[idx] = location
  else locations.value.push(location)
}

function removeLocation(publicId: string) {
  locations.value = locations.value.filter(l => l.publicId !== publicId)
}

function onSaved(location: StorageLocationInfo) {
  upsertLocation(location)
}

function onDeleted(publicId: string) {
  removeLocation(publicId)
}

// Realtime handlers
function handleUpserted(dto: StorageLocationInfo) {
  upsertLocation(dto)
}

function handleDeleted(payload: MasterDataDeletedEvent) {
  removeLocation(payload.publicId)
}

onMounted(async () => {
  await loadStorageLocations()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('StorageLocationUpserted', handleUpserted)
  masterDataSocket.on('StorageLocationDeleted', handleDeleted)
  masterDataSocket.onReconnected(loadStorageLocations)
})

onBeforeUnmount(() => {
  masterDataSocket.off('StorageLocationUpserted', handleUpserted)
  masterDataSocket.off('StorageLocationDeleted', handleDeleted)
  masterDataSocket.offReconnected(loadStorageLocations)
})
</script>
