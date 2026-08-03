<template>
  <div>
    <!-- Search + filters bar, teleported into the persistent AppHeader (identity +
         active-list subtitle live there too; the header skeletons this slot). -->
    <Teleport to="#app-header-search">
      <div class="space-y-3">
        <!-- Search row + filters trigger -->
        <div class="flex gap-2">
          <UFieldGroup size="md" orientation="horizontal" class="flex-1">
            <UInput
              v-model="searchQuery"
              :disabled="!isSearchEnabled"
              :placeholder="$t('pages.shoppingLists.searchPlaceholder')"
              class="flex-1"
            >
              <template #trailing>
                <UButton
                  v-if="searchQuery"
                  icon="i-lucide-x"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :disabled="!isSearchEnabled"
                  @click="searchQuery = ''"
                />
              </template>
            </UInput>
            <BarcodeScannerButton
              v-if="showCameraButton"
              :disabled="!isSearchEnabled"
              @scanned="handleBarcodeScanned"
            />
          </UFieldGroup>
          <UButton
            v-if="listLocations.length > 0"
            :icon="locationTracking ? 'i-lucide-navigation' : 'i-lucide-navigation-off'"
            :color="locationTracking ? 'primary' : 'neutral'"
            :variant="locationTracking ? 'solid' : 'outline'"
            size="md"
            :loading="isLocating"
            :aria-label="$t('pages.shoppingLists.nearby.toggle')"
            :aria-pressed="locationTracking"
            @click="toggleLocation"
          />
          <UChip :show="activeFilterCount > 0" :text="activeFilterCount" color="primary" size="2xl">
            <UButton
              icon="i-lucide-sliders-horizontal"
              color="primary"
              size="md"
              :aria-label="$t('pages.shoppingLists.filters.toggle')"
              :aria-expanded="filtersOpen"
              @click="filtersOpen = true"
            >
              <span class="hidden sm:inline">{{ $t('pages.shoppingLists.filters.toggle') }}</span>
            </UButton>
          </UChip>
        </div>

        <!-- Active filter chips (dismissible) -->
        <div
          v-if="activeFilters.length"
          class="flex items-center gap-2 overflow-x-auto pb-1 -mx-1 px-1"
        >
          <UButton
            v-for="f in activeFilters"
            :key="f.key"
            :label="f.label"
            size="xs"
            color="primary"
            variant="soft"
            trailing-icon="i-lucide-x"
            class="rounded-full shrink-0"
            :aria-label="`${$t('pages.shoppingLists.filters.removeFilter')}: ${f.label}`"
            @click="f.clear()"
          />
          <UButton
            :label="$t('pages.shoppingLists.filters.clearAll')"
            size="xs"
            color="neutral"
            variant="ghost"
            class="shrink-0"
            @click="clearAllFilters"
          />
        </div>
      </div>
    </Teleport>

    <!-- Content Section -->
    <div class="px-2 sm:px-4 md:px-6 lg:px-8 pb-6">
      <PullToRefreshIndicator
        :pull-distance="pullDistance"
        :is-pulling="isPulling"
        :is-refreshing="isRefreshing"
        :is-ready="isReady"
      />

      <!-- "You are here" banner (foreground proximity) -->
      <div
        v-if="locationTracking && nearbyLocations.length > 0"
        class="mb-4 flex items-center gap-3 px-4 py-3 rounded-2xl bg-blue-50 dark:bg-blue-900/30 border border-blue-300/60 dark:border-blue-600/50"
      >
        <UIcon name="i-lucide-navigation" class="h-5 w-5 text-blue-600 dark:text-blue-400 shrink-0" />
        <p class="text-sm font-medium text-blue-800 dark:text-blue-200">
          {{ $t('pages.shoppingLists.nearby.banner', { count: nearbyPendingTotal, location: nearbyLocationNames }) }}
        </p>
      </div>
      <div
        v-else-if="locationTracking"
        class="mb-4 flex items-center gap-3 px-4 py-3 rounded-2xl bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700"
      >
        <UIcon name="i-lucide-radar" class="h-5 w-5 text-gray-500 dark:text-gray-400 shrink-0" />
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ $t('pages.shoppingLists.nearby.searching') }}
        </p>
      </div>
      <!-- Loading State — only while there is nothing to show yet. A refetch of
           an already-open list (showPurchased toggle, socket reconnect,
           pull-to-refresh) keeps the grid mounted, so the bubble animation is
           not replayed for every card. -->
      <div v-if="isLoadingDetails && !currentListDetails" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
        <USkeleton class="h-48 w-full" />
        <USkeleton class="h-48 w-full" />
        <USkeleton class="h-48 w-full" />
        <USkeleton class="h-48 w-full" />
      </div>

      <!-- Empty: No Lists -->
      <div v-else-if="!isLoadingLists && allShoppingLists.length === 0" class="text-center py-12">
        <UIcon name="i-lucide-shopping-cart" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
        <p class="text-gray-500 dark:text-gray-400">
          {{ $t('pages.shoppingLists.noListsFound') }}
        </p>
      </div>

      <!-- Empty: Lists exist but none selected -->
      <div v-else-if="!isLoadingLists && !selectedListId" class="text-center py-12">
        <UIcon name="i-lucide-list-checks" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
        <p class="text-gray-500 dark:text-gray-400 mb-4">
          {{ $t('pages.shoppingLists.filters.selectListPrompt') }}
        </p>
        <UButton
          icon="i-lucide-sliders-horizontal"
          color="primary"
          :label="$t('pages.shoppingLists.filters.toggle')"
          @click="filtersOpen = true"
        />
      </div>

      <!-- A list is open -->
      <template v-else>
        <!-- Both empty states render next to the (then empty) grid, never in
             place of it: unmounting the grid replays the enter animation on the
             way back and swallows the leave animation of the last item removed. -->
        <!-- Empty: No Items in List -->
        <div v-if="currentListDetails && currentListDetails.items.length === 0" class="text-center py-12">
          <UIcon name="i-lucide-package-open" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
          <p class="text-gray-500 dark:text-gray-400">
            {{ $t('pages.shoppingLists.noItemsInList') }}
          </p>
        </div>

        <!-- Empty: No Search Results -->
        <div v-else-if="filteredItems.length === 0" class="text-center py-12">
          <p class="text-gray-500 dark:text-gray-400">
            {{ $t('pages.shoppingLists.noSearchResults') }}
          </p>
        </div>

        <!-- Items Grid -->
        <AnimatedList class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          <ShoppingListItemCard
            v-for="item in filteredItems"
            :key="item.publicId"
            :item="item"
            :search-query="searchQuery"
            :at-current-location="isItemAtCurrentLocation(item)"
            :similar-type-at-current-location="isItemSimilarTypeHere(item)"
            :shopping-locations="allShoppingLocations"
            :current-store="currentStoreForItem(item)"
            @refresh="handleItemRefresh"
            @deleted="handleItemRefresh"
          />
        </AnimatedList>
      </template>
    </div>

    <!-- Filter drawer (bottom sheet) -->
    <AppDrawer v-model:open="filtersOpen" :title="$t('pages.shoppingLists.filters.toggle')" icon="i-lucide-sliders-horizontal">
        <div class="space-y-5 pb-2">
          <!-- Shopping list selection + management -->
          <div role="group" :aria-label="$t('pages.shoppingLists.filterLabels.list')">
            <div class="flex items-center gap-2 mb-1.5">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.shoppingLists.filterLabels.list') }}
              </p>
              <UDropdownMenu :items="listMenuItems" size="md">
                <UButton
                  icon="i-lucide-ellipsis"
                  size="sm"
                  color="primary"
                  :aria-label="$t('pages.shoppingLists.menu.edit')"
                />
              </UDropdownMenu>
            </div>
            <div v-if="allShoppingLists.length" class="flex gap-2 overflow-x-auto pb-1 -mx-1 px-1">
              <UButton
                v-for="list in allShoppingLists"
                :key="list.publicId"
                :label="list.text"
                size="sm"
                class="rounded-full shrink-0"
                :color="selectedListId === list.publicId ? 'primary' : 'neutral'"
                :variant="selectedListId === list.publicId ? 'solid' : 'outline'"
                :aria-pressed="selectedListId === list.publicId"
                @click="selectedListId = list.publicId"
              />
            </div>
            <p v-else class="text-sm text-gray-500 dark:text-gray-400">
              {{ $t('pages.shoppingLists.noListsFound') }}
            </p>
          </div>

          <!-- Deadline status -->
          <FilterChipGroup
            v-model="deadlineFilter"
            :label="$t('pages.shoppingLists.filterLabels.deadline')"
            :options="deadlineOptions"
          />

          <!-- Boolean property toggles -->
          <div role="group" :aria-label="$t('pages.shoppingLists.filterLabels.properties')">
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
              {{ $t('pages.shoppingLists.filterLabels.properties') }}
            </p>
            <UButton
              :label="$t('pages.shoppingLists.filters.showPurchased')"
              icon="i-lucide-check-check"
              size="sm"
              class="rounded-full"
              :color="showPurchased ? 'primary' : 'neutral'"
              :variant="showPurchased ? 'solid' : 'outline'"
              :aria-pressed="showPurchased"
              @click="showPurchased = !showPurchased"
            />
          </div>

          <!-- Quantity range -->
          <div>
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.shoppingLists.filterLabels.quantity') }}
              </p>
              <span class="text-sm text-gray-500 dark:text-gray-400 tabular-nums">
                {{ quantityRange[0] }} – {{ quantityRange[1] }}
              </span>
            </div>
            <USlider
              v-model="quantityRange"
              :min="0"
              :max="maxItemQuantity"
              :step="1"
              color="primary"
              :disabled="!currentListDetails"
            />
          </div>

          <!-- Shopping location -->
          <FilterChipGroup
            v-if="locationOptions.length > 1"
            v-model="locationFilter"
            :label="$t('pages.shoppingLists.filterLabels.location')"
            :options="locationOptions"
          />
        </div>

      <template #footer>
        <div class="flex items-center gap-2 w-full">
          <UButton
            :label="$t('pages.shoppingLists.filters.clearAll')"
            color="neutral"
            variant="ghost"
            size="lg"
            :disabled="activeFilterCount === 0"
            @click="clearAllFilters"
          />
          <UButton
            class="flex-1"
            size="lg"
            color="primary"
            :label="$t('pages.shoppingLists.filters.showResults', { count: filteredItems.length })"
            @click="filtersOpen = false"
          />
        </div>
      </template>
    </AppDrawer>

    <!-- Create / edit bottom sheet (shared with /profile/shopping-lists) -->
    <ShoppingListFormDrawer
      :open="isListFormOpen"
      :list="editingList"
      @update:open="(val) => isListFormOpen = val"
      @saved="onListSaved"
    />

    <!-- Delete confirmation (shared with /profile/shopping-lists) -->
    <ShoppingListDeleteDrawer
      :open="isDeleteModalOpen"
      :list="currentListDetails"
      :item-count="currentListDetails?.items.length ?? 0"
      @update:open="(val) => isDeleteModalOpen = val"
      @deleted="onListDeleted"
    />

    <!-- Add item wizard (fullscreen modal) -->
    <AddShoppingListItemModal
      v-model:open="isAddItemModalOpen"
      :list-id="selectedListId"
      :mode="addItemMode"
    />

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="handleBarcodeScanned" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import type { SelectValue } from '../../types/selectValue'
import type { DetailedShoppingListInfo, ShoppingListItemInfo, ShoppingListInfo } from '../../types/shoppingList'
import { SelectValueType, StoreType } from '../../types/enums'
import { useSelectValueApi } from '../../composables/api/useSelectValueApi'
import { useShoppingListApi } from '../../composables/api/useShoppingListApi'
import { useLocationsApi } from '../../composables/api/useLocationsApi'
import { normalizeForSearch } from '../../utils/stringUtils'
import { useCameraAvailability } from '../../composables/useCameraAvailability'
import type { GeoPosition } from '../../composables/useGeolocation'
import type { ShoppingLocationInfo } from '../../types/location'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { t: $t } = useI18n()
const { getSelectValues } = useSelectValueApi()
const { getShoppingListDetails } = useShoppingListApi()
const { getShoppingLocations } = useLocationsApi()
const { showCameraButton } = useCameraAvailability()
const { isExpired: checkIsExpired, isExpiringWithinTwoWeeks: checkIsExpiringWithinTwoWeeks } = useExpirationCheck()
const toast = useToast()

