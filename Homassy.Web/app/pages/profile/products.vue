<template>
  <div>
    <!-- Fixed Header with search + filters -->
    <ListFilterBar
      v-model:search="searchQuery"
      :title="$t('profile.allProducts.title')"
      icon="i-lucide-package"
      back-to="/profile/data"
      :search-placeholder="$t('common.searchPlaceholder')"
      :active-filters="activeFilters"
      :filter-count="activeFilterCount"
      :result-count="filteredProducts.length"
      @clear-all="clearAllFilters"
    >
      <template #search-trailing>
        <BarcodeScannerButton v-if="showCameraButton" @scanned="handleSearchBarcodeScanned" />
      </template>

      <template #filters>
        <!-- Category -->
        <div v-if="categoryFilterOptions.length > 1">
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
            {{ $t('profile.allProducts.filterLabels.category') }}
          </p>
          <USelectMenu
            v-model="categoryFilter"
            :items="categoryFilterOptions"
            value-key="value"
            class="w-full"
          />
        </div>

        <!-- Eatable -->
        <FilterChipGroup
          v-model="eatableFilter"
          :label="$t('profile.allProducts.filterLabels.eatable')"
          :options="eatableOptions"
        />

        <!-- Boolean property toggles -->
        <div role="group" :aria-label="$t('profile.allProducts.filterLabels.properties')">
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
            {{ $t('profile.allProducts.filterLabels.properties') }}
          </p>
          <div class="flex flex-wrap gap-2">
            <UButton
              :label="$t('profile.allProducts.filters.favorites')"
              icon="i-lucide-star"
              size="sm"
              class="rounded-full"
              :color="favoritesFilter ? 'primary' : 'neutral'"
              :variant="favoritesFilter ? 'solid' : 'outline'"
              :aria-pressed="favoritesFilter"
              @click="favoritesFilter = !favoritesFilter"
            />
            <UButton
              :label="$t('profile.allProducts.filters.withBarcode')"
              icon="i-lucide-barcode"
              size="sm"
              class="rounded-full"
              :color="barcodeFilter ? 'primary' : 'neutral'"
              :variant="barcodeFilter ? 'solid' : 'outline'"
              :aria-pressed="barcodeFilter"
              @click="barcodeFilter = !barcodeFilter"
            />
          </div>
        </div>
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
      <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
        <USkeleton v-for="i in 12" :key="i" class="h-48 w-full rounded-lg" />
      </div>
    </template>

    <!-- Empty State -->
    <div v-else-if="filteredProducts.length === 0" class="rounded-lg p-12 text-center">
      <UIcon name="i-lucide-package" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
      <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
        {{ hasActiveQuery ? $t('pages.products.noResults') : 'Nincsenek termékek' }}
      </p>
      <p class="text-gray-600 dark:text-gray-400">
        {{ hasActiveQuery ? 'Próbálj meg más keresési feltételt' : 'Adj hozzá termékeket a kezdéshez' }}
      </p>
    </div>

    <!-- Products Grid -->
    <div v-else class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
      <ProductCard
        v-for="product in filteredProducts"
        :key="product.publicId"
        :product="product"
        :search-query="searchQuery"
        :editable="true"
        @edit="openEditDrawer"
        @updated="loadProducts"
        @deleted="onDeleted"
      />
    </div>
    </div>

  <!-- Create / edit bottom sheet -->
  <ProductFormDrawer
    ref="productDrawer"
    :open="drawerOpen"
    :product="editingProduct"
    @update:open="(v) => drawerOpen = v"
    @saved="onSaved"
  />

  <!-- Barcode Scanner Modal (shared: routes to the open drawer or the search box) -->
  <BarcodeScannerModal :on-barcode-detected="dynamicBarcodeHandler" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useCameraAvailability } from '~/composables/useCameraAvailability'
import type { ProductInfo } from '~/types/product'
import type { MasterDataDeletedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getProducts } = useProductsApi()
const masterDataSocket = useMasterDataSocket()
const { t } = useI18n()
const { showCameraButton } = useCameraAvailability()

// Add-action lives on the dynamic nav FAB instead of an inline header button.
useFabActions(() => [
  {
    label: t('common.add'),
    icon: 'i-lucide-plus',
    handler: () => openCreateDrawer()
  }
])

const { formatProductCategory } = useEnumLabel()

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(() => loadProducts())

const loading = ref(true)
const products = ref<ProductInfo[]>([])
const searchQuery = ref('')

// Filter state
const categoryFilter = ref('all')
const eatableFilter = ref('all')
const favoritesFilter = ref(false)
const barcodeFilter = ref(false)

// Create / edit drawer state
const drawerOpen = ref(false)
const editingProduct = ref<ProductInfo | null>(null)
const productDrawer = ref<{ applyScannedBarcode: (barcode: string) => void } | null>(null)

// Load all products (client-side search + filtering)
async function loadProducts() {
  loading.value = true
  try {
    const response = await getProducts({ returnAll: true })
    products.value = response.data?.items || []
  } catch (error) {
    console.error('Failed to load products:', error)
  } finally {
    loading.value = false
  }
}

