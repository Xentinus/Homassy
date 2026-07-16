<template>
  <div>
    <!-- Sticky search + filters sub-bar (page identity lives in the persistent AppHeader) -->
    <div
      class="sticky z-30 -mx-4 sm:-mx-6 lg:-mx-8 px-6 sm:px-10 lg:px-16 py-3 mb-4 border-b border-gray-200 dark:border-gray-800 bg-default/95 backdrop-blur"
      :style="{ top: 'var(--app-header-height, 5.5rem)' }"
    >
      <!-- Search row (always visible) + filters trigger -->
      <div class="space-y-2">
        <div class="flex gap-2">
          <UFieldGroup size="md" orientation="horizontal" class="flex-1">
            <UInput
              v-model="searchQuery"
              class="flex-1"
              type="text"
              :placeholder="$t('common.searchPlaceholder')"
            >
              <template #trailing>
                <UButton
                  v-if="searchQuery"
                  icon="i-lucide-x"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :aria-label="$t('common.clear')"
                  @click="searchQuery = ''"
                />
              </template>
            </UInput>
            <BarcodeScannerButton v-if="showCameraButton" @scanned="handleBarcodeScanned" />
          </UFieldGroup>
          <UChip :show="activeFilterCount > 0" :text="activeFilterCount" color="primary" size="2xl">
            <UButton
              icon="i-lucide-sliders-horizontal"
              color="primary"
              size="md"
              :aria-label="$t('pages.products.filters.toggle')"
              :aria-expanded="filtersOpen"
              @click="filtersOpen = true"
            >
              <span class="hidden sm:inline">{{ $t('pages.products.filters.toggle') }}</span>
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
            :aria-label="`${$t('pages.products.filters.removeFilter')}: ${f.label}`"
            @click="f.clear()"
          />
          <UButton
            :label="$t('pages.products.filters.clearAll')"
            size="xs"
            color="neutral"
            variant="ghost"
            class="shrink-0"
            @click="clearAllFilters"
          />
        </div>
      </div>
    </div>

    <!-- Filter drawer (bottom sheet) -->
    <UDrawer v-model:open="filtersOpen" :title="$t('pages.products.filters.toggle')">
      <template #body>
        <div class="space-y-5 pb-2">
          <FilterChipGroup
            v-model="expirationFilter"
            :label="$t('pages.products.filterLabels.expiration')"
            :options="expirationOptions"
          />
          <FilterChipGroup
            v-model="scopeFilter"
            :label="$t('pages.products.filterLabels.scope')"
            :options="scopeOptions"
          />
          <FilterChipGroup
            v-model="eatableFilter"
            :label="$t('pages.products.filterLabels.eatable')"
            :options="eatableOptions"
          />

          <!-- Boolean property toggles -->
          <div role="group" :aria-label="$t('pages.products.filterLabels.properties')">
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
              {{ $t('pages.products.filterLabels.properties') }}
            </p>
            <div class="flex flex-wrap gap-2">
              <UButton
                :label="$t('pages.products.filters.favorites')"
                icon="i-lucide-star"
                size="sm"
                class="rounded-full"
                :color="favoritesFilter === 'favorites' ? 'primary' : 'neutral'"
                :variant="favoritesFilter === 'favorites' ? 'solid' : 'outline'"
                :aria-pressed="favoritesFilter === 'favorites'"
                @click="favoritesFilter = favoritesFilter === 'favorites' ? 'all' : 'favorites'"
              />
              <UButton
                :label="$t('pages.products.filterLabels.barcode')"
                icon="i-lucide-barcode"
                size="sm"
                class="rounded-full"
                :color="barcodeFilter === 'withBarcode' ? 'primary' : 'neutral'"
                :variant="barcodeFilter === 'withBarcode' ? 'solid' : 'outline'"
                :aria-pressed="barcodeFilter === 'withBarcode'"
                @click="barcodeFilter = barcodeFilter === 'withBarcode' ? 'all' : 'withBarcode'"
              />
            </div>
          </div>

          <!-- Stock quantity range -->
          <div>
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.filterLabels.quantity') }}
              </p>
              <span class="text-sm text-gray-500 dark:text-gray-400 tabular-nums">
                {{ quantityRange[0] }} – {{ quantityRange[1] }}
              </span>
            </div>
            <USlider
              v-model="quantityRange"
              :min="0"
              :max="maxStockQuantity"
              :step="1"
              color="primary"
            />
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex items-center gap-2 w-full">
          <UButton
            :label="$t('pages.products.filters.clearAll')"
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
            :label="$t('pages.products.filters.showResults', { count: filteredProducts.length })"
            @click="filtersOpen = false"
          />
        </div>
      </template>
    </UDrawer>

    <!-- Content Section -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6">

    <PullToRefreshIndicator
      :pull-distance="pullDistance"
      :is-pulling="isPulling"
      :is-refreshing="isRefreshing"
      :is-ready="isReady"
    />

    <!-- Loading State -->
    <div v-if="isLoading" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
      <USkeleton v-for="i in 8" :key="i" class="h-36 w-full rounded-lg" />
    </div>

    <!-- No Results -->
    <div v-else-if="filteredProducts.length === 0 && !isLoading" class="text-center py-12">
      <UIcon name="i-lucide-package-search" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-500 dark:text-gray-400">{{ $t('pages.products.noResults') }}</p>
    </div>

    <!-- Products Grid -->
    <div v-else class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
      <DetailedProductCard
        v-for="product in displayedProducts"
        :key="product.publicId"
        :product="product"
        :search-query="searchQuery"
        @select="openOverview"
      />
    </div>

    <!-- Sentinel for intersection observer -->
    <div v-if="hasMoreProducts" ref="sentinelRef" class="w-full min-h-[1px]">
      <!-- Loading skeletons while loading more -->
      <div v-if="loadingMore" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4 mt-4">
        <USkeleton v-for="i in 8" :key="i" class="h-36 w-full rounded-lg" />
      </div>
    </div>
    </div>

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="handleBarcodeScanned" />

    <!-- Add Inventory Wizard (bottom-sheet) -->
    <AddInventoryItemModal v-model:open="isAddInventoryOpen" @created="handleInventoryCreated" />

    <!-- Inventory overview (bottom-sheet), opened by tapping a card -->
    <InventoryOverviewDrawer v-model:open="isOverviewOpen" :product-public-id="overviewProductId" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, onBeforeUnmount } from 'vue'
