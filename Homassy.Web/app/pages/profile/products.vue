<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <NuxtLink to="/profile">
            <UButton
              icon="i-lucide-arrow-left"
              color="neutral"
              variant="ghost"
            />
          </NuxtLink>
          <UIcon name="i-lucide-package" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('profile.allProducts.title') }}</h1>
        </div>
        <UButton 
          color="primary" 
          size="sm" 
          trailing-icon="i-lucide-plus"
          @click="openCreateModal"
        >
          {{ $t('common.add') }}
        </UButton>
      </div>
      
      <!-- Search Input -->
      <div class="w-full">
        <UFieldGroup size="md" orientation="horizontal" class="w-full">
          <UInput
            v-model="searchQuery"
            trailing-icon="i-lucide-search"
            :placeholder="$t('common.searchPlaceholder')"
            class="flex-1"
            @update:model-value="handleSearch"
          />
          <BarcodeScannerButton v-if="showCameraButton" @scanned="handleSearchBarcodeScanned" />
        </UFieldGroup>
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-40 px-4 sm:px-8 lg:px-14 pb-6">

    <!-- Loading State -->
    <template v-if="loading">
      <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
        <USkeleton v-for="i in 12" :key="i" class="h-48 w-full rounded-lg" />
      </div>
    </template>

    <!-- Empty State -->
    <div v-else-if="products.length === 0" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-12 text-center">
      <UIcon name="i-lucide-package" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
      <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
        {{ searchQuery ? $t('pages.products.noResults') : 'Nincsenek termékek' }}
      </p>
      <p class="text-gray-600 dark:text-gray-400">
        {{ searchQuery ? 'Próbálj meg más keresési feltételt' : 'Adj hozzá termékeket a kezdéshez' }}
      </p>
    </div>

    <!-- Products Grid -->
    <div v-else class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
      <ProductCard
        v-for="product in products"
        :key="product.publicId"
        :product="product"
        :search-query="searchQuery"
        :editable="true"
        @updated="reloadCurrentPages"
        @deleted="reloadCurrentPages"
      />
    </div>

    <!-- Sentinel for intersection observer -->
    <div v-if="hasMoreProducts" ref="sentinelRef" class="w-full py-8">
      <!-- Loading skeletons while loading more -->
      <div v-if="loadingMore" class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
        <USkeleton v-for="i in 6" :key="i" class="h-48 w-full rounded-lg" />
      </div>
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
          <UInput
            v-model="createForm.category"
            type="text"
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
import { ref, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import { useCameraAvailability } from '~/composables/useCameraAvailability'
import type { ProductInfo } from '~/types/product'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getProducts, createProduct } = useProductsApi()
const openFoodFactsApi = useOpenFoodFactsApi()
const { t } = useI18n()
const toast = useToast()
const { showCameraButton } = useCameraAvailability()

const loading = ref(true)
const loadingMore = ref(false)
const products = ref<ProductInfo[]>([])
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = 24
const hasMoreProducts = ref(true)

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
  category: string
  barcode: string
  isEatable: boolean
  isFavorite: boolean
  notes: string
}>({
  name: '',
  brand: '',
  category: '',
  barcode: '',
  isEatable: false,
  isFavorite: false,
  notes: ''
})

// Intersection observer
const sentinelRef = ref<HTMLElement | null>(null)
let observer: IntersectionObserver | null = null

// Search debounce
let searchTimeout: NodeJS.Timeout | null = null

// Load products
async function loadProducts(reset = false) {
  if (reset) {
    loading.value = true
    currentPage.value = 1
    products.value = []
    hasMoreProducts.value = true
  } else {
    loadingMore.value = true
  }

  try {
    const response = await getProducts({
      pageNumber: currentPage.value,
      pageSize,
      searchText: searchQuery.value.trim() || undefined
    })
    
    const newProducts = response.data?.items || []
    
    if (reset) {
      products.value = newProducts
    } else {
      products.value.push(...newProducts)
    }

    // Check if there are more products
    const totalItems = response.data?.totalCount || 0
    hasMoreProducts.value = products.value.length < totalItems
  } catch (error) {
    console.error('Failed to load products:', error)
  } finally {
    loading.value = false
    loadingMore.value = false
  }
}

// Reload all pages up to current page
async function reloadCurrentPages() {
  const pagesToLoad = currentPage.value
  loading.value = true
  products.value = []
  
  try {
    // Load all pages from 1 to current
    for (let page = 1; page <= pagesToLoad; page++) {
      const response = await getProducts({
        pageNumber: page,
        pageSize,
        searchText: searchQuery.value.trim() || undefined
      })
      
      const newProducts = response.data?.items || []
      products.value.push(...newProducts)
      
      // Update hasMoreProducts on last page
      if (page === pagesToLoad) {
        const totalItems = response.data?.totalCount || 0
        hasMoreProducts.value = products.value.length < totalItems
      }
    }
  } catch (error) {
    console.error('Failed to reload products:', error)
  } finally {
    loading.value = false
  }
}

// Load more products (pagination)
async function loadMoreProducts() {
  if (loadingMore.value || !hasMoreProducts.value) return
  
  currentPage.value++
  await loadProducts(false)
}

// Handle search with debounce
function handleSearch() {
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  
  searchTimeout = setTimeout(() => {
    loadProducts(true)
  }, 300)
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
  handleSearch()
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
    await reloadCurrentPages()
    isCreating.value = false
  }
}

// Setup intersection observer for infinite scroll
function setupIntersectionObserver() {
  observer = new IntersectionObserver(
    (entries) => {
      const [entry] = entries
      if (entry.isIntersecting && hasMoreProducts.value && !loadingMore.value) {
        loadMoreProducts()
      }
    },
    {
      root: null,
      rootMargin: '200px',
      threshold: 0.1
    }
  )

  if (sentinelRef.value) {
    observer.observe(sentinelRef.value)
  }
}

onMounted(async () => {
  await loadProducts(true)
  await nextTick()
  setupIntersectionObserver()
})

onBeforeUnmount(() => {
  if (observer) {
    observer.disconnect()
  }
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
})
</script>