// Filter options
const eatableOptions = computed(() => [
  { label: t('common.filters.all'), value: 'all' },
  { label: t('profile.allProducts.filters.eatable'), value: 'eatable' },
  { label: t('profile.allProducts.filters.notEatable'), value: 'notEatable' }
])

// Category options built from the categories actually present on the products
const categoryFilterOptions = computed(() => {
  const seen = new Set<string>()
  for (const product of products.value) {
    if (product.category) seen.add(product.category)
  }
  const options = [...seen]
    .map(value => ({ label: formatProductCategory(value), value }))
    .sort((a, b) => a.label.localeCompare(b.label))
  return [{ label: t('common.filters.all'), value: 'all' }, ...options]
})

// Client-side filtered products
const filteredProducts = computed(() => {
  let result = products.value

  // Text search (name / brand / barcode, accent-insensitive)
  if (searchQuery.value.trim()) {
    const normalized = normalizeForSearch(searchQuery.value)
    result = result.filter(product =>
      normalizeForSearch(product.name).includes(normalized)
      || normalizeForSearch(product.brand).includes(normalized)
      || normalizeForSearch(product.barcode).includes(normalized)
    )
  }

  // Category
  if (categoryFilter.value !== 'all') {
    result = result.filter(product => product.category === categoryFilter.value)
  }

  // Eatable
  if (eatableFilter.value === 'eatable') {
    result = result.filter(product => product.isEatable)
  } else if (eatableFilter.value === 'notEatable') {
    result = result.filter(product => !product.isEatable)
  }

  // Favorites
  if (favoritesFilter.value) {
    result = result.filter(product => product.isFavorite)
  }

  // Has barcode
  if (barcodeFilter.value) {
    result = result.filter(product => !!product.barcode && product.barcode.trim() !== '')
  }

  return result
})

// Active filter chips
const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (categoryFilter.value !== 'all') {
    chips.push({
      key: 'category',
      label: formatProductCategory(categoryFilter.value),
      clear: () => { categoryFilter.value = 'all' }
    })
  }
  if (eatableFilter.value !== 'all') {
    const opt = eatableOptions.value.find(o => o.value === eatableFilter.value)
    chips.push({ key: 'eatable', label: opt?.label ?? '', clear: () => { eatableFilter.value = 'all' } })
  }
  if (favoritesFilter.value) {
    chips.push({ key: 'favorites', label: t('profile.allProducts.filters.favorites'), clear: () => { favoritesFilter.value = false } })
  }
  if (barcodeFilter.value) {
    chips.push({ key: 'barcode', label: t('profile.allProducts.filters.withBarcode'), clear: () => { barcodeFilter.value = false } })
  }
  return chips
})

const activeFilterCount = computed(() => activeFilters.value.length)
const hasActiveQuery = computed(() => !!searchQuery.value.trim() || activeFilterCount.value > 0)

function clearAllFilters() {
  categoryFilter.value = 'all'
  eatableFilter.value = 'all'
  favoritesFilter.value = false
  barcodeFilter.value = false
}

// Search barcode scanner handler
const handleSearchBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// The single BarcodeScannerModal is shared: when the create/edit drawer is open the scan feeds the
// product's barcode (and triggers its OpenFoodFacts lookup), otherwise it fills the search box.
const dynamicBarcodeHandler = (barcode: string) => {
  if (drawerOpen.value) {
    productDrawer.value?.applyScannedBarcode(barcode)
  } else {
    handleSearchBarcodeScanned(barcode)
  }
}

// Create / edit drawer functions
function openCreateDrawer() {
  editingProduct.value = null
  drawerOpen.value = true
}

function openEditDrawer(product: ProductInfo) {
  editingProduct.value = product
  drawerOpen.value = true
}

// Idempotent local patch for instant feedback; the realtime socket delivers the same change to other
// family members. On upsert we preserve the client's own per-user favorite flag (the broadcast is
// family-scoped and must not overwrite another member's favorite state).
function upsertProduct(product: ProductInfo) {
  const idx = products.value.findIndex(p => p.publicId === product.publicId)
  const existing = idx >= 0 ? products.value[idx] : undefined
  if (existing) products.value[idx] = { ...product, isFavorite: existing.isFavorite }
  else products.value.push(product)
}

function removeProduct(publicId: string) {
  products.value = products.value.filter(p => p.publicId !== publicId)
}

function onSaved(product: ProductInfo) {
  // The acting client keeps the favorite it just set (create carries it; edit leaves it untouched).
  const idx = products.value.findIndex(p => p.publicId === product.publicId)
  if (idx >= 0) products.value[idx] = product
  else products.value.push(product)
}

function onDeleted(publicId: string) {
  removeProduct(publicId)
}

function handleUpserted(dto: ProductInfo) {
  upsertProduct(dto)
}

function handleDeleted(payload: MasterDataDeletedEvent) {
  removeProduct(payload.publicId)
}

onMounted(async () => {
  await loadProducts()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('ProductUpserted', handleUpserted)
  masterDataSocket.on('ProductDeleted', handleDeleted)
  masterDataSocket.onReconnected(loadProducts)
})

onBeforeUnmount(() => {
  masterDataSocket.off('ProductUpserted', handleUpserted)
  masterDataSocket.off('ProductDeleted', handleDeleted)
  masterDataSocket.offReconnected(loadProducts)
})
</script>