import { useProductsApi } from '../../composables/api/useProductsApi'
import type {
  InventoryGridProductInfo,
  InventoryUpsertedEvent,
  InventoryDeletedEvent,
  ProductDeletedEvent,
  ProductFavoriteChangedEvent
} from '../../types/product'
import { normalizeForSearch } from '../../utils/stringUtils'
import { useCameraAvailability } from '../../composables/useCameraAvailability'
import { useInventorySocket } from '../../composables/useInventorySocket'
import { useEventBus } from '../../composables/useEventBus'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { getDetailedProducts } = useProductsApi()
const { isExpired: checkIsExpired, isExpiringSoon: checkIsExpiringSoon } = useExpirationCheck()
const { t: $t } = useI18n()
const { showCameraButton } = useCameraAvailability()
const inventorySocket = useInventorySocket()
const eventBus = useEventBus()

// Persistent header (auth layout) — page identity + info popover.
usePageHeader(() => ({
  icon: 'i-lucide-package',
  title: $t('pages.products.title'),
  info: $t('pages.products.description')
}))

// The add-inventory wizard (bottom-sheet) opened from the nav FAB.
const isAddInventoryOpen = ref(false)

// The inventory-overview bottom-sheet, opened by tapping a product card.
const isOverviewOpen = ref(false)
const overviewProductId = ref<string | null>(null)
const openOverview = (publicId: string) => {
  overviewProductId.value = publicId
  isOverviewOpen.value = true
}

// Register the page's add-action(s) on the dynamic nav FAB.
useFabActions(() => [
  {
    label: $t('pages.products.addProductButton'),
    icon: 'i-lucide-plus',
    handler: () => { isAddInventoryOpen.value = true }
  }
])

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(() => loadProducts())

// LocalStorage key for filter settings (single consolidated object)
const FILTERS_KEY = 'productsFilters'