// Geolocation-based "you are here" highlighting (foreground-only; see useGeolocation).
const { isSupported: isGeoSupported, getCurrentPosition, startWatch, stopWatch } = useGeolocation()
const { geocode } = useGeocoding()
const { permissionStatus: notificationPermission } = usePushNotifications()

// Realtime channel for the currently-open list (see useShoppingListSocket).
const socket = useShoppingListSocket()
const { emit: emitBusEvent } = useEventBus()

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(async () => {
  await loadShoppingLists()
  if (selectedListId.value) await loadListDetails(selectedListId.value)
})

// LocalStorage key for last selected shopping list
const LAST_SELECTED_LIST_KEY = 'lastSelectedShoppingListId'
const SHOW_PURCHASED_KEY = 'shoppingListsShowPurchased'
const FILTERS_KEY = 'shoppingListsFilters'

// State
const allShoppingLists = ref<SelectValue[]>([])
const selectedListId = ref<string | null>(null)
const currentListDetails = ref<DetailedShoppingListInfo | null>(null)
const searchQuery = ref('')
const showPurchased = ref(false)
const isLoadingLists = ref(false)
const isLoadingDetails = ref(false)

// Add-item wizard (fullscreen modal) state.
const isAddItemModalOpen = ref(false)
const addItemMode = ref<'product' | 'custom'>('product')
const openAddItemModal = (mode: 'product' | 'custom') => {
  if (!selectedListId.value) return
  addItemMode.value = mode
  isAddItemModalOpen.value = true
}

