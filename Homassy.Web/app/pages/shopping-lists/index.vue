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

    <!-- Who else has this list open right now, and whether the realtime connection is healthy —
         trailing header actions, next to the title. -->
    <Teleport to="#app-header-actions">
      <PresenceAvatars v-if="currentListDetails" :members="socket.presentMembers.value" />
      <RealtimeConnectionBar variant="chip" />
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
        <SkeletonCard v-for="i in 8" :key="i" :lines="1" :footer-lines="3" />
      </div>

      <!-- Empty: No Lists -->
      <EmptyState
        v-else-if="!isLoadingLists && allShoppingLists.length === 0"
        illustration="shoppingList"
        :title="$t('pages.shoppingLists.noListsFound')"
        :description="$t('pages.shoppingLists.noListsHint')"
        :action-label="$t('pages.shoppingLists.menu.add')"
        action-icon="i-lucide-plus"
        @action="openCreateModal"
      />

      <!-- Empty: Lists exist but none selected -->
      <EmptyState
        v-else-if="!isLoadingLists && !selectedListId"
        illustration="shoppingList"
        :title="$t('pages.shoppingLists.filters.selectListPrompt')"
        :action-label="$t('pages.shoppingLists.filters.toggle')"
        action-icon="i-lucide-sliders-horizontal"
        @action="filtersOpen = true"
      />

      <!-- A list is open -->
      <template v-else>
        <!-- Both empty states render next to the (then empty) grid, never in
             place of it: unmounting the grid replays the enter animation on the
             way back and swallows the leave animation of the last item removed. -->
        <!-- Empty: No Items in List -->
        <EmptyState
          v-if="currentListDetails && currentListDetails.items.length === 0"
          illustration="shoppingList"
          :title="$t('pages.shoppingLists.noItemsInList')"
          :description="$t('pages.shoppingLists.noItemsInListHint')"
          :action-label="$t('pages.shoppingLists.addProductButton')"
          action-icon="i-lucide-plus"
          @action="openAddItemModal('product')"
        />

        <!-- Empty: No Search Results -->
        <EmptyState
          v-else-if="filteredItems.length === 0"
          illustration="search"
          :title="$t('pages.shoppingLists.noSearchResults')"
          :description="$t('pages.shoppingLists.noSearchResultsHint')"
          :action-label="$t('common.filters.clear')"
          action-icon="i-lucide-filter-x"
          @action="clearAllFilters"
        />

        <!-- Estimated total (#128) — from the best price the household has actually paid for each
             product. Deliberately states the unpriced count out loud rather than quietly leaving
             those items out: a total that silently omits three items reads as the cost of the whole
             list, which is the one way this could actively mislead someone at the till. Several
             currencies are several lines, never one combined figure - there is no exchange rate in
             this milestone. -->
        <div v-if="hasEstimate" class="mb-4 rounded-lg bg-elevated px-3 py-2">
          <div class="flex items-center gap-2 text-sm">
            <UIcon name="i-lucide-receipt" class="h-4 w-4 shrink-0 text-primary" />
            <span class="font-medium text-muted">{{ $t('price.estimate.label') }}</span>
          </div>
          <div class="mt-1 flex flex-col gap-0.5 text-sm">
            <span v-for="line in estimateLines" :key="line" class="font-semibold text-highlighted tabular-nums">
              {{ line }}
            </span>
            <span v-if="estimateLines.length === 0" class="text-muted">{{ $t('price.estimate.noneKnown') }}</span>
            <span v-if="listEstimate.unpricedCount > 0" class="text-muted">
              {{ $t('price.estimate.unpriced', { count: listEstimate.unpricedCount }) }}
            </span>
          </div>
        </div>

        <!-- "Buy here" section — the items you can pick up in the store you're standing in
             (exact store + same-type stores), pinned above the rest of the list. Rendered as
             its own grid rather than a spanning header inside one: two TransitionGroups keep
             their own FLIP geometry, a header child would join the animation. -->
        <template v-if="hereItems.length">
          <div class="flex items-center gap-2 mb-3">
            <UIcon name="i-lucide-store" class="h-5 w-5 text-blue-600 dark:text-blue-400 shrink-0" />
            <h2 class="text-sm font-semibold text-gray-800 dark:text-gray-200">
              {{ $t('pages.shoppingLists.nearby.sectionHere', { location: currentLocationNames }) }}
            </h2>
            <UBadge color="primary" variant="soft" size="sm" class="shrink-0">
              {{ $t('pages.shoppingLists.nearby.sectionPending', { count: herePendingCount }) }}
            </UBadge>
          </div>
          <AnimatedList class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4 mb-6">
            <div
              v-for="entry in hereItemsWithAttribution"
              :key="entry.item.publicId"
              class="relative rounded-2xl"
              :class="{ 'item-attribution-flash': !!entry.attribution, 'row-updated-flash': entry.updated }"
              :style="entry.attribution?.style"
            >
              <ShoppingListItemCard
                :item="entry.item"
                :search-query="searchQuery"
                :best-price="bestPriceFor(entry.item)"
                :at-current-location="isItemAtCurrentLocation(entry.item)"
                :similar-type-at-current-location="isItemSimilarTypeHere(entry.item)"
                :shopping-locations="allShoppingLocations"
                :current-store="currentStoreForItem(entry.item)"
                @refresh="handleItemRefresh"
                @delete-requested="handleDeleteRequested(entry.item)"
                @purchase-requested="(request) => handlePurchaseRequested(entry.item, request)"
                @restore-requested="handleRestoreRequested(entry.item)"
              />
              <div v-if="entry.attribution" class="item-attribution-label">
                <span class="item-attribution-dot" :style="entry.attribution.style" />
                {{ $t('shoppingList.changedBy', { name: entry.attribution.name }) }}
              </div>
              <span v-if="entry.updated" class="sr-only">{{ $t('realtime.updatedFlash') }}</span>
            </div>
          </AnimatedList>
          <div v-if="restItems.length" class="flex items-center gap-2 mb-3">
            <UIcon name="i-lucide-list" class="h-5 w-5 text-gray-500 dark:text-gray-400 shrink-0" />
            <h2 class="text-sm font-semibold text-gray-800 dark:text-gray-200">
              {{ $t('pages.shoppingLists.nearby.sectionRest') }}
            </h2>
          </div>
        </template>

        <!-- Items Grid (everything not buyable here; the whole list away from a store) -->
        <AnimatedList class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          <div
            v-for="entry in restItemsWithAttribution"
            :key="entry.item.publicId"
            class="relative rounded-2xl"
            :class="{ 'item-attribution-flash': !!entry.attribution, 'row-updated-flash': entry.updated }"
            :style="entry.attribution?.style"
          >
            <ShoppingListItemCard
              :item="entry.item"
              :search-query="searchQuery"
              :best-price="bestPriceFor(entry.item)"
              :at-current-location="isItemAtCurrentLocation(entry.item)"
              :similar-type-at-current-location="isItemSimilarTypeHere(entry.item)"
              :shopping-locations="allShoppingLocations"
              :current-store="currentStoreForItem(entry.item)"
              @refresh="handleItemRefresh"
              @deleted="handleItemRefresh"
            />
            <div v-if="entry.attribution" class="item-attribution-label">
              <span class="item-attribution-dot" :style="entry.attribution.style" />
              {{ $t('shoppingList.changedBy', { name: entry.attribution.name }) }}
            </div>
            <span v-if="entry.updated" class="sr-only">{{ $t('realtime.updatedFlash') }}</span>
          </div>
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
            <div class="flex flex-wrap gap-2">
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
              <!-- Not a filter: the auto-start preference for location tracking. -->
              <UButton
                v-if="isGeoSupported"
                :label="$t('pages.shoppingLists.filters.autoLocate')"
                icon="i-lucide-locate-fixed"
                size="sm"
                class="rounded-full"
                :color="autoLocate ? 'primary' : 'neutral'"
                :variant="autoLocate ? 'solid' : 'outline'"
                :aria-pressed="autoLocate"
                @click="toggleAutoLocate"
              />
            </div>
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
      :initial-name="sharedItemName"
    />

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="handleBarcodeScanned" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import type { SelectValue } from '../../types/selectValue'
import type { DetailedShoppingListInfo, ShoppingListItemInfo, ShoppingListInfo, PurchaseShoppingListItemRequest } from '../../types/shoppingList'
import type { ItemDeletedEvent, ItemUpsertedEvent } from '../../types/realtime'
import { SelectValueType, StoreType } from '../../types/enums'
import { useSelectValueApi } from '../../composables/api/useSelectValueApi'
import { useShoppingListApi } from '../../composables/api/useShoppingListApi'
import { useLocationsApi } from '../../composables/api/useLocationsApi'
import { useInsightsApi } from '../../composables/api/useInsightsApi'
import type { BestKnownPrice } from '../../types/insights'
import { estimateListTotal } from '../../utils/priceEstimate'
import { formatCurrency } from '../../utils/chart/format'
import { normalizeForSearch } from '../../utils/stringUtils'
import { useCameraAvailability } from '../../composables/useCameraAvailability'
import type { GeoPosition } from '../../composables/useGeolocation'
import type { ShoppingLocationInfo } from '../../types/location'
import { useAuthStore } from '../../stores/auth'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { t: $t, locale } = useI18n()
const { getSelectValues } = useSelectValueApi()
const { getShoppingListDetails, deleteShoppingListItem, purchaseShoppingListItem, restorePurchaseShoppingListItem } = useShoppingListApi()
const { getShoppingLocations } = useLocationsApi()
const { getBestPrices } = useInsightsApi()
const { showCameraButton } = useCameraAvailability()
const { isExpired: checkIsExpired, isExpiringWithinTwoWeeks: checkIsExpiringWithinTwoWeeks } = useExpirationCheck()
const toast = useToast()

// Geolocation-based "you are here" highlighting (foreground-only; see useGeolocation).
const { isSupported: isGeoSupported, getPermissionStatus, getCurrentPosition, startWatch, stopWatch } = useGeolocation()
const { geocode } = useGeocoding()
const { permissionStatus: notificationPermission } = usePushNotifications()

// Realtime channel for the currently-open list (see useShoppingListSocket).
const socket = useShoppingListSocket()
const { emit: emitBusEvent } = useEventBus()
const { accentStyle } = useMemberColor()
const authStore = useAuthStore()
// The optimistic undo queue (see useUndoableAction.ts) — this page owns currentListDetails.items,
// so it is the one that runs() the delete/purchase/restore actions ShoppingListItemCard requests.
const { run, isPendingEntity } = useUndoableAction()

/**
 * The signed-in member's own public id. Used only to make sure a foreign-change flash never
 * fires for the user's own edit.
 */
const currentUserPublicId = computed(() => authStore.user?.publicId ?? null)

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

// --- Best known prices + the estimated total (#128) ----------------------------------------
//
// One request per list, keyed by product public id — never one per row. The API asserts the
// single-round-trip property in its own tests (PriceInsightTests); this side's part of the bargain
// is asking once for every id on the list, which is what the watcher below does.

const bestPricesByProduct = ref<Record<string, BestKnownPrice>>({})

/**
 * Every distinct product on the open list, sorted so the watcher below can compare two id sets as
 * one string. Custom (product-less) rows carry no product to price and drop out here.
 */
const listProductPublicIds = computed(() => [...new Set(
  (currentListDetails.value?.items ?? [])
    .map(item => item.productPublicId)
    .filter((id): id is string => !!id)
)].sort())

const loadBestPrices = async (productPublicIds: readonly string[]) => {
  if (productPublicIds.length === 0) {
    bestPricesByProduct.value = {}
    return
  }

  try {
    const response = await getBestPrices(productPublicIds)
    // A product with no recorded price is OMITTED from the map rather than returned as null, so
    // "no key" is the single no-price-known signal and no row can render a phantom zero.
    bestPricesByProduct.value = response.success && response.data ? response.data : {}
  } catch (error) {
    // A missing estimate is a missing nicety, never a broken list: leave the map empty and let
    // every row render exactly as it did before this feature existed.
    console.error('Failed to load best prices:', error)
    bestPricesByProduct.value = {}
  }
}

// Watches the id set as a joined string rather than the array itself, so this fires when the list's
// products actually change (switching lists, adding or removing a row, a realtime patch) and not on
// every unrelated recompute of the details object.
watch(
  () => listProductPublicIds.value.join(','),
  () => loadBestPrices(listProductPublicIds.value),
  { immediate: true }
)

const bestPriceFor = (item: ShoppingListItemInfo): BestKnownPrice | undefined =>
  item.productPublicId ? bestPricesByProduct.value[item.productPublicId] : undefined

/**
 * The estimate covers what is still to be bought — every non-purchased row on the list, not the
 * filtered view. The header sits above a filtered grid, but the question it answers ("what will
 * this shop cost") is about the list, and a total that moved with a search box would be worse than
 * no total at all.
 */
const listEstimate = computed(() => estimateListTotal(
  (currentListDetails.value?.items ?? [])
    .filter(item => !item.purchasedAt)
    .map((item) => {
      const best = bestPriceFor(item)
      return {
        productId: item.productPublicId ?? item.publicId,
        quantity: item.quantity,
        bestPrice: best ? { unitPrice: best.unitPrice, currency: best.currency } : undefined
      }
    })
))

/** One line per currency, ordered so the lines do not reshuffle between renders. */
const estimateLines = computed(() => Object.entries(listEstimate.value.totalsByCurrency)
  .sort(([a], [b]) => a.localeCompare(b))
  .map(([currency, total]) => $t('price.estimate.approx', {
    amount: formatCurrency(total, currency, locale.value)
  })))

/** Nothing to estimate at all (an empty or fully-purchased list) shows no summary. */
const hasEstimate = computed(() => listEstimate.value.pricedCount > 0 || listEstimate.value.unpricedCount > 0)

// Add-item wizard (fullscreen modal) state.
const isAddItemModalOpen = ref(false)
const addItemMode = ref<'product' | 'custom'>('product')
const openAddItemModal = (mode: 'product' | 'custom') => {
  if (!selectedListId.value) return
  addItemMode.value = mode
  isAddItemModalOpen.value = true
}

// --- Share target / app-shortcut arrivals (#118) ----------------------------
// A name parked by /share for a custom item. Read once, then cleared: it is a
// one-time intent, and `AddShoppingListItemModal` only seeds from it on open.
const { takeHandoffItemName } = useShareTarget()
const sharedItemName = ref<string | undefined>(undefined)

/**
 * The mode an arriving deep link asked for but could not be given yet, because
 * `loadShoppingLists()` had not picked a list. `openAddItemModal` refuses without
 * one, so the intent is parked and the watcher below fires it the moment a list
 * exists — otherwise a shortcut into a cold start would silently do nothing.
 */
const pendingAddItemMode = ref<'product' | 'custom' | null>(null)

function requestAddItem(mode: 'product' | 'custom') {
  if (selectedListId.value) {
    openAddItemModal(mode)
  } else {
    pendingAddItemMode.value = mode
  }
}

watch(selectedListId, (id) => {
  if (!id || !pendingAddItemMode.value) return
  const mode = pendingAddItemMode.value
  pendingAddItemMode.value = null
  openAddItemModal(mode)
})

useDeepLinkAction({
  add: () => requestAddItem('product'),
  'add-custom': () => {
    sharedItemName.value = takeHandoffItemName() ?? undefined
    requestAddItem('custom')
  }
})

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
// User preference (persisted with the filters): start tracking on load when the browser has
// already granted the geolocation permission. Never prompts — see maybeAutoStartLocation.
const autoLocate = ref(true)
// Guards the auto-start so it is attempted once per page visit, not on every list switch.
const autoStartAttempted = ref(false)
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

// Built from the locations present in the active list's items (+ "no location"), plus the
// "buy here" pseudo-value while the user is actually standing in one of their stores.
const locationOptions = computed(() => {
  const opts: { label: string, value: string }[] = [
    { label: $t('pages.shoppingLists.filters.all'), value: 'all' }
  ]
  if (isAtAnyStore.value) {
    opts.push({ label: $t('pages.shoppingLists.filters.buyHere'), value: 'here' })
  }
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
  if (locationFilter.value === 'here') {
    items = items.filter(isItemBuyableHere)
  } else if (locationFilter.value === 'none') {
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

// True while tracking is on AND the user is inside the radius of at least one saved store.
// Gates both the "buy here" section and the "buy here" filter value.
const isAtAnyStore = computed(() => locationTracking.value && nearbyLocationIds.value.size > 0)

// Names of every saved store the user is currently standing in — including stores with no
// items on the open list, which is what makes the same-type matches legible in the header.
const currentLocationNames = computed(() =>
  allShoppingLocations.value
    .filter(l => nearbyLocationIds.value.has(l.publicId))
    .map(l => l.name)
    .join(', ')
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

// Everything buyable at the store the user is standing in: the exact store as well as a
// different store sharing a type with it. The cards keep their own colour distinction
// (blue vs cyan dashed) — this only decides which section an item lands in.
const isItemBuyableHere = (item: ShoppingListItemInfo): boolean =>
  isItemAtCurrentLocation(item) || isItemSimilarTypeHere(item)

// The "buy here" section pinned to the top of the list, and the rest below it. Both keep
// the urgency ordering of filteredItems; away from any store everything stays in one group.
const hereItems = computed(() =>
  isAtAnyStore.value ? filteredItems.value.filter(isItemBuyableHere) : []
)

const restItems = computed(() =>
  isAtAnyStore.value ? filteredItems.value.filter(item => !isItemBuyableHere(item)) : filteredItems.value
)

const herePendingCount = computed(() => hereItems.value.filter(item => !item.purchasedAt).length)

// --- Per-item "changed by" flash --------------------------------------------
// ShoppingListItemCard is out of scope for this task, so the flash ring and the "changed by"
// label are painted by a thin wrapper this page owns around each card (see the template) rather
// than inside the card component itself. Keyed by item publicId; cleared after
// ATTRIBUTION_FLASH_MS or as soon as the item is deleted.
interface ItemAttribution {
  name: string
  // Pre-resolved --member-color style, ready to :style-bind on both the wrapper and the label's
  // dot. Derived from accentStyle's own return type (useMemberColor.ts's MemberAccentStyle isn't
  // exported) so this never drifts from what accentStyle actually returns.
  style: ReturnType<typeof accentStyle>
}

const attributions = ref<Record<string, ItemAttribution>>({})
const attributionTimers = new Map<string, ReturnType<typeof setTimeout>>()
// Keep in sync with --attribution-flash in main.css.
const ATTRIBUTION_FLASH_MS = 1500

// --- Reconnect "updated" flash ---------------------------------------------
// Neutral (not member-coloured) — see .row-updated-flash in main.css. Fires when
// handleSocketReconnected's diff finds a row whose payload actually changed while the socket was
// down, unlike the attribution flash above, which only ever fires for a live event from another
// present member. Its own map/timers on purpose: the two can in principle overlap (a live edit
// arriving just after a reconnect resync) and each clears independently.
const rowUpdates = ref<Set<string>>(new Set())
const rowUpdateTimers = new Map<string, ReturnType<typeof setTimeout>>()

const flashUpdatedRow = (itemPublicId: string) => {
  rowUpdates.value = new Set(rowUpdates.value).add(itemPublicId)
  const existingTimer = rowUpdateTimers.get(itemPublicId)
  if (existingTimer) clearTimeout(existingTimer)
  rowUpdateTimers.set(itemPublicId, setTimeout(() => {
    rowUpdateTimers.delete(itemPublicId)
    const next = new Set(rowUpdates.value)
    next.delete(itemPublicId)
    rowUpdates.value = next
  }, ATTRIBUTION_FLASH_MS))
}

// Present members, keyed by publicId. Item events carry only actorPublicId, never a name/colour,
// so presentMembers (which already excludes the current user — see useShoppingListSocket) is the
// only place to resolve who a *foreign* actor actually is.
const presenceByPublicId = computed(() => new Map(socket.presentMembers.value.map(m => [m.publicId, m])))

/** Attach the ~1.5s flash to `itemPublicId` when `actorPublicId` names a present, foreign member. */
const attributeChange = (itemPublicId: string, actorPublicId?: string | null) => {
  if (!actorPublicId || actorPublicId === currentUserPublicId.value) return

  const actor = presenceByPublicId.value.get(actorPublicId)
  // Actor already left presence (rare race between the item event and their disconnect) — with
  // no name to show, skip the flash rather than attribute to "someone".
  if (!actor) return

  attributions.value = {
    ...attributions.value,
    [itemPublicId]: { name: actor.displayName, style: accentStyle(actor.publicId, actor.identityColor) }
  }

  const existingTimer = attributionTimers.get(itemPublicId)
  if (existingTimer) clearTimeout(existingTimer)
  attributionTimers.set(itemPublicId, setTimeout(() => {
    attributionTimers.delete(itemPublicId)
    const next = { ...attributions.value }
    Reflect.deleteProperty(next, itemPublicId)
    attributions.value = next
  }, ATTRIBUTION_FLASH_MS))
}

/** Drop any pending flash (attribution or "updated") for a deleted item — nothing left on screen
 *  to keep it lit. */
const clearAttribution = (itemPublicId: string) => {
  const existingTimer = attributionTimers.get(itemPublicId)
  if (existingTimer) {
    clearTimeout(existingTimer)
    attributionTimers.delete(itemPublicId)
  }
  if (itemPublicId in attributions.value) {
    const next = { ...attributions.value }
    Reflect.deleteProperty(next, itemPublicId)
    attributions.value = next
  }

  const existingRowTimer = rowUpdateTimers.get(itemPublicId)
  if (existingRowTimer) {
    clearTimeout(existingRowTimer)
    rowUpdateTimers.delete(itemPublicId)
  }
  if (rowUpdates.value.has(itemPublicId)) {
    const next = new Set(rowUpdates.value)
    next.delete(itemPublicId)
    rowUpdates.value = next
  }
}

const withAttribution = (items: ShoppingListItemInfo[]) =>
  items.map(item => ({
    item,
    attribution: attributions.value[item.publicId] ?? null,
    updated: rowUpdates.value.has(item.publicId)
  }))

const hereItemsWithAttribution = computed(() => withAttribution(hereItems.value))
const restItemsWithAttribution = computed(() => withAttribution(restItems.value))

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

// `silent` suppresses both toasts — used by the auto-start, where the user never asked for
// location right now and a failure should just leave tracking off.
const enableLocation = async (options?: { silent?: boolean }) => {
  const silent = options?.silent === true
  if (!isGeoSupported.value) {
    if (!silent) {
      toast.add({
        title: $t('pages.shoppingLists.nearby.unsupportedTitle'),
        description: $t('pages.shoppingLists.nearby.unsupportedBody'),
        color: 'warning',
        icon: 'i-lucide-map-pin-off'
      })
    }
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
    if (!silent) {
      toast.add({
        title: $t('pages.shoppingLists.nearby.deniedTitle'),
        description: $t('pages.shoppingLists.nearby.deniedBody'),
        color: 'error',
        icon: 'i-lucide-map-pin-off'
      })
    }
  } finally {
    isLocating.value = false
  }
}

/**
 * Starts tracking without any user gesture, but ONLY when the browser reports the
 * geolocation permission as already granted — a 'prompt' or 'denied' state is left alone, so
 * the permission request itself stays tied to the locate button (see useGeolocation).
 */
const maybeAutoStartLocation = async () => {
  if (autoStartAttempted.value || !autoLocate.value || locationTracking.value) return
  if (!isGeoSupported.value) return
  autoStartAttempted.value = true
  if (await getPermissionStatus() !== 'granted') return
  await enableLocation({ silent: true })
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

// Switching the preference back on starts tracking right away when it can (same
// already-granted rule), so the toggle isn't a "takes effect next visit" setting.
const toggleAutoLocate = () => {
  autoLocate.value = !autoLocate.value
  if (autoLocate.value && !locationTracking.value && listLocations.value.length > 0) {
    autoStartAttempted.value = false
    maybeAutoStartLocation()
  }
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

// --- Optimistic delete/purchase/restore -------------------------------------
// ShoppingListItemCard owns the confirm-drawer UX and the request shape (and emits once the user
// has confirmed); this page owns currentListDetails.items, so it is what apply()/revert() mutate
// and what commit() eventually calls the REST API with, deferred behind the undo window (see
// useUndoableAction.ts). Nothing here awaits a network response — the row/toggle updates the
// instant the user confirms, exactly the round-trip this task exists to remove.

// NOTE on all three handlers below: apply/revert deliberately re-read `currentListDetails.value`
// fresh on every invocation rather than closing over the array once. currentListDetails.value can
// be replaced wholesale — a list switch, a showPurchased toggle, or handleSocketReconnected's
// resync — while an action is still pending; closing over the old array would silently mutate a
// detached snapshot the page no longer renders instead of the live one.

const handleDeleteRequested = (item: ShoppingListItemInfo): void => {
  if (!currentListDetails.value) return
  const index = currentListDetails.value.items.findIndex(i => i.publicId === item.publicId)
  if (index < 0) return

  // Splicing at a captured index only makes sense against the same list it was captured from — if
  // the user has since switched the open list (or it was reloaded) before the undo window closes,
  // currentListDetails.value.items is a different list's array and must not be spliced into.
  const belongsToOpenList = () => currentListDetails.value?.publicId === item.shoppingListPublicId

  run({
    entityIds: [item.publicId],
    kind: 'delete',
    label: $t('undo.item.delete', { name: getDisplayName(item) }),
    // Capture the index now: revert must restore the row to its original position, not append it
    // to the end, which would be a visible bug on this urgency-then-alphabetical list.
    apply: () => {
      if (!belongsToOpenList()) return
      currentListDetails.value?.items.splice(index, 1)
      clearAttribution(item.publicId)
    },
    revert: (ownedEntityIds) => {
      if (!belongsToOpenList() || !ownedEntityIds.includes(item.publicId)) return
      currentListDetails.value?.items.splice(index, 0, item)
    },
    commit: () => deleteShoppingListItem(item.publicId)
  })
}

const handlePurchaseRequested = (item: ShoppingListItemInfo, request: PurchaseShoppingListItemRequest): void => {
  if (!currentListDetails.value) return
  const originalPurchasedAt = item.purchasedAt

  run({
    entityIds: [item.publicId],
    kind: 'purchase',
    label: $t('undo.item.purchase', { name: getDisplayName(item) }),
    // Re-locate by id rather than trusting a captured index: a same-entity replacement (e.g. a
    // rapid purchase-then-restore double-tap) can run this apply/revert more than once, and other
    // items may have been deleted (or the whole array replaced) in between.
    apply: () => {
      const items = currentListDetails.value?.items
      const idx = items?.findIndex(i => i.publicId === item.publicId) ?? -1
      if (items && idx >= 0) items[idx] = { ...items[idx]!, purchasedAt: request.purchasedAt }
    },
    revert: (ownedEntityIds) => {
      if (!ownedEntityIds.includes(item.publicId)) return
      const items = currentListDetails.value?.items
      const idx = items?.findIndex(i => i.publicId === item.publicId) ?? -1
      if (items && idx >= 0) items[idx] = { ...items[idx]!, purchasedAt: originalPurchasedAt }
    },
    commit: () => purchaseShoppingListItem(request)
  })
}

const handleRestoreRequested = (item: ShoppingListItemInfo): void => {
  if (!currentListDetails.value) return
  const originalPurchasedAt = item.purchasedAt

  run({
    entityIds: [item.publicId],
    // Shares the 'purchase' kind with handlePurchaseRequested — the queue only knows three kinds
    // (see undoQueue.ts), and restoring is the same toggle in the other direction.
    kind: 'purchase',
    label: $t('undo.item.restore', { name: getDisplayName(item) }),
    apply: () => {
      const items = currentListDetails.value?.items
      const idx = items?.findIndex(i => i.publicId === item.publicId) ?? -1
      if (items && idx >= 0) items[idx] = { ...items[idx]!, purchasedAt: undefined }
    },
    revert: (ownedEntityIds) => {
      if (!ownedEntityIds.includes(item.publicId)) return
      const items = currentListDetails.value?.items
      const idx = items?.findIndex(i => i.publicId === item.publicId) ?? -1
      if (items && idx >= 0) items[idx] = { ...items[idx]!, purchasedAt: originalPurchasedAt }
    },
    commit: () => restorePurchaseShoppingListItem(item.publicId)
  })
}

// --- Realtime handlers: mutate the open list in place instead of refetching. ---
// ItemUpserted/ItemDeleted now arrive as { item, actorPublicId } / { publicId,
// shoppingListPublicId, actorPublicId } (Homassy.API.Hubs.ShoppingListRealtime) rather than the
// bare item/id the client used to read — every handler below destructures the new shape.
const handleRealtimeItemUpserted = ({ item, actorPublicId }: ItemUpsertedEvent) => {
  // A local optimistic change (delete/purchase/restore) is still pending for this item — an echo
  // of the pre-change server state must not fight it. The commit's own resolution (and whatever
  // the server broadcasts because of it) is what reconciles once the window closes.
  if (isPendingEntity(item.publicId)) return
  if (!currentListDetails.value || item.shoppingListPublicId !== selectedListId.value) return
  const items = currentListDetails.value.items
  const index = items.findIndex(i => i.publicId === item.publicId)
  if (index >= 0) items[index] = item
  else items.push(item)
  // Nudge the bottom-nav deadline badge to recount.
  emitBusEvent('shopping-list-item:updated')
  attributeChange(item.publicId, actorPublicId)
}

const handleRealtimeItemDeleted = ({ publicId, shoppingListPublicId }: ItemDeletedEvent) => {
  // See handleRealtimeItemUpserted above — a pending optimistic change on this item wins.
  if (isPendingEntity(publicId)) return
  if (!currentListDetails.value || shoppingListPublicId !== selectedListId.value) return
  currentListDetails.value.items = currentListDetails.value.items.filter(i => i.publicId !== publicId)
  emitBusEvent('shopping-list-item:deleted')
  // The card itself is about to leave via the bubble transition — nothing left to flash.
  clearAttribution(publicId)
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

const handleSocketReconnected = async () => {
  // Re-sync the snapshot after a dropped connection (this also re-joins the group). Diff the
  // incoming items against what was on screen right before the refetch (by public id) so a row
  // that actually changed while disconnected gets a neutral "updated" flash instead of silently
  // swapping in — a full-payload comparison rather than tracking individual fields, since
  // ShoppingListItemInfo carries no version/updatedAt to diff more cheaply, and a list's item count
  // is small enough that this is cheap regardless.
  if (!selectedListId.value) return

  const before = new Map((currentListDetails.value?.items ?? []).map(item => [item.publicId, JSON.stringify(item)]))
  await loadListDetails(selectedListId.value)

  for (const item of currentListDetails.value?.items ?? []) {
    if (before.get(item.publicId) !== JSON.stringify(item)) flashUpdatedRow(item.publicId)
  }
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

// Persist the (list-independent) filter settings.
const persistFilters = () => {
  localStorage.setItem(FILTERS_KEY, JSON.stringify({
    deadline: deadlineFilter.value,
    autoLocate: autoLocate.value
  }))
}

watch([deadlineFilter, autoLocate], persistFilters)

// Auto-start once the open list actually has location-bound items — the same condition that
// shows the locate button, so tracking never runs behind a hidden toggle.
watch(() => listLocations.value.length > 0, (hasLocationItems) => {
  if (hasLocationItems) maybeAutoStartLocation()
}, { immediate: true })

// Walking out of every store's radius retires the "buy here" filter instead of leaving the
// list filtered down to nothing.
watch(isAtAnyStore, (atStore) => {
  if (!atStore && locationFilter.value === 'here') locationFilter.value = 'all'
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

  // Restore the persisted filter settings (whitelist-validated)
  const savedFilters = localStorage.getItem(FILTERS_KEY)
  if (savedFilters) {
    try {
      const parsed = JSON.parse(savedFilters)
      if (['all', 'overdue', 'dueSoon'].includes(parsed.deadline)) {
        deadlineFilter.value = parsed.deadline
      }
      if (typeof parsed.autoLocate === 'boolean') {
        autoLocate.value = parsed.autoLocate
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

  // Drop any pending attribution-flash / "updated"-flash timeouts so none fire after this page is gone.
  attributionTimers.forEach(timer => clearTimeout(timer))
  attributionTimers.clear()
  rowUpdateTimers.forEach(timer => clearTimeout(timer))
  rowUpdateTimers.clear()
})
</script>
