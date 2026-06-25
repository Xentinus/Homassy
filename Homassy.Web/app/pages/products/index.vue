<template>
  <div>
    <!-- Sticky Header with Search -->
    <div ref="headerRef" class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-4 border-b border-gray-200 dark:border-gray-800 space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-package" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('pages.products.title') }}</h1>
        </div>
      </div>
      <p class="text-gray-600 dark:text-gray-400">{{ $t('pages.products.description') }}</p>

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

    <!-- Content Section (offset by the fixed header's measured height) -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6" :style="{ paddingTop: headerHeight ? `calc(${headerHeight}px + 0.5rem)` : '13rem' }">

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
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, onBeforeUnmount } from 'vue'
import { useProductsApi } from '../../composables/api/useProductsApi'
import type { DetailedProductInfo } from '../../types/product'
import { normalizeForSearch } from '../../utils/stringUtils'
import { useCameraAvailability } from '../../composables/useCameraAvailability'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { getDetailedProducts } = useProductsApi()
const { isExpired: checkIsExpired, isExpiringSoon: checkIsExpiringSoon } = useExpirationCheck()
const { t: $t } = useI18n()
const { showCameraButton } = useCameraAvailability()

// Register the page's add-action(s) on the dynamic nav FAB.
useFabActions(() => [
  {
    label: $t('pages.products.addProductButton'),
    icon: 'i-lucide-plus',
    handler: () => navigateTo('/products/add-product')
  }
])

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(() => loadProducts())

// LocalStorage key for filter settings (single consolidated object)
const FILTERS_KEY = 'productsFilters'

// State
const allProducts = ref<DetailedProductInfo[]>([])
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

// Fixed header: measure its (variable) height so the scrollable content can be offset by it.
const headerRef = ref<HTMLElement | null>(null)
const headerHeight = ref(0)
let headerObserver: ResizeObserver | null = null

const updateHeaderHeight = () => {
  if (headerRef.value) headerHeight.value = headerRef.value.offsetHeight
}

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
  const expiredProducts: DetailedProductInfo[] = []
  const expiringSoonProducts: DetailedProductInfo[] = []
  const otherProducts: DetailedProductInfo[] = []

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
  const sortAlphabetically = (a: DetailedProductInfo, b: DetailedProductInfo) => {
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
const hasExpiredItems = (product: DetailedProductInfo): boolean => {
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
const hasExpiringSoonItems = (product: DetailedProductInfo): boolean => {
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
const hasFamilyItems = (product: DetailedProductInfo): boolean => {
  return product.inventoryItems.some(item => item.isSharedWithFamily === true)
}

const hasPersonalItems = (product: DetailedProductInfo): boolean => {
  return product.inventoryItems.some(item => item.isSharedWithFamily === false)
}

// Total stock across all items. Items may use different units (pcs, g, l), so this
// sum is unit-agnostic and only approximate when a product mixes units.
const totalQuantity = (product: DetailedProductInfo): number => {
  return product.inventoryItems.reduce((sum, item) => sum + item.currentQuantity, 0)
}

// Methods
const loadProducts = async () => {
  isLoading.value = true
  try {
    const response = await getDetailedProducts({ returnAll: true })

    if (response.success && response.data) {
      allProducts.value = response.data.items
    }
  } finally {
    isLoading.value = false
  }
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

  // Track the fixed header's height (changes when the filter chips row appears/disappears).
  updateHeaderHeight()
  if (headerRef.value && typeof ResizeObserver !== 'undefined') {
    headerObserver = new ResizeObserver(() => updateHeaderHeight())
    headerObserver.observe(headerRef.value)
  }
})

// Cleanup on unmount
onBeforeUnmount(() => {
  if (observer.value) {
    observer.value.disconnect()
  }
  if (headerObserver) {
    headerObserver.disconnect()
    headerObserver = null
  }
})
</script>
