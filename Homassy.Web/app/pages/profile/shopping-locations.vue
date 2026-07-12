<template>
  <div>
    <!-- Fixed Header with search + filters -->
    <ListFilterBar
      v-model:search="searchQuery"
      :title="$t('profile.shoppingLocations.title')"
      icon="i-lucide-shopping-cart"
      back-to="/profile/data"
      :search-placeholder="$t('profile.shoppingLocations.searchPlaceholder')"
      :active-filters="activeFilters"
      :filter-count="activeFilterCount"
      :result-count="filteredLocations.length"
      @clear-all="clearAllFilters"
    >
      <template #filters>
        <FilterChipGroup
          v-model="sharedFilter"
          :label="$t('profile.shoppingLocations.filterLabels.shared')"
          :options="sharedOptions"
        />
        <FilterChipGroup
          v-if="cityOptions.length > 1"
          v-model="cityFilter"
          :label="$t('common.city')"
          :options="cityOptions"
        />
        <FilterChipGroup
          v-if="countryOptions.length > 1"
          v-model="countryFilter"
          :label="$t('common.country')"
          :options="countryOptions"
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
      <UIcon name="i-lucide-shopping-cart" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
      <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
        {{ hasActiveQuery ? $t('profile.shoppingLocations.noResults') : $t('profile.shoppingLocations.noLocations') }}
      </p>
      <p class="text-gray-600 dark:text-gray-400">
        {{ hasActiveQuery ? $t('profile.shoppingLocations.tryDifferentSearch') : $t('profile.shoppingLocations.addFirstLocation') }}
      </p>
    </div>

    <!-- Locations Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <DataShoppingLocationCard
        v-for="location in filteredLocations"
        :key="location.publicId"
        :location="location"
        :search-query="searchQuery"
        @select="openOverview"
        @edit="openEditDrawer"
        @deleted="onDeleted"
      />
    </div>
    </div>

  <!-- Create / edit bottom sheet -->
  <ShoppingLocationFormDrawer
    :open="drawerOpen"
    :location="editingLocation"
    @update:open="(v) => drawerOpen = v"
    @saved="onSaved"
  />

  <!-- Tap a shopping location → overview (info + purchase history) -->
  <ShoppingLocationOverviewDrawer v-model:open="isOverviewOpen" :location="overviewLocation" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useLocationsApi } from '~/composables/api/useLocationsApi'
import type { ShoppingLocationInfo } from '~/types/location'
import type { MasterDataDeletedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getShoppingLocations } = useLocationsApi()
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

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(loadLocations)

const loading = ref(true)
const locations = ref<ShoppingLocationInfo[]>([])
const searchQuery = ref('')

// Filter state
const sharedFilter = ref('all')
const cityFilter = ref('all')
const countryFilter = ref('all')

// Create / edit drawer state
const drawerOpen = ref(false)
const editingLocation = ref<ShoppingLocationInfo | null>(null)

// Overview drawer state (info + purchase history) — opened on card tap
const isOverviewOpen = ref(false)
const overviewLocation = ref<ShoppingLocationInfo | null>(null)
function openOverview(publicId: string) {
  overviewLocation.value = locations.value.find(l => l.publicId === publicId) ?? null
  isOverviewOpen.value = true
}

// Filter options
const sharedOptions = computed(() => [
  { label: t('common.filters.all'), value: 'all' },
  { label: t('common.family'), value: 'shared' },
  { label: t('common.personal'), value: 'personal' }
])

// City / country options built from values actually present on the locations
const cityOptions = computed(() => {
  const seen = new Set<string>()
  for (const loc of locations.value) {
    if (loc.city?.trim()) seen.add(loc.city.trim())
  }
  const options = [...seen]
    .map(value => ({ label: value, value }))
    .sort((a, b) => a.label.localeCompare(b.label))
  return [{ label: t('common.filters.all'), value: 'all' }, ...options]
})