// State
const allProducts = ref<InventoryGridProductInfo[]>([])
const isLoading = ref(false)
const searchQuery = ref('')
const filtersOpen = ref(false)

// Independent, individually-combinable filters (values validated on load/use)
const expirationFilter = ref('all')
const eatableFilter = ref('all')
const favoritesFilter = ref('all')
const barcodeFilter = ref('all')
const scopeFilter = ref('all')
const minQuantity = ref<number | null>(null)
const maxQuantity = ref<number | null>(null)

// Pagination state
const currentPage = ref(1)
const pageSize = 20
const loadingMore = ref(false)
const sentinelRef = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

// Filter dropdown options
const expirationOptions = computed(() => [
  { label: $t('pages.products.filters.all'), value: 'all' },
  { label: $t('pages.products.filters.expired'), value: 'expired' },
  { label: $t('pages.products.filters.expiringSoon'), value: 'expiringSoon' }
])

const eatableOptions = computed(() => [
  { label: $t('pages.products.filters.all'), value: 'all' },
  { label: $t('pages.products.filters.eatable'), value: 'eatable' },
  { label: $t('pages.products.filters.notEatable'), value: 'notEatable' }
])

// Scope filter options (family vs personal)
const scopeOptions = computed(() => [
  { label: $t('pages.products.scopeFilters.all'), value: 'all' },
  { label: $t('pages.products.scopeFilters.family'), value: 'family' },
  { label: $t('pages.products.scopeFilters.personal'), value: 'personal' }
])

// Coerce a value to a non-negative number, or null when empty/invalid
const normalizeQty = (v: unknown): number | null => {
  if (v === '' || v === null || v === undefined) return null
  const n = Number(v)
  return Number.isFinite(n) && n >= 0 ? n : null
}

// Largest single-product total stock — upper bound for the quantity slider
const maxStockQuantity = computed(() => {
  const max = allProducts.value.reduce((m, p) => Math.max(m, totalQuantity(p)), 0)
  return Math.max(1, Math.ceil(max))
})

// Slider range proxy: full range [0, max] means "no quantity filter"
const quantityRange = computed<number[]>({
  get: () => [minQuantity.value ?? 0, maxQuantity.value ?? maxStockQuantity.value],
  set: (range: number[]) => {
    const [lo, hi] = range
    minQuantity.value = lo == null || lo <= 0 ? null : lo
    maxQuantity.value = hi == null || hi >= maxStockQuantity.value ? null : hi
  }
})

// Number of active (non-default) filters, shown as a badge on the filter toggle
const activeFilterCount = computed(() =>
  (expirationFilter.value !== 'all' ? 1 : 0) +
  (eatableFilter.value !== 'all' ? 1 : 0) +
  (favoritesFilter.value !== 'all' ? 1 : 0) +
  (barcodeFilter.value !== 'all' ? 1 : 0) +
  (scopeFilter.value !== 'all' ? 1 : 0) +
  (minQuantity.value != null || maxQuantity.value != null ? 1 : 0)
)

const optLabel = (options: { label: string, value: string }[], value: string) =>
  options.find(o => o.value === value)?.label ?? value

// Active filters as dismissible chips shown under the search box
const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (expirationFilter.value !== 'all') {
    chips.push({ key: 'expiration', label: optLabel(expirationOptions.value, expirationFilter.value), clear: () => { expirationFilter.value = 'all' } })
  }
  if (scopeFilter.value !== 'all') {
    chips.push({ key: 'scope', label: optLabel(scopeOptions.value, scopeFilter.value), clear: () => { scopeFilter.value = 'all' } })
  }
  if (eatableFilter.value !== 'all') {
    chips.push({ key: 'eatable', label: optLabel(eatableOptions.value, eatableFilter.value), clear: () => { eatableFilter.value = 'all' } })
  }
  if (favoritesFilter.value === 'favorites') {
    chips.push({ key: 'favorites', label: $t('pages.products.filters.favorites'), clear: () => { favoritesFilter.value = 'all' } })
  }
  if (barcodeFilter.value === 'withBarcode') {
    chips.push({ key: 'barcode', label: $t('pages.products.filterLabels.barcode'), clear: () => { barcodeFilter.value = 'all' } })
  }
  if (minQuantity.value != null || maxQuantity.value != null) {
    chips.push({
      key: 'quantity',
      label: `${$t('pages.products.filterLabels.quantity')}: ${minQuantity.value ?? 0}–${maxQuantity.value ?? maxStockQuantity.value}`,
      clear: () => { minQuantity.value = null; maxQuantity.value = null }
    })
  }
  return chips
})