// Dynamic add-actions on the nav FAB: only when a list is selected. Two options →
// the FAB opens a chooser (see useFabActions); each opens the wizard in a given mode.
useFabActions(() => selectedListId.value
  ? [
      {
        label: $t('pages.shoppingLists.addWithSearch'),
        icon: 'i-lucide-search',
        handler: () => openAddItemModal('product')
      },
      {
        label: $t('pages.shoppingLists.addCustom'),
        icon: 'i-lucide-pencil-line',
        handler: () => openAddItemModal('custom')
      }
    ]
  : [])

// Filter state
const filtersOpen = ref(false)
const deadlineFilter = ref('all') // all | overdue | dueSoon
const locationFilter = ref('all') // all | none | <shoppingLocationPublicId>
const minQuantity = ref<number | null>(null)
const maxQuantity = ref<number | null>(null)

// --- "You are here" geolocation state --------------------------------------
const myPosition = ref<GeoPosition | null>(null)
const isLocating = ref(false)
const locationTracking = ref(false)
// Runtime-geocoded coordinates for locations that have an address but no stored
// coordinates (older records). Keyed by location publicId; null means "couldn't resolve".
const resolvedCoords = ref<Map<string, { lat: number, lon: number } | null>>(new Map())
// Locations we've already fired a proximity notification for (re-armed when left).
const notifiedLocationIds = ref<Set<string>>(new Set())
// All saved shopping locations (loaded once). Used for proximity store detection (so being
// at any store — not just ones on this list — is recognized) and for the item edit location
// picker. Only locations with resolvable coordinates participate in proximity.
const allShoppingLocations = ref<ShoppingLocationInfo[]>([])

