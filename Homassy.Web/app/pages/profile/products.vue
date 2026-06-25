<template>
  <div>
    <!-- Fixed Header with search + filters -->
    <ListFilterBar
      v-model:search="searchQuery"
      :title="$t('profile.allProducts.title')"
      icon="i-lucide-package"
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
        @updated="loadProducts"
        @deleted="loadProducts"
      />
    </div>
    </div>

  <!-- Create Product Modal -->
  <UModal :open="isCreateModalOpen" @update:open="(val) => isCreateModalOpen = val" :dismissible="false">
    <template #title>
      {{ $t('pages.addProduct.form.createProduct') }}
    </template>

    <template #description>
      {{ $t('pages.addProduct.form.createProductDescription') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Name -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('common.name') }} <span class="text-red-500">*</span>
          </label>
          <UInput
            v-model="createForm.name"
            type="text"
            :placeholder="$t('pages.addProduct.form.namePlaceholder')"
            class="w-full"
            required
          />
        </div>

        <!-- Brand -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('pages.addProduct.form.brand') }} <span class="text-red-500">*</span>
          </label>
          <UInput
            v-model="createForm.brand"
            type="text"
            :placeholder="$t('pages.addProduct.form.brandPlaceholder')"
            class="w-full"
            required
          />
        </div>

        <!-- Category -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('pages.addProduct.form.category') }}
          </label>
          <USelectMenu
            v-model="createForm.category"
            :items="categoryOptions"
            value-key="value"
            :placeholder="$t('pages.addProduct.form.categoryPlaceholder')"
            class="w-full"
          />
        </div>

        <!-- Barcode -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('pages.addProduct.form.barcode') }}
          </label>
          <UFieldGroup size="md" orientation="horizontal" class="w-full">
            <UInput
              v-model="createForm.barcode"
              type="text"
              :placeholder="$t('pages.addProduct.form.barcodePlaceholder')"
              inputmode="numeric"
              pattern="[0-9]*"
              class="flex-1"
            />
            <BarcodeScannerButton
              v-if="showCameraButton"
              :disabled="isCreating"
              @scanned="handleBarcodeScanned"
            />
            <UButton
              :label="$t('pages.addProduct.form.barcodeQuery')"
              icon="i-lucide-barcode"
              color="primary"
              size="sm"
              :loading="isQueryingBarcode"
              @click="handleBarcodeQuery"
            />
          </UFieldGroup>
        </div>

        <!-- Is Eatable -->
        <div class="flex items-center gap-2">
          <UCheckbox
            v-model="createForm.isEatable"
            :label="$t('pages.addProduct.form.isEatableLabel')"
          />
        </div>

        <!-- Is Favorite -->
        <div class="flex items-center gap-2">
          <UCheckbox
            v-model="createForm.isFavorite"
            :label="$t('pages.addProduct.form.isFavoriteLabel')"
          />
        </div>

        <!-- Notes -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('pages.addProduct.form.notes') }}
          </label>
          <UTextarea
            v-model="createForm.notes"
            :placeholder="$t('pages.addProduct.form.notesPlaceholder')"
            class="w-full"
          />
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="outline"
          @click="closeCreateModal"
        />
        <UButton
          :label="$t('common.save')"
          :loading="isCreating"
          @click="handleCreate"
        />
      </div>
    </template>
  </UModal>

  <!-- OpenFoodFacts Modal -->
  <UModal
    :open="isOpenFoodFactsModalOpen"
    @update:open="(val) => { if (!val) handleCancelImport() }"
    :dismissible="false"
  >
    <template #title>
      {{ $t('pages.addProduct.openFoodFacts.modalTitle') }}
    </template>

    <template #description>
      {{ $t('pages.addProduct.openFoodFacts.modalDescription') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Product Image -->
        <div class="flex justify-center">
          <div class="relative w-40 h-40">
            <USkeleton
              v-if="isImageLoading && openFoodFactsProduct?.image_base64"
              class="w-full h-full rounded-lg"
            />
            <img
              v-if="openFoodFactsProduct?.image_base64"
              :src="openFoodFactsProduct.image_base64"
              alt="Product image"
              class="w-full h-full object-contain rounded-lg border border-gray-200 dark:border-gray-700"
              :class="{ 'opacity-0': isImageLoading }"
              @load="isImageLoading = false"
            >
            <div
              v-else
              class="w-full h-full flex items-center justify-center bg-gray-100 dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700"
            >
              <UIcon name="i-lucide-package" class="h-16 w-16 text-gray-400" />
            </div>
          </div>
        </div>

        <!-- Product Information -->
        <div class="space-y-3">
          <div v-if="openFoodFactsProduct?.product_name">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.addProduct.openFoodFacts.productName') }}:
            </span>
            <p class="text-sm mt-1">{{ openFoodFactsProduct.product_name }}</p>
          </div>

          <div v-if="openFoodFactsProduct?.brands">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.addProduct.openFoodFacts.brands') }}:
            </span>
            <p class="text-sm mt-1">{{ openFoodFactsProduct.brands }}</p>
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('pages.addProduct.openFoodFacts.cancel')"
          color="neutral"
          variant="outline"
          @click="handleCancelImport"
        />
        <UButton
          :label="$t('pages.addProduct.openFoodFacts.import')"
          color="primary"
          @click="handleImportProduct"
        />
      </div>
    </template>
  </UModal>

  <!-- Barcode Scanner Modal -->
  <BarcodeScannerModal :on-barcode-detected="dynamicBarcodeHandler" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import { useSelectValueApi } from '~/composables/api/useSelectValueApi'
import { useCameraAvailability } from '~/composables/useCameraAvailability'
import type { ProductInfo } from '~/types/product'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import type { SelectValue } from '~/types/selectValue'
import { SelectValueType } from '~/types/enums'
import type { ProductCategory } from '~/types/enums'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getProducts, createProduct } = useProductsApi()
const openFoodFactsApi = useOpenFoodFactsApi()
const { getSelectValues } = useSelectValueApi()
const { t } = useI18n()
const toast = useToast()
const { showCameraButton } = useCameraAvailability()

// Add-action lives on the dynamic nav FAB instead of an inline header button.
useFabActions(() => [
  {
    label: t('common.add'),
    icon: 'i-lucide-plus',
    handler: () => openCreateModal()
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

// Create modal state
const isCreateModalOpen = ref(false)
const isCreating = ref(false)
const isOpenFoodFactsModalOpen = ref(false)
const isQueryingBarcode = ref(false)
const isImageLoading = ref(true)

// OpenFoodFacts state
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)

// Create form
const createForm = ref<{
  name: string
  brand: string
  category: ProductCategory | undefined
  barcode: string
  isEatable: boolean
  isFavorite: boolean
  notes: string
}>({
  name: '',
  brand: '',
  category: undefined,
  barcode: '',
  isEatable: false,
  isFavorite: false,
  notes: ''
})

// Category options for dropdown
const categoryOptionsRaw = ref<SelectValue[]>([])
const categoryOptions = computed(() => {
  return categoryOptionsRaw.value.map(cat => ({
    label: t(`enums.productCategory.${cat.text}`),
    value: parseInt(cat.text)
  }))
})

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

// OpenFoodFacts handlers
const handleBarcodeQuery = async () => {
  if (!createForm.value.barcode || createForm.value.barcode.trim() === '') {
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.openFoodFacts.noBarcodeError'),
      color: 'error'
    })
    return
  }

  isQueryingBarcode.value = true
  try {
    const response = await openFoodFactsApi.getProductByBarcode(createForm.value.barcode.trim())
    
    if (response.success && response.data) {
      openFoodFactsProduct.value = response.data
      isImageLoading.value = true
      isOpenFoodFactsModalOpen.value = true
    } else {
      toast.add({
        title: t('toast.error'),
        description: t('pages.addProduct.openFoodFacts.noProductError'),
        color: 'error'
      })
    }
  } catch (error) {
    console.error('OpenFoodFacts query failed:', error)
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.openFoodFacts.noProductError'),
      color: 'error'
    })
  } finally {
    isQueryingBarcode.value = false
  }
}

