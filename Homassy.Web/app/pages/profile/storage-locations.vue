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

    <!-- Loading State — first load only. A pull-to-refresh keeps the grid
         mounted (PullToRefreshIndicator gives the feedback); swapping it out
         would remount every card and replay the bubble animation. -->
    <template v-if="loading && !hasLoaded">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <SkeletonCard v-for="i in 6" :key="i" />
      </div>
    </template>

    <template v-else>
      <!-- Empty State — rendered next to the (then empty) grid, never in place
           of it: unmounting the grid replays the enter animation on the way back
           and swallows the leave animation of the last card removed. -->
      <EmptyState
        v-if="filteredLocations.length === 0"
        :illustration="hasActiveQuery ? 'search' : 'storageLocation'"
        :title="hasActiveQuery ? $t('profile.storageLocations.noResults') : $t('profile.storageLocations.noLocations')"
        :description="hasActiveQuery ? $t('profile.storageLocations.tryDifferentSearch') : $t('profile.storageLocations.addFirstLocation')"
        :action-label="hasActiveQuery ? $t('common.filters.clear') : $t('profile.storageLocations.addLocation')"
        :action-icon="hasActiveQuery ? 'i-lucide-filter-x' : 'i-lucide-plus'"
        @action="hasActiveQuery ? clearAllFilters() : openCreateDrawer()"
      />

      <!-- Locations Grid. The wrapper exists so the drag composable has a plain element to scan for
           `data-reorder-key` rows — AnimatedList's own root has to keep the grid classes, since
           `grid` only applies to direct children. -->
      <div ref="gridEl">
        <AnimatedList class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <DataStorageLocationCard
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
  <StorageLocationFormDrawer
    :open="drawerOpen"
    :location="editingLocation"
    @update:open="(v) => drawerOpen = v"
    @saved="onSaved"
  />

  <!-- Tap a storage location → overview (info + current stock) -->
  <StorageLocationOverviewDrawer v-model:open="isOverviewOpen" :location="overviewLocation" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useLocationsApi } from '~/composables/api/useLocationsApi'
import type { StorageLocationInfo } from '~/types/location'
import type { MasterDataDeletedEvent, MasterDataReorderedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getStorageLocations, reorderStorageLocations } = useLocationsApi()
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

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(loadStorageLocations)

const loading = ref(true)
// Distinguishes the first load (skeletons) from a refetch (keep the grid mounted
// so the bubble animation is not replayed for every card).
const hasLoaded = ref(false)
const locations = ref<StorageLocationInfo[]>([])
const searchQuery = ref('')

// Filter state
const freezerFilter = ref('all')
const sharedFilter = ref('all')

// Create / edit drawer state
const drawerOpen = ref(false)
const editingLocation = ref<StorageLocationInfo | null>(null)

// Overview drawer state (info + current stock) — opened on card tap
const isOverviewOpen = ref(false)
const overviewLocation = ref<StorageLocationInfo | null>(null)
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

// Manual ordering (#113). The order is the list's own — there is no alternative sort to switch
// between here, so the handles are always shown and `sortOrder` is always what decides the order,
// with the alphabetical order it used to have as the tie-break for rows nobody has dragged.
const gridEl = ref<HTMLElement | null>(null)
const reorder = useReorderableList<StorageLocationInfo>({
  items: locations,
  container: gridEl,
  baseSort: (a, b) => a.name.localeCompare(b.name),
  filter: matchesFilters,
  commit: orderedIds => reorderStorageLocations({ locationPublicIds: orderedIds }),
  onFailed: (error) => {
    console.error('Failed to reorder storage locations:', error)
    toast.add({ title: t('common.error'), description: t('common.reorder.failed'), color: 'error' })
  }
})

// Search + filter predicate, shared by the rendered list and by the reorder composable (which must
// only ever send the rows the user can actually see).
function matchesFilters(loc: StorageLocationInfo): boolean {
  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase()
    const matches = loc.name.toLowerCase().includes(query)
      || loc.description?.toLowerCase().includes(query)
    if (!matches) return false
  }

  if (freezerFilter.value === 'freezer' && !loc.isFreezer) return false
  if (freezerFilter.value === 'notFreezer' && loc.isFreezer) return false

  if (sharedFilter.value === 'shared' && !loc.isSharedWithFamily) return false
  if (sharedFilter.value === 'personal' && loc.isSharedWithFamily) return false

  return true
}

/** What the grid renders: the filter applied, in manual order (see `useReorderableList`). */
const filteredLocations = computed(() => reorder.orderedItems.value)

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
    hasLoaded.value = true
  }
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

// Another member dragged something: patch the positions that moved and let the order recompute.
function handleReordered(payload: MasterDataReorderedEvent) {
  reorder.applyReorderedEntries(payload.entries)
}

onMounted(async () => {
  await loadStorageLocations()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('StorageLocationUpserted', handleUpserted)
  masterDataSocket.on('StorageLocationDeleted', handleDeleted)
  masterDataSocket.on('StorageLocationsReordered', handleReordered)
  masterDataSocket.onReconnected(loadStorageLocations)
})

onBeforeUnmount(() => {
  masterDataSocket.off('StorageLocationUpserted', handleUpserted)
  masterDataSocket.off('StorageLocationDeleted', handleDeleted)
  masterDataSocket.off('StorageLocationsReordered', handleReordered)
  masterDataSocket.offReconnected(loadStorageLocations)
})
</script>