// Persistent header (auth layout) — identity + info + the active list's subtitle
// (name, colour dot, shared icon). The subtitle skeletons while a list loads.
usePageHeader(() => ({
  icon: 'i-lucide-shopping-cart',
  title: $t('pages.shoppingLists.title'),
  info: $t('pages.shoppingLists.description'),
  subtitle: currentListDetails.value?.name,
  subtitleColor: currentListDetails.value?.color || undefined,
  subtitleIcon: currentListDetails.value?.isSharedWithFamily ? 'i-lucide-users' : undefined,
  hasSubtitle: isLoadingDetails.value || !!currentListDetails.value,
  hasSearch: true
}))

// Create / edit drawer state (ShoppingListFormDrawer owns the form and the API call).
const isListFormOpen = ref(false)
const editingList = ref<DetailedShoppingListInfo | null>(null)

// Delete modal state (ShoppingListDeleteDrawer owns the API call).
const isDeleteModalOpen = ref(false)

// Helper function to get target date for an item
const getTargetDate = (item: ShoppingListItemInfo): Date | null => {
  const deadlineDate = item.deadlineAt ? new Date(item.deadlineAt) : null
  const dueDate = item.dueAt ? new Date(item.dueAt) : null

  // Use the earlier of deadline or due date
  return deadlineDate && dueDate
    ? (deadlineDate < dueDate ? deadlineDate : dueDate)
    : (deadlineDate || dueDate)
}

// Helper function to get display name for an item
const getDisplayName = (item: ShoppingListItemInfo): string => {
  return item.product?.name || item.customName || 'Unnamed Item'
}

// Computed
const listMenuItems = computed(() => {
  const groups: Record<string, unknown>[][] = [
    [
      {
        label: $t('pages.shoppingLists.menu.add'),
        icon: 'i-lucide-plus',
        onSelect: openCreateModal
      }
    ]
  ]
  // Edit/delete only make sense for the currently selected list.
  if (selectedListId.value) {
    groups.push([
      {
        label: $t('pages.shoppingLists.menu.edit'),
        icon: 'i-lucide-pencil',
        onSelect: openEditModal
      },
      {
        label: $t('pages.shoppingLists.menu.delete'),
        icon: 'i-lucide-trash-2',
        onSelect: openDeleteModal
      }
    ])
  }
  return groups
})

const isSearchEnabled = computed(() => {
  return currentListDetails.value && currentListDetails.value.items.length > 0
})

// --- Filters ---------------------------------------------------------------

const deadlineOptions = computed(() => [
  { label: $t('pages.shoppingLists.filters.all'), value: 'all' },
  { label: $t('pages.shoppingLists.filters.overdue'), value: 'overdue' },
  { label: $t('pages.shoppingLists.filters.dueSoon'), value: 'dueSoon' }
])

// Built from the locations present in the active list's items (+ "no location").
const locationOptions = computed(() => {
  const opts: { label: string, value: string }[] = [
    { label: $t('pages.shoppingLists.filters.all'), value: 'all' }
  ]
  const seen = new Map<string, string>()
  let hasNone = false
  for (const item of currentListDetails.value?.items ?? []) {
    if (item.shoppingLocation) {
      seen.set(item.shoppingLocation.publicId, item.shoppingLocation.name)
    } else {
      hasNone = true
    }
  }
  for (const [value, label] of seen) opts.push({ label, value })
  if (hasNone) opts.push({ label: $t('pages.shoppingLists.filters.noLocation'), value: 'none' })
  return opts
})

const maxItemQuantity = computed(() => {
  const max = (currentListDetails.value?.items ?? []).reduce((m, i) => Math.max(m, i.quantity || 0), 0)
  return Math.max(1, Math.ceil(max))
})

const quantityRange = computed<number[]>({
  get: () => [minQuantity.value ?? 0, maxQuantity.value ?? maxItemQuantity.value],
  set: (range: number[]) => {
    const [lo, hi] = range
    minQuantity.value = lo == null || lo <= 0 ? null : lo
    maxQuantity.value = hi == null || hi >= maxItemQuantity.value ? null : hi
  }
})

const optLabel = (options: { label: string, value: string }[], value: string) =>
  options.find(o => o.value === value)?.label ?? value

const activeFilterCount = computed(() =>
  (deadlineFilter.value !== 'all' ? 1 : 0) +
  (locationFilter.value !== 'all' ? 1 : 0) +
  (minQuantity.value != null || maxQuantity.value != null ? 1 : 0) +
  (showPurchased.value ? 1 : 0)
)

