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

    <!-- Loading State — first load only. A pull-to-refresh keeps the grid
         mounted (PullToRefreshIndicator gives the feedback); swapping it out
         would remount every card and replay the bubble animation. -->
    <template v-if="loading && !hasLoaded">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <SkeletonCard v-for="i in 6" :key="i" />
      </div>
    </template>

    <!-- All the shops at once (#107). Only offered when at least one of them has coordinates —
         a map of nothing is worse than no map. -->
    <div v-if="mapMarkers.length && (!loading || hasLoaded)" class="mb-4">
      <div class="mb-2 flex items-center justify-between">
        <UButton
          :icon="isMapOpen ? 'i-lucide-chevron-up' : 'i-lucide-map'"
          :label="isMapOpen ? $t('map.hide') : $t('map.showAll', { count: mapMarkers.length })"
          color="neutral"
          variant="ghost"
          size="sm"
          :aria-expanded="isMapOpen"
          @click="toggleMap"
        />
      </div>
      <InteractiveMap
        v-if="isMapOpen"
        :markers="mapMarkers"
        :user-position="myPosition"
        height="16rem"
        cluster
        @marker-click="openOverview"
      />
    </div>

    <template v-if="!loading || hasLoaded">
      <!-- Empty State — rendered next to the (then empty) grid, never in place
           of it: unmounting the grid replays the enter animation on the way back
           and swallows the leave animation of the last card removed. -->
      <EmptyState
        v-if="filteredLocations.length === 0"
        :illustration="hasActiveQuery ? 'search' : 'shoppingLocation'"
        :title="hasActiveQuery ? $t('profile.shoppingLocations.noResults') : $t('profile.shoppingLocations.noLocations')"
        :description="hasActiveQuery ? $t('profile.shoppingLocations.tryDifferentSearch') : $t('profile.shoppingLocations.addFirstLocation')"
        :action-label="hasActiveQuery ? $t('common.filters.clear') : $t('profile.shoppingLocations.createLocation')"
        :action-icon="hasActiveQuery ? 'i-lucide-filter-x' : 'i-lucide-plus'"
        @action="hasActiveQuery ? clearAllFilters() : openCreateDrawer()"
      />

      <!-- Locations Grid. The wrapper exists so the drag composable has a plain element to scan for
           `data-reorder-key` rows — AnimatedList's own root has to keep the grid classes. -->
      <div ref="gridEl">
        <AnimatedList class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <DataShoppingLocationCard
            v-for="location in filteredLocations"
            :key="location.publicId"
            :data-reorder-key="location.publicId"
            :location="location"
            :search-query="searchQuery"
            reorderable
            :dragging="reorder.draggingKey.value === location.publicId"
            @select="openOverview"
            @edit="openEditDrawer"
            @deleted="onDeleted"
            @reorder-lift="(event) => reorder.startDrag(event, location.publicId)"
            @reorder-move="(direction) => reorder.moveByKeyboard(location.publicId, direction)"
          />
        </AnimatedList>
      </div>
    </template>
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
import type { MasterDataDeletedEvent, MasterDataReorderedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getShoppingLocations, reorderShoppingLocations } = useLocationsApi()
const masterDataSocket = useMasterDataSocket()
const { t } = useI18n()
const toast = useToast()

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
// Distinguishes the first load (skeletons) from a refetch (keep the grid mounted
// so the bubble animation is not replayed for every card).
const hasLoaded = ref(false)
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

// Arrivals from the command palette (#111). The selection waits for `hasLoaded` because
// `openOverview` resolves the location out of the loaded list — on an empty list it would open
// an empty drawer.
useSearchHandoff({
  search: (term) => { searchQuery.value = term },
  select: openOverview,
  ready: () => hasLoaded.value
})

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

// Search + filter predicate, shared by the rendered list and by the reorder composable (which must
// only ever send the rows the user can actually see).
function matchesFilters(loc: ShoppingLocationInfo): boolean {
  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase()
    const matches = loc.name.toLowerCase().includes(query)
      || loc.description?.toLowerCase().includes(query)
      || loc.address?.toLowerCase().includes(query)
      || loc.city?.toLowerCase().includes(query)
      || loc.postalCode?.toLowerCase().includes(query)
      || loc.country?.toLowerCase().includes(query)
    if (!matches) return false
  }

  if (sharedFilter.value === 'shared' && !loc.isSharedWithFamily) return false
  if (sharedFilter.value === 'personal' && loc.isSharedWithFamily) return false

  if (cityFilter.value !== 'all' && loc.city?.trim() !== cityFilter.value) return false
  if (countryFilter.value !== 'all' && loc.country?.trim() !== countryFilter.value) return false

  return true
}

