<template>
  <div>
    <!-- Fixed Header with search + filters -->
    <ListFilterBar
      v-model:search="searchQuery"
      :title="$t('profile.shoppingLists.title')"
      icon="i-lucide-list-checks"
      back-to="/profile/data"
      :search-placeholder="$t('profile.shoppingLists.searchPlaceholder')"
      :active-filters="activeFilters"
      :filter-count="activeFilterCount"
      :result-count="filteredLists.length"
      @clear-all="clearAllFilters"
    >
      <template #filters>
        <FilterChipGroup
          v-model="sharedFilter"
          :label="$t('profile.shoppingLists.filterLabels.shared')"
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
        <USkeleton v-for="i in 6" :key="i" class="h-32 w-full rounded-lg" />
      </div>
    </template>

    <template v-else>
      <!-- Empty State — rendered next to the (then empty) grid, never in place
           of it: unmounting the grid replays the enter animation on the way back
           and swallows the leave animation of the last card removed. -->
      <div v-if="filteredLists.length === 0" class="rounded-lg p-12 text-center">
        <UIcon name="i-lucide-list-checks" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
        <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
          {{ hasActiveQuery ? $t('profile.shoppingLists.noResults') : $t('profile.shoppingLists.noLists') }}
        </p>
        <p class="text-gray-600 dark:text-gray-400">
          {{ hasActiveQuery ? $t('profile.shoppingLists.tryDifferentSearch') : $t('profile.shoppingLists.addFirstList') }}
        </p>
      </div>

      <!-- Lists Grid -->
      <AnimatedList class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <DataShoppingListCard
          v-for="list in filteredLists"
          :key="list.publicId"
          :list="list"
          :search-query="searchQuery"
          @select="openOverview"
          @edit="openEditDrawer"
          @deleted="onDeleted"
        />
      </AnimatedList>
    </template>
    </div>

  <!-- Create / edit bottom sheet (shared with /shopping-lists) -->
  <ShoppingListFormDrawer
    :open="drawerOpen"
    :list="editingList"
    @update:open="(v) => drawerOpen = v"
    @saved="onSaved"
  />

  <!-- Tap a shopping list → overview (info + items still to buy) -->
  <ShoppingListOverviewDrawer v-model:open="isOverviewOpen" :list="overviewList" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useShoppingListApi } from '~/composables/api/useShoppingListApi'
import type { ShoppingListInfo } from '~/types/shoppingList'
import type { MasterDataDeletedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getShoppingLists } = useShoppingListApi()
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

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(loadLists)

const loading = ref(true)
// Distinguishes the first load (skeletons) from a refetch (keep the grid mounted
// so the bubble animation is not replayed for every card).
const hasLoaded = ref(false)
const lists = ref<ShoppingListInfo[]>([])
const searchQuery = ref('')

// Filter state
const sharedFilter = ref('all')

// Create / edit drawer state
const drawerOpen = ref(false)
const editingList = ref<ShoppingListInfo | null>(null)

// Overview drawer state (info + items still to buy) — opened on card tap
const isOverviewOpen = ref(false)
const overviewList = ref<ShoppingListInfo | null>(null)
function openOverview(publicId: string) {
  overviewList.value = lists.value.find(l => l.publicId === publicId) ?? null
  isOverviewOpen.value = true
}

// Filter options
const sharedOptions = computed(() => [
  { label: t('common.filters.all'), value: 'all' },
  { label: t('common.family'), value: 'shared' },
  { label: t('common.personal'), value: 'personal' }
])

// Filtered lists based on search + filters
const filteredLists = computed(() => {
  let result = lists.value

  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(list =>
      list.name.toLowerCase().includes(query)
      || list.description?.toLowerCase().includes(query)
    )
  }

  if (sharedFilter.value === 'shared') {
    result = result.filter(list => list.isSharedWithFamily)
  } else if (sharedFilter.value === 'personal') {
    result = result.filter(list => !list.isSharedWithFamily)
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
  return chips
})

const activeFilterCount = computed(() => activeFilters.value.length)
const hasActiveQuery = computed(() => !!searchQuery.value.trim() || activeFilterCount.value > 0)

function clearAllFilters() {
  sharedFilter.value = 'all'
}

// Load lists
async function loadLists() {
  loading.value = true
  try {
    const response = await getShoppingLists({ returnAll: true })
    lists.value = response.data?.items || []
  } catch (error) {
    console.error('Failed to load shopping lists:', error)
  } finally {
    loading.value = false
    hasLoaded.value = true
  }
}

// Create / edit drawer functions
function openCreateDrawer() {
  editingList.value = null
  drawerOpen.value = true
}

function openEditDrawer(list: ShoppingListInfo) {
  editingList.value = list
  drawerOpen.value = true
}

// Idempotent local patch (upsert / delete) for instant feedback; the realtime socket delivers the
// same change to other family members.
function upsertList(list: ShoppingListInfo) {
  const idx = lists.value.findIndex(l => l.publicId === list.publicId)
  if (idx >= 0) lists.value[idx] = list
  else lists.value.push(list)
}

function removeList(publicId: string) {
  lists.value = lists.value.filter(l => l.publicId !== publicId)
}

function onSaved(list: ShoppingListInfo) {
  upsertList(list)
}

function onDeleted(publicId: string) {
  removeList(publicId)
}

function handleUpserted(dto: ShoppingListInfo) {
  upsertList(dto)
}

function handleDeleted(payload: MasterDataDeletedEvent) {
  removeList(payload.publicId)
}

onMounted(async () => {
  await loadLists()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('ShoppingListUpserted', handleUpserted)
  masterDataSocket.on('ShoppingListDeleted', handleDeleted)
  masterDataSocket.onReconnected(loadLists)
})

onBeforeUnmount(() => {
  masterDataSocket.off('ShoppingListUpserted', handleUpserted)
  masterDataSocket.off('ShoppingListDeleted', handleDeleted)
  masterDataSocket.offReconnected(loadLists)
})
</script>