const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (deadlineFilter.value !== 'all') {
    chips.push({
      key: 'deadline',
      label: optLabel(deadlineOptions.value, deadlineFilter.value),
      clear: () => { deadlineFilter.value = 'all' }
    })
  }
  if (locationFilter.value !== 'all') {
    chips.push({
      key: 'location',
      label: optLabel(locationOptions.value, locationFilter.value),
      clear: () => { locationFilter.value = 'all' }
    })
  }
  if (minQuantity.value != null || maxQuantity.value != null) {
    chips.push({
      key: 'quantity',
      label: `${$t('pages.shoppingLists.filterLabels.quantity')}: ${minQuantity.value ?? 0}–${maxQuantity.value ?? maxItemQuantity.value}`,
      clear: () => { minQuantity.value = null; maxQuantity.value = null }
    })
  }
  if (showPurchased.value) {
    chips.push({
      key: 'purchased',
      label: $t('pages.shoppingLists.filters.showPurchased'),
      clear: () => { showPurchased.value = false }
    })
  }
  return chips
})

const clearAllFilters = () => {
  deadlineFilter.value = 'all'
  locationFilter.value = 'all'
  minQuantity.value = null
  maxQuantity.value = null
  showPurchased.value = false
}

const filteredItems = computed(() => {
  if (!currentListDetails.value) return []

  let items = currentListDetails.value.items

  // Filter purchased items
  if (!showPurchased.value) {
    items = items.filter(item => !item.purchasedAt)
  }

  // Search filter
  if (searchQuery.value.trim()) {
    const normalized = normalizeForSearch(searchQuery.value)
    items = items.filter(item => {
      const productName = item.product?.name || ''
      const productBrand = item.product?.brand || ''
      const customName = item.customName || ''
      const note = item.note || ''

      return normalizeForSearch(productName).includes(normalized) ||
             normalizeForSearch(productBrand).includes(normalized) ||
             normalizeForSearch(customName).includes(normalized) ||
             normalizeForSearch(note).includes(normalized)
    })
  }

  // Deadline status
  if (deadlineFilter.value === 'overdue') {
    items = items.filter(item => {
      const d = getTargetDate(item)
      return d ? checkIsExpired(d) : false
    })
  } else if (deadlineFilter.value === 'dueSoon') {
    items = items.filter(item => {
      const d = getTargetDate(item)
      return d ? (!checkIsExpired(d) && checkIsExpiringWithinTwoWeeks(d)) : false
    })
  }

  // Shopping location
  if (locationFilter.value === 'none') {
    items = items.filter(item => !item.shoppingLocation)
  } else if (locationFilter.value !== 'all') {
    items = items.filter(item => item.shoppingLocation?.publicId === locationFilter.value)
  }

  // Quantity range
  if (minQuantity.value != null || maxQuantity.value != null) {
    items = items.filter(item => {
      const q = item.quantity ?? 0
      if (minQuantity.value != null && q < minQuantity.value) return false
      if (maxQuantity.value != null && q > maxQuantity.value) return false
      return true
    })
  }

  // Sort items by urgency and then alphabetically
  // Categorize items
  const overdueItems: ShoppingListItemInfo[] = []
  const dueSoonItems: ShoppingListItemInfo[] = []
  const otherItems: ShoppingListItemInfo[] = []

  items.forEach(item => {
    const targetDate = getTargetDate(item)

    if (targetDate) {
      if (checkIsExpired(targetDate)) {
        overdueItems.push(item)
      } else if (checkIsExpiringWithinTwoWeeks(targetDate)) {
        dueSoonItems.push(item)
      } else {
        otherItems.push(item)
      }
    } else {
      otherItems.push(item)
    }
  })

  // Sort each category alphabetically
  const sortAlphabetically = (a: ShoppingListItemInfo, b: ShoppingListItemInfo) => {
    const nameA = getDisplayName(a).toLowerCase()
    const nameB = getDisplayName(b).toLowerCase()
    return nameA.localeCompare(nameB, 'hu')
  }

  overdueItems.sort(sortAlphabetically)
  dueSoonItems.sort(sortAlphabetically)
  otherItems.sort(sortAlphabetically)

  // Concatenate in priority order
  return [...overdueItems, ...dueSoonItems, ...otherItems]
})

// --- "You are here" geolocation logic --------------------------------------

// Distinct shopping locations referenced by the open list's items, with a count of
// items still to buy (not yet purchased) at each.
const listLocations = computed(() => {
  const map = new Map<string, { location: ShoppingLocationInfo, pendingCount: number }>()
  for (const item of currentListDetails.value?.items ?? []) {
    const loc = item.shoppingLocation
    if (!loc) continue
    const entry = map.get(loc.publicId)
    const pending = item.purchasedAt ? 0 : 1
    if (entry) entry.pendingCount += pending
    else map.set(loc.publicId, { location: loc, pendingCount: pending })
  }
  return [...map.values()]
})

// Resolve a location's coordinates: stored coordinates win, otherwise fall back to any
// runtime-geocoded coordinates we've cached for it.
const coordsFor = (loc: ShoppingLocationInfo): { lat: number, lon: number } | null => {
  if (typeof loc.latitude === 'number' && typeof loc.longitude === 'number') {
    return { lat: loc.latitude, lon: loc.longitude }
  }
  return resolvedCoords.value.get(loc.publicId) ?? null
}