const handleImportProduct = () => {
  if (openFoodFactsProduct.value) {
    if (openFoodFactsProduct.value.product_name) {
      createForm.value.name = openFoodFactsProduct.value.product_name
    }
    if (openFoodFactsProduct.value.brands) {
      createForm.value.brand = openFoodFactsProduct.value.brands
    }
  }
  handleCancelImport()
}

const handleCancelImport = () => {
  isOpenFoodFactsModalOpen.value = false
  openFoodFactsProduct.value = null
  isImageLoading.value = true
}

// Barcode scanner handler
const handleBarcodeScanned = (barcode: string) => {
  createForm.value.barcode = barcode
  // Auto-trigger OpenFoodFacts query
  nextTick(() => {
    handleBarcodeQuery()
  })
}

// Search barcode scanner handler
const handleSearchBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Dynamic barcode handler - routes to correct handler based on context
const dynamicBarcodeHandler = (barcode: string) => {
  if (isCreateModalOpen.value) {
    // We're in the create modal - use create handler
    handleBarcodeScanned(barcode)
  } else {
    // We're in search mode - use search handler
    handleSearchBarcodeScanned(barcode)
  }
}

// Create modal functions
const openCreateModal = () => {
  createForm.value = {
    name: '',
    brand: '',
    category: '',
    barcode: '',
    isEatable: false,
    isFavorite: false,
    notes: ''
  }
  isCreateModalOpen.value = true
}

const closeCreateModal = () => {
  isCreateModalOpen.value = false
}

const handleCreate = async () => {
  if (!createForm.value.name.trim()) {
    toast.add({
      title: t('common.error'),
      description: t('pages.addProduct.form.nameRequired'),
      color: 'error'
    })
    return
  }

  if (!createForm.value.brand.trim()) {
    toast.add({
      title: t('common.error'),
      description: t('pages.addProduct.form.brandRequired'),
      color: 'error'
    })
    return
  }

  isCreating.value = true
  try {
    await createProduct({
      name: createForm.value.name,
      brand: createForm.value.brand,
      category: createForm.value.category || null,
      barcode: createForm.value.barcode || null,
      isEatable: createForm.value.isEatable,
      isFavorite: createForm.value.isFavorite,
      notes: createForm.value.notes || null
    })

    closeCreateModal()
  } catch (error) {
    console.error('Failed to create product:', error)
  } finally {
    await loadProducts()
    isCreating.value = false
  }
}

onMounted(async () => {
  await loadProducts()
})

onMounted(async () => {
  const response = await getSelectValues(SelectValueType.ProductCategory)
  if (response.success && response.data) {
    categoryOptionsRaw.value = response.data
  }
})
</script>