// Manual ordering (#113). There is no alternative sort to switch between here, so the handles are
// always shown and `sortOrder` decides the order, with the alphabetical order the list used to have
// as the tie-break for rows nobody has dragged.
const gridEl = ref<HTMLElement | null>(null)
const reorder = useReorderableList<ShoppingLocationInfo>({
  items: locations,
  container: gridEl,
  baseSort: (a, b) => a.name.localeCompare(b.name),
  filter: matchesFilters,
  commit: orderedIds => reorderShoppingLocations({ locationPublicIds: orderedIds }),
  onFailed: (error) => {
    console.error('Failed to reorder shopping locations:', error)
    toast.add({ title: t('common.error'), description: t('common.reorder.failed'), color: 'error' })
  }
})

/** What the grid renders: the filter applied, in manual order (see `useReorderableList`). */
const filteredLocations = computed(() => reorder.orderedItems.value)

// --- The multi-location map (#107) -----------------------------------------
// An overview of every shop at once, which the old per-location iframe could not express at all.
// Built from stored coordinates only: geocoding a whole page of addresses at load would mean one
// Nominatim request per location, and a location saved today already has its coordinates.

const MAP_OPEN_KEY = 'shoppingLocationsMapOpen'

const isMapOpen = ref(false)
const myPosition = ref<{ lat: number, lon: number } | null>(null)

const { isSupported: isGeoSupported, getPermissionStatus, getCurrentPosition } = useGeolocation()

const mappableLocations = computed(() => filteredLocations.value.filter(
  (loc): loc is ShoppingLocationInfo & { latitude: number, longitude: number } =>
    typeof loc.latitude === 'number' && typeof loc.longitude === 'number'
))

/**
 * The shop closest to the user, which gets the pulse ring. Only meaningful once a position is
 * known — without one nothing is "currently relevant" and no marker is singled out.
 */
const nearestLocationId = computed(() => {
  const position = myPosition.value
  if (!position) return null

  let nearestId: string | null = null
  let nearestDistance = Number.POSITIVE_INFINITY
  for (const loc of mappableLocations.value) {
    const distance = distanceMeters(position.lat, position.lon, loc.latitude, loc.longitude)
    if (distance < nearestDistance) {
      nearestDistance = distance
      nearestId = loc.publicId
    }
  }
  return nearestId
})

const mapMarkers = computed(() => mappableLocations.value.map(loc => ({
  id: loc.publicId,
  lat: loc.latitude,
  lon: loc.longitude,
  label: loc.name,
  color: loc.color || undefined,
  highlighted: loc.publicId === nearestLocationId.value
})))

function toggleMap() {
  isMapOpen.value = !isMapOpen.value
  localStorage.setItem(MAP_OPEN_KEY, String(isMapOpen.value))
}

/**
 * Reads the device position only when the browser says permission is already granted. The map is
 * not a reason to put a permission prompt in front of someone who opened a list of shops — it just
 * shows their dot if the app already has the right to know where they are.
 */
async function maybeLocate() {
  if (!isGeoSupported.value) return
  if (await getPermissionStatus() !== 'granted') return
  try {
    const position = await getCurrentPosition()
    myPosition.value = { lat: position.lat, lon: position.lon }
  } catch {
    // Timed out or unavailable — the map simply shows no "you are here" dot.
  }
}

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
    hasLoaded.value = true
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

// Another member dragged something: patch the positions that moved and let the order recompute.
function handleReordered(payload: MasterDataReorderedEvent) {
  reorder.applyReorderedEntries(payload.entries)
}

onMounted(async () => {
  isMapOpen.value = localStorage.getItem(MAP_OPEN_KEY) === 'true'
  void maybeLocate()

  await loadLocations()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('ShoppingLocationUpserted', handleUpserted)
  masterDataSocket.on('ShoppingLocationDeleted', handleDeleted)
  masterDataSocket.on('ShoppingLocationsReordered', handleReordered)
  masterDataSocket.onReconnected(loadLocations)
})

onBeforeUnmount(() => {
  masterDataSocket.off('ShoppingLocationUpserted', handleUpserted)
  masterDataSocket.off('ShoppingLocationDeleted', handleDeleted)
  masterDataSocket.off('ShoppingLocationsReordered', handleReordered)
  masterDataSocket.offReconnected(loadLocations)
})
</script>