// Geocode any list locations that lack stored coordinates (older records). Cached so each
// distinct location is only geocoded once per session.
const ensureCoordsResolved = async () => {
  for (const location of allShoppingLocations.value) {
    const hasStored = typeof location.latitude === 'number' && typeof location.longitude === 'number'
    if (hasStored || resolvedCoords.value.has(location.publicId)) continue

    const query = buildLocationQuery(location)
    if (!query) {
      resolvedCoords.value = new Map(resolvedCoords.value).set(location.publicId, null)
      continue
    }
    const coords = await geocode(query)
    resolvedCoords.value = new Map(resolvedCoords.value).set(
      location.publicId,
      coords ? { lat: coords.lat, lon: coords.lon } : null
    )
  }
}

const buildLocationQuery = (loc: ShoppingLocationInfo): string =>
  [loc.address, loc.postalCode, loc.city, loc.country]
    .map(p => p?.trim())
    .filter(Boolean)
    .join(', ')

// Location publicIds within NEARBY_RADIUS_METERS of the user's current position. Considers
// ALL saved shopping locations (not just ones on the list), so the "similar store" detection
// works even when the store you're standing in has no items on the current list.
const nearbyLocationIds = computed(() => {
  const ids = new Set<string>()
  const pos = myPosition.value
  if (!pos) return ids
  for (const location of allShoppingLocations.value) {
    const coords = coordsFor(location)
    if (!coords) continue
    if (distanceMeters(pos.lat, pos.lon, coords.lat, coords.lon) <= NEARBY_RADIUS_METERS) {
      ids.add(location.publicId)
    }
  }
  return ids
})

// Nearby locations that still have items to buy — drives the "you are here" banner.
const nearbyLocations = computed(() =>
  listLocations.value.filter(l => l.pendingCount > 0 && nearbyLocationIds.value.has(l.location.publicId))
)

const nearbyPendingTotal = computed(() =>
  nearbyLocations.value.reduce((sum, l) => sum + l.pendingCount, 0)
)

const nearbyLocationNames = computed(() =>
  nearbyLocations.value.map(l => l.location.name).join(', ')
)

const isItemAtCurrentLocation = (item: ShoppingListItemInfo): boolean =>
  !!item.shoppingLocation && nearbyLocationIds.value.has(item.shoppingLocation.publicId)

// The union of store types of the saved location(s) the user is currently standing in
// (excluding Other). Drives the "similar store here" highlight.
const currentStoreTypes = computed(() => {
  const types = new Set<StoreType>()
  for (const location of allShoppingLocations.value) {
    if (!nearbyLocationIds.value.has(location.publicId)) continue
    for (const t of location.storeTypes ?? []) {
      if (t !== StoreType.Other) types.add(t)
    }
  }
  return types
})

// True when the item's store is NOT the one you're at, but shares at least one (non-Other)
// store type with a store you're currently at — e.g. its Tesco items while you're in an Auchan.
const isItemSimilarTypeHere = (item: ShoppingListItemInfo): boolean => {
  const loc = item.shoppingLocation
  if (!loc || nearbyLocationIds.value.has(loc.publicId)) return false
  if (currentStoreTypes.value.size === 0) return false
  return (loc.storeTypes ?? []).some(t => t !== StoreType.Other && currentStoreTypes.value.has(t))
}

// The store the user is currently standing at, used to pre-fill an item's purchase location.
// Prefers the item's own store if the user is at it, otherwise the first nearby saved store.
const currentStoreForItem = (item: ShoppingListItemInfo): ShoppingLocationInfo | undefined => {
  const ids = nearbyLocationIds.value
  if (!ids.size) return undefined
  if (item.shoppingLocation && ids.has(item.shoppingLocation.publicId)) return item.shoppingLocation
  return allShoppingLocations.value.find(l => ids.has(l.publicId))
}

const onPositionUpdate = (position: GeoPosition) => {
  myPosition.value = position
  ensureCoordsResolved().then(() => checkProximityNotification())
}

const enableLocation = async () => {
  if (!isGeoSupported.value) {
    toast.add({
      title: $t('pages.shoppingLists.nearby.unsupportedTitle'),
      description: $t('pages.shoppingLists.nearby.unsupportedBody'),
      color: 'warning',
      icon: 'i-lucide-map-pin-off'
    })
    return
  }
  isLocating.value = true
  try {
    myPosition.value = await getCurrentPosition()
    locationTracking.value = true
    if (!allShoppingLocations.value.length) await loadAllShoppingLocations()
    await ensureCoordsResolved()
    checkProximityNotification()
    startWatch(onPositionUpdate)
  } catch {
    // Permission denied, timeout, or position unavailable.
    toast.add({
      title: $t('pages.shoppingLists.nearby.deniedTitle'),
      description: $t('pages.shoppingLists.nearby.deniedBody'),
      color: 'error',
      icon: 'i-lucide-map-pin-off'
    })
  } finally {
    isLocating.value = false
  }
}

const disableLocation = () => {
  stopWatch()
  locationTracking.value = false
  myPosition.value = null
  notifiedLocationIds.value = new Set()
}

const toggleLocation = () => {
  if (locationTracking.value) disableLocation()
  else enableLocation()
}