const countryOptions = computed(() => {
  const seen = new Set<string>()
  for (const loc of locations.value) {
    if (loc.country?.trim()) seen.add(loc.country.trim())
  }
  const options = [...seen]
    .map(value => ({ label: value, value }))
    .sort((a, b) => a.label.localeCompare(b.label))
  return [{ label: t('common.filters.all'), value: 'all' }, ...options]
})

// Filtered locations based on search + filters
const filteredLocations = computed(() => {
  let result = locations.value

  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(loc =>
      loc.name.toLowerCase().includes(query)
      || loc.description?.toLowerCase().includes(query)
      || loc.address?.toLowerCase().includes(query)
      || loc.city?.toLowerCase().includes(query)
      || loc.postalCode?.toLowerCase().includes(query)
      || loc.country?.toLowerCase().includes(query)
    )
  }

  if (sharedFilter.value === 'shared') {
    result = result.filter(loc => loc.isSharedWithFamily)
  } else if (sharedFilter.value === 'personal') {
    result = result.filter(loc => !loc.isSharedWithFamily)
  }

  if (cityFilter.value !== 'all') {
    result = result.filter(loc => loc.city?.trim() === cityFilter.value)
  }

  if (countryFilter.value !== 'all') {
    result = result.filter(loc => loc.country?.trim() === countryFilter.value)
  }

  return result
})

// Active filter chips
const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (sharedFilter.value !== 'all') {
    const opt = sharedOptions.value.find(o => o.value === sharedFilter.value)
    chips.push({ key: 'shared', label: opt?.label ?? '', clear: () => { sharedFilter.value = 'all' } })
  }
  if (cityFilter.value !== 'all') {
    chips.push({ key: 'city', label: cityFilter.value, clear: () => { cityFilter.value = 'all' } })
  }
  if (countryFilter.value !== 'all') {
    chips.push({ key: 'country', label: countryFilter.value, clear: () => { countryFilter.value = 'all' } })
  }
  return chips
})

const activeFilterCount = computed(() => activeFilters.value.length)
const hasActiveQuery = computed(() => !!searchQuery.value.trim() || activeFilterCount.value > 0)

function clearAllFilters() {
  sharedFilter.value = 'all'
  cityFilter.value = 'all'
  countryFilter.value = 'all'
}

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

// Create / edit drawer functions
function openCreateDrawer() {
  editingLocation.value = null
  drawerOpen.value = true
}

function openEditDrawer(location: ShoppingLocationInfo) {
  editingLocation.value = location
  drawerOpen.value = true
}

// Idempotent local patch (upsert / delete) for instant feedback; the realtime socket delivers the
// same change to other family members.
function upsertLocation(location: ShoppingLocationInfo) {
  const idx = locations.value.findIndex(l => l.publicId === location.publicId)
  if (idx >= 0) locations.value[idx] = location
  else locations.value.push(location)
}

function removeLocation(publicId: string) {
  locations.value = locations.value.filter(l => l.publicId !== publicId)
}

function onSaved(location: ShoppingLocationInfo) {
  upsertLocation(location)
}

function onDeleted(publicId: string) {
  removeLocation(publicId)
}

function handleUpserted(dto: ShoppingLocationInfo) {
  upsertLocation(dto)
}

function handleDeleted(payload: MasterDataDeletedEvent) {
  removeLocation(payload.publicId)
}

onMounted(async () => {
  await loadLocations()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('ShoppingLocationUpserted', handleUpserted)
  masterDataSocket.on('ShoppingLocationDeleted', handleDeleted)
  masterDataSocket.onReconnected(loadLocations)
})

onBeforeUnmount(() => {
  masterDataSocket.off('ShoppingLocationUpserted', handleUpserted)
  masterDataSocket.off('ShoppingLocationDeleted', handleDeleted)
  masterDataSocket.offReconnected(loadLocations)
})
</script>