const clearAllFilters = () => {
  expirationFilter.value = 'all'
  eatableFilter.value = 'all'
  favoritesFilter.value = 'all'
  barcodeFilter.value = 'all'
  scopeFilter.value = 'all'
  minQuantity.value = null
  maxQuantity.value = null
}

// All filter values as one object — drives persistence and pagination reset
const filterState = computed(() => ({
  expiration: expirationFilter.value,
  eatable: eatableFilter.value,
  favorites: favoritesFilter.value,
  barcode: barcodeFilter.value,
  scope: scopeFilter.value,
  minQuantity: minQuantity.value,
  maxQuantity: maxQuantity.value
}))

// Computed - client-side filtering
const filteredProducts = computed(() => {
  let result = allProducts.value

  // Text search filter
  if (searchQuery.value.trim()) {
    const normalized = normalizeForSearch(searchQuery.value)
    result = result.filter(product =>
      normalizeForSearch(product.name).includes(normalized) ||
      normalizeForSearch(product.brand).includes(normalized) ||
      normalizeForSearch(product.barcode).includes(normalized)
    )
  }

  // Independent filters — all AND-combined

  // Expiration status
  if (expirationFilter.value === 'expired') {
    result = result.filter(product => hasExpiredItems(product))
  } else if (expirationFilter.value === 'expiringSoon') {
    result = result.filter(product => hasExpiringSoonItems(product))
  }

  // Eatable
  if (eatableFilter.value === 'eatable') {
    result = result.filter(product => product.isEatable)
  } else if (eatableFilter.value === 'notEatable') {
    result = result.filter(product => !product.isEatable)
  }

  // Favorites
  if (favoritesFilter.value === 'favorites') {
    result = result.filter(product => product.isFavorite)
  }

  // Barcode
  if (barcodeFilter.value === 'withBarcode') {
    result = result.filter(product => !!product.barcode && product.barcode.trim() !== '')
  }

  // Scope (family vs personal)
  if (scopeFilter.value === 'family') {
    result = result.filter(product => hasFamilyItems(product))
  } else if (scopeFilter.value === 'personal') {
    result = result.filter(product => hasPersonalItems(product))
  }

  // Stock-quantity range (unit-agnostic total; see totalQuantity)
  if (minQuantity.value != null || maxQuantity.value != null) {
    result = result.filter(product => {
      const total = totalQuantity(product)
      if (minQuantity.value != null && total < minQuantity.value) return false
      if (maxQuantity.value != null && total > maxQuantity.value) return false
      return true
    })
  }

  // Sort products by urgency and then alphabetically
  const expiredProducts: InventoryGridProductInfo[] = []
  const expiringSoonProducts: InventoryGridProductInfo[] = []
  const otherProducts: InventoryGridProductInfo[] = []

  result.forEach(product => {
    if (hasExpiredItems(product)) {
      expiredProducts.push(product)
    } else if (hasExpiringSoonItems(product)) {
      expiringSoonProducts.push(product)
    } else {
      otherProducts.push(product)
    }
  })

  // Sort each category alphabetically
  const sortAlphabetically = (a: InventoryGridProductInfo, b: InventoryGridProductInfo) => {
    const nameA = a.name.toLowerCase()
    const nameB = b.name.toLowerCase()
    return nameA.localeCompare(nameB, 'hu')
  }

  expiredProducts.sort(sortAlphabetically)
  expiringSoonProducts.sort(sortAlphabetically)
  otherProducts.sort(sortAlphabetically)

  // Concatenate in priority order
  return [...expiredProducts, ...expiringSoonProducts, ...otherProducts]
})