// Fire a one-off local notification when arriving at a store with items to buy. Foreground
// only (the app must be open); re-arms once the user leaves the store's radius.
const checkProximityNotification = async () => {
  if (!import.meta.client) return
  const nearby = nearbyLocationIds.value

  // Re-arm any location the user has since left.
  const stillNearby = new Set<string>()
  for (const id of notifiedLocationIds.value) {
    if (nearby.has(id)) stillNearby.add(id)
  }
  notifiedLocationIds.value = stillNearby

  if (notificationPermission.value !== 'granted') return

  for (const { location, pendingCount } of nearbyLocations.value) {
    if (pendingCount <= 0 || notifiedLocationIds.value.has(location.publicId)) continue
    notifiedLocationIds.value.add(location.publicId)
    await showProximityNotification(location.name, pendingCount)
  }
}

const showProximityNotification = async (locationName: string, count: number) => {
  try {
    const registration = await navigator.serviceWorker?.ready
    if (!registration) return
    await registration.showNotification(
      $t('pages.shoppingLists.nearby.notificationTitle', { location: locationName }),
      {
        body: $t('pages.shoppingLists.nearby.notificationBody', { count }),
        icon: '/apple-touch-icon-180x180.png',
        badge: '/favicon-32x32.png',
        tag: `shopping-nearby-${locationName}`,
        data: { url: '/shopping-lists' }
      }
    )
  } catch (error) {
    console.error('Failed to show proximity notification:', error)
  }
}

// Load all saved shopping locations (once) for proximity detection + the item edit picker.
const loadAllShoppingLocations = async () => {
  try {
    const res = await getShoppingLocations({ returnAll: true })
    if (res.success && res.data) allShoppingLocations.value = res.data.items ?? []
  } catch {
    // Non-fatal: proximity simply has nothing to match and the edit picker shows an empty list.
  }
}

// Methods
const loadShoppingLists = async () => {
  isLoadingLists.value = true
  try {
    const response = await getSelectValues(SelectValueType.ShoppingList)

    if (response.success && response.data) {
      allShoppingLists.value = response.data
      
      // Try to restore last selected list from localStorage
      const lastSelectedId = localStorage.getItem(LAST_SELECTED_LIST_KEY)
      
      if (lastSelectedId && allShoppingLists.value.some(list => list.publicId === lastSelectedId)) {
        // If last selected list exists in the current list, select it
        selectedListId.value = lastSelectedId
      } else if (allShoppingLists.value.length > 0 && allShoppingLists.value[0]) {
        // Otherwise, auto-select first list if available
        selectedListId.value = allShoppingLists.value[0].publicId
      }
    }
  } catch (error) {
    console.error('Failed to load shopping lists:', error)
  } finally {
    isLoadingLists.value = false
  }
}

const fetchListDetailsRest = async (publicId: string) => {
  const response = await getShoppingListDetails(publicId, showPurchased.value)
  if (response.success && response.data) {
    currentListDetails.value = response.data
  }
}

const loadListDetails = async (publicId: string) => {
  isLoadingDetails.value = true
  searchQuery.value = '' // Reset search when switching lists

  try {
    // Join the realtime channel; the hub returns the current snapshot, so no extra
    // fetch is needed. Falls back to a plain REST fetch when sockets are unavailable.
    const snapshot = await socket.joinList(publicId, showPurchased.value)
    if (snapshot) {
      currentListDetails.value = snapshot
    } else {
      await fetchListDetailsRest(publicId)
    }
  } catch (error) {
    console.error('Failed to load list details:', error)
    try { await fetchListDetailsRest(publicId) } catch { /* already logged above */ }
  } finally {
    isLoadingDetails.value = false
  }
}

// List CRUD openers — the shared drawers own the forms and the API calls, so these only decide
// which mode to open in. The names are referenced by listMenuItems.
const openCreateModal = () => {
  filtersOpen.value = false
  editingList.value = null
  isListFormOpen.value = true
}

const openEditModal = () => {
  if (!currentListDetails.value) return

  filtersOpen.value = false
  editingList.value = currentListDetails.value
  isListFormOpen.value = true
}

const openDeleteModal = () => {
  if (!currentListDetails.value) return

  filtersOpen.value = false
  isDeleteModalOpen.value = true
}

const onListSaved = async (list: ShoppingListInfo) => {
  // Read the mode before anything resets editingList — the drawer emits `saved` first.
  const wasCreate = !editingList.value

  // The list selector is populated from select values ({ publicId, text } only), so the saved DTO
  // cannot patch it — refetch. On create this also puts the new list in allShoppingLists before it
  // is selected.
  await loadShoppingLists()

  if (wasCreate) {
    // The selectedListId watcher loads the details and joins the socket group.
    selectedListId.value = list.publicId
  } else if (selectedListId.value) {
    await loadListDetails(selectedListId.value)
  }
}

const onListDeleted = async () => {
  selectedListId.value = null
  currentListDetails.value = null
  // Auto-selects the first remaining list.
  await loadShoppingLists()
}

// Handle item refresh (when item is updated, deleted, purchased, or restored).
// The realtime channel keeps the open list in sync — including this client's own change,
// echoed back from the server — so a manual refetch is only needed when the socket is down.
const handleItemRefresh = async () => {
  if (!socket.isConnected.value && selectedListId.value) {
    await loadListDetails(selectedListId.value)
  }
}