// Paginated products for display (lazy loading)
const displayedProducts = computed(() => {
  const startIndex = 0
  const endIndex = currentPage.value * pageSize
  return filteredProducts.value.slice(startIndex, endIndex)
})

const hasMoreProducts = computed(() => {
  return displayedProducts.value.length < filteredProducts.value.length
})

// Helper function to check if product has expired items
const hasExpiredItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      return checkIsExpired(item.expirationAt)
    } catch {
      return false
    }
  })
}

// Helper function to check if product has items expiring soon (within 2 weeks)
const hasExpiringSoonItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      return checkIsExpiringSoon(item.expirationAt)
    } catch {
      return false
    }
  })
}

// Scope helpers: a product matches "family" if it has any shared item, "personal"
// if it has any personal item. A product with both kinds matches both scopes.
const hasFamilyItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => item.isSharedWithFamily === true)
}

const hasPersonalItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => item.isSharedWithFamily === false)
}

// Total stock across all items. Items may use different units (pcs, g, l), so this
// sum is unit-agnostic and only approximate when a product mixes units.
const totalQuantity = (product: InventoryGridProductInfo): number => {
  return product.inventoryItems.reduce((sum, item) => sum + item.currentQuantity, 0)
}

// Methods
// Load the grid: prefer the realtime snapshot (only card-needed fields), fall back to REST
// (SSR or socket down), mapping the heavier DTO down to the light grid shape.
const loadProducts = async () => {
  isLoading.value = true
  try {
    const snapshot = await inventorySocket.joinInventory()
    if (snapshot) {
      allProducts.value = snapshot
      return
    }

    const response = await getDetailedProducts({ returnAll: true })
    if (response.success && response.data) {
      allProducts.value = response.data.items.map(p => ({
        publicId: p.publicId,
        name: p.name,
        brand: p.brand,
        barcode: p.barcode,
        isEatable: p.isEatable,
        isFavorite: p.isFavorite,
        inventoryItems: p.inventoryItems.map(i => ({
          publicId: i.publicId,
          productPublicId: p.publicId,
          currentQuantity: i.currentQuantity,
          unit: i.unit,
          expirationAt: i.expirationAt,
          isSharedWithFamily: i.isSharedWithFamily
        }))
      }))
    }
  } finally {
    isLoading.value = false
  }
}

// Reload only if the socket is down — a connected client receives its own change back over the
// socket and patches in place, so no full refetch is needed.
const handleInventoryCreated = () => {
  if (!inventorySocket.isConnected.value) loadProducts()
}

// --- Realtime patch handlers: mutate allProducts in place instead of refetching ---

const handleRealtimeInventoryUpserted = (payload: InventoryUpsertedEvent) => {
  const { product, item } = payload
  const existing = allProducts.value.find(p => p.publicId === product.publicId)
  if (!existing) {
    // A product enters the grid only once it has an in-scope item.
    allProducts.value.push({ ...product, inventoryItems: [item] })
  } else {
    const idx = existing.inventoryItems.findIndex(i => i.publicId === item.publicId)
    if (idx >= 0) existing.inventoryItems.splice(idx, 1, item)
    else existing.inventoryItems.push(item)
  }
  eventBus.emit('inventory:updated')
}

const handleRealtimeInventoryDeleted = (payload: InventoryDeletedEvent) => {
  const product = allProducts.value.find(p => p.publicId === payload.productPublicId)
  if (!product) return
  product.inventoryItems = product.inventoryItems.filter(i => i.publicId !== payload.itemPublicId)
  // A product with no in-scope inventory left is no longer in the grid.
  if (product.inventoryItems.length === 0) {
    allProducts.value = allProducts.value.filter(p => p.publicId !== payload.productPublicId)
  }
  eventBus.emit('inventory:deleted')
}

const handleRealtimeProductUpdated = (product: InventoryGridProductInfo) => {
  const existing = allProducts.value.find(p => p.publicId === product.publicId)
  if (!existing) return // no in-scope inventory → not shown in the grid
  // Catalog fields only — preserve local items and the per-user favorite flag.
  existing.name = product.name
  existing.brand = product.brand
  existing.barcode = product.barcode
  existing.isEatable = product.isEatable
}