// --- Realtime handlers: mutate the open list in place instead of refetching. ---
const handleRealtimeItemUpserted = (item: ShoppingListItemInfo) => {
  if (!currentListDetails.value || item.shoppingListPublicId !== selectedListId.value) return
  const items = currentListDetails.value.items
  const index = items.findIndex(i => i.publicId === item.publicId)
  if (index >= 0) items[index] = item
  else items.push(item)
  // Nudge the bottom-nav deadline badge to recount.
  emitBusEvent('shopping-list-item:updated')
}

const handleRealtimeItemDeleted = (payload: { publicId: string; shoppingListPublicId: string }) => {
  if (!currentListDetails.value || payload.shoppingListPublicId !== selectedListId.value) return
  currentListDetails.value.items = currentListDetails.value.items.filter(i => i.publicId !== payload.publicId)
  emitBusEvent('shopping-list-item:deleted')
}

const handleRealtimeListUpdated = (list: ShoppingListInfo) => {
  if (!currentListDetails.value || list.publicId !== selectedListId.value) return
  currentListDetails.value.name = list.name
  currentListDetails.value.description = list.description
  currentListDetails.value.color = list.color
  currentListDetails.value.isSharedWithFamily = list.isSharedWithFamily
}

const handleRealtimeListDeleted = (payload: { publicId: string }) => {
  if (payload.publicId !== selectedListId.value) return
  // The open list was deleted by someone else — reset selection and refresh the selector.
  currentListDetails.value = null
  selectedListId.value = null
  loadShoppingLists()
}

const handleSocketReconnected = () => {
  // Re-sync the snapshot after a dropped connection (this also re-joins the group).
  if (selectedListId.value) loadListDetails(selectedListId.value)
}

// Barcode scanner handler
const handleBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Watchers
watch(selectedListId, (newId, oldId) => {
  // Reset list-dependent filters when switching lists (options differ per list).
  locationFilter.value = 'all'
  minQuantity.value = null
  maxQuantity.value = null

  // Leave the previous list's channel so we stop receiving its events.
  if (oldId) socket.leaveList(oldId)

  // Re-arm proximity notifications for the newly selected list.
  notifiedLocationIds.value = new Set()

  if (newId) {
    // Drop the previous list so the skeleton (not the wrong list's items) shows
    // while the new one loads, and so the new list gets a fresh staggered render
    // instead of every old card bursting as every new one bubbles in.
    currentListDetails.value = null
    loadListDetails(newId)
    // Save to localStorage
    localStorage.setItem(LAST_SELECTED_LIST_KEY, newId)
  } else {
    currentListDetails.value = null
    // Clear from localStorage if no list is selected
    localStorage.removeItem(LAST_SELECTED_LIST_KEY)
  }
})

// Persist the (list-independent) deadline filter.
watch(deadlineFilter, (value) => {
  localStorage.setItem(FILTERS_KEY, JSON.stringify({ deadline: value }))
})

watch(showPurchased, (newValue) => {
  // Save to localStorage
  localStorage.setItem(SHOW_PURCHASED_KEY, String(newValue))
  
  // Reload list details
  if (selectedListId.value) {
    loadListDetails(selectedListId.value)
  }
})

// Lifecycle
onMounted(() => {
  // Register realtime handlers before any list is joined so no events are missed.
  socket.on('ItemUpserted', handleRealtimeItemUpserted)
  socket.on('ItemDeleted', handleRealtimeItemDeleted)
  socket.on('ListUpdated', handleRealtimeListUpdated)
  socket.on('ListDeleted', handleRealtimeListDeleted)
  socket.onReconnected(handleSocketReconnected)

  // Restore showPurchased state from localStorage
  const savedShowPurchased = localStorage.getItem(SHOW_PURCHASED_KEY)
  if (savedShowPurchased !== null) {
    showPurchased.value = savedShowPurchased === 'true'
  }

  // Restore the persisted deadline filter (whitelist-validated)
  const savedFilters = localStorage.getItem(FILTERS_KEY)
  if (savedFilters) {
    try {
      const parsed = JSON.parse(savedFilters)
      if (['all', 'overdue', 'dueSoon'].includes(parsed.deadline)) {
        deadlineFilter.value = parsed.deadline
      }
    } catch {
      // Ignore malformed stored filters
    }
  }

  loadShoppingLists()
  loadAllShoppingLocations()
})

onBeforeUnmount(() => {
  // Tear down realtime subscriptions and leave the open list's channel (the shared
  // connection itself stays alive for other pages / quick re-entry).
  socket.off('ItemUpserted', handleRealtimeItemUpserted)
  socket.off('ItemDeleted', handleRealtimeItemDeleted)
  socket.off('ListUpdated', handleRealtimeListUpdated)
  socket.off('ListDeleted', handleRealtimeListDeleted)
  socket.offReconnected(handleSocketReconnected)
  if (selectedListId.value) socket.leaveList(selectedListId.value)

  // Stop watching the device position when leaving the page.
  stopWatch()
})
</script>