const handleRealtimeProductFavoriteChanged = (payload: ProductFavoriteChangedEvent) => {
  const existing = allProducts.value.find(p => p.publicId === payload.publicId)
  if (existing) existing.isFavorite = payload.isFavorite
}

const handleRealtimeProductDeleted = (payload: ProductDeletedEvent) => {
  allProducts.value = allProducts.value.filter(p => p.publicId !== payload.publicId)
  eventBus.emit('product:deleted')
}

const loadMoreProducts = () => {
  if (loadingMore.value || !hasMoreProducts.value) return

  loadingMore.value = true

  // Simulate loading delay for better UX
  setTimeout(() => {
    currentPage.value++
    loadingMore.value = false
  }, 300)
}

// Barcode scanner handler
const handleBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Watch for filter changes to reset pagination
watch(searchQuery, () => {
  currentPage.value = 1
})

// Reset pagination and persist whenever any filter changes
watch(filterState, (state) => {
  currentPage.value = 1
  localStorage.setItem(FILTERS_KEY, JSON.stringify(state))
})

// Watch for sentinel availability and setup observer
watch(sentinelRef, (newSentinel) => {
  // Disconnect previous observer if exists
  if (observer.value) {
    observer.value.disconnect()
  }

  // Setup new observer if sentinel exists
  if (newSentinel) {
    observer.value = new IntersectionObserver(
      (entries) => {
        const [entry] = entries
        if (entry && entry.isIntersecting && hasMoreProducts.value && !loadingMore.value) {
          loadMoreProducts()
        }
      },
      {
        root: null,
        rootMargin: '100px', // Trigger 100px before reaching bottom
        threshold: 0.1
      }
    )

    observer.value.observe(newSentinel)
  }
})

// Lifecycle
onMounted(() => {
  // Restore filter settings from localStorage, validating each field
  const saved = localStorage.getItem(FILTERS_KEY)

  if (saved) {
    try {
      const parsed = JSON.parse(saved)

      if (['all', 'expired', 'expiringSoon'].includes(parsed.expiration)) {
        expirationFilter.value = parsed.expiration
      }
      if (['all', 'eatable', 'notEatable'].includes(parsed.eatable)) {
        eatableFilter.value = parsed.eatable
      }
      if (['all', 'favorites'].includes(parsed.favorites)) {
        favoritesFilter.value = parsed.favorites
      }
      if (['all', 'withBarcode'].includes(parsed.barcode)) {
        barcodeFilter.value = parsed.barcode
      }
      if (['all', 'family', 'personal'].includes(parsed.scope)) {
        scopeFilter.value = parsed.scope
      }
      minQuantity.value = normalizeQty(parsed.minQuantity)
      maxQuantity.value = normalizeQty(parsed.maxQuantity)
    } catch {
      // Ignore malformed stored filters
    }
  }

  loadProducts()

  // Live updates: patch the grid from server pushes instead of refetching.
  inventorySocket.on('InventoryUpserted', handleRealtimeInventoryUpserted)
  inventorySocket.on('InventoryDeleted', handleRealtimeInventoryDeleted)
  inventorySocket.on('ProductUpdated', handleRealtimeProductUpdated)
  inventorySocket.on('ProductFavoriteChanged', handleRealtimeProductFavoriteChanged)
  inventorySocket.on('ProductDeleted', handleRealtimeProductDeleted)
  inventorySocket.onReconnected(loadProducts)
})

// Cleanup on unmount
onBeforeUnmount(() => {
  inventorySocket.off('InventoryUpserted', handleRealtimeInventoryUpserted)
  inventorySocket.off('InventoryDeleted', handleRealtimeInventoryDeleted)
  inventorySocket.off('ProductUpdated', handleRealtimeProductUpdated)
  inventorySocket.off('ProductFavoriteChanged', handleRealtimeProductFavoriteChanged)
  inventorySocket.off('ProductDeleted', handleRealtimeProductDeleted)
  inventorySocket.offReconnected(loadProducts)

  if (observer.value) {
    observer.value.disconnect()
  }
})
</script>
