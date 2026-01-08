<template>
  <div>
    <!-- Fixed Header with Search -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-package" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('pages.products.title') }}</h1>
        </div>
        <NuxtLink to="/products/add-product">
          <UButton color="primary" size="sm" trailing-icon="i-lucide-plus">
            {{ $t('common.add') }}
          </UButton>
        </NuxtLink>
      </div>
      <p class="text-gray-600 dark:text-gray-400">{{ $t('pages.products.description') }}</p>
      
      <!-- Search and Filter Section -->
      <div class="flex flex-col sm:flex-row gap-2">
        <div class="flex-1">
          <UFieldGroup size="md" orientation="horizontal" class="w-full">
            <UInput
              v-model="searchQuery"
              class="flex-1"
              type="text"
              :placeholder="$t('common.searchPlaceholder')"
              trailing-icon="i-lucide-search"
            />
            <BarcodeScannerButton v-if="showCameraButton" @scanned="handleBarcodeScanned" />
          </UFieldGroup>
        </div>
        <div class="flex-1">
          <USelect
            v-model="filterMode"
            :items="filterOptions"
            class="w-full"
          />
        </div>
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-56 px-4 sm:px-8 lg:px-14 pb-6">

    <!-- Loading State -->
    <div v-if="isLoading" class="space-y-3">
      <USkeleton class="h-32 w-full" />
      <USkeleton class="h-32 w-full" />
      <USkeleton class="h-32 w-full" />
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
    <div v-if="hasMoreProducts" ref="sentinelRef" class="w-full">
      <!-- Loading skeletons while loading more -->
      <div v-if="loadingMore" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4 mt-4">
        <USkeleton class="h-32 w-full" />
        <USkeleton class="h-32 w-full" />
        <USkeleton class="h-32 w-full" />
      </div>
    </div>
    </div>

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="handleBarcodeScanned" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick, onBeforeUnmount } from 'vue'
import { useProductsApi } from '../../composables/api/useProductsApi'
import type { DetailedProductInfo } from '../../types/product'
import { normalizeForSearch } from '../../utils/stringUtils'
import { useCameraAvailability } from '../../composables/useCameraAvailability'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { getDetailedProducts } = useProductsApi()
const { t: $t } = useI18n()
const { showCameraButton } = useCameraAvailability()

// LocalStorage keys for filter settings
const LAST_SEARCH_QUERY_KEY = 'productsLastSearchQuery'
const LAST_FILTER_MODE_KEY = 'productsLastFilterMode'

// State
const allProducts = ref<DetailedProductInfo[]>([])
const isLoading = ref(false)
const searchQuery = ref('')
const filterMode = ref('all')

// Pagination state
const currentPage = ref(1)
const pageSize = 20
const loadingMore = ref(false)
const sentinelRef = ref<HTMLElement | null>(null)

// Filter options for select dropdown
const filterOptions = computed(() => [
  { label: $t('pages.products.filters.all'), value: 'all' },
  { label: $t('pages.products.filters.expired'), value: 'expired' },
  { label: $t('pages.products.filters.expiringSoon'), value: 'expiringSoon' },
  { label: $t('pages.products.filters.favorites'), value: 'favorites' },
  { label: $t('pages.products.filters.eatable'), value: 'eatable' },
  { label: $t('pages.products.filters.withBarcode'), value: 'withBarcode' }
])

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

  // Apply select filter (mutually exclusive)
  switch (filterMode.value) {
    case 'expired':
      result = result.filter(product => hasExpiredItems(product))
      break
    case 'expiringSoon':
      result = result.filter(product => hasExpiringSoonItems(product))
      break
    case 'favorites':
      result = result.filter(product => product.isFavorite)
      break
    case 'eatable':
      result = result.filter(product => product.isEatable)
      break
    case 'withBarcode':
      result = result.filter(product => product.barcode && product.barcode.trim() !== '')
      break
    case 'all':
    default:
      // No additional filtering
      break
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
  const now = new Date()
  return product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      const expirationDate = new Date(item.expirationAt)
      return expirationDate < now
    } catch {
      return false
    }
  })
}

// Helper function to check if product has items expiring soon (within 2 weeks)
const hasExpiringSoonItems = (product: DetailedProductInfo): boolean => {
  const now = new Date()
  const twoWeeksFromNow = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000)

  return product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      const expirationDate = new Date(item.expirationAt)
      return expirationDate >= now && expirationDate <= twoWeeksFromNow
    } catch {
      return false
    }
  })
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

watch(filterMode, () => {
  currentPage.value = 1
})

// Lifecycle
onMounted(() => {
  // Restore filter settings from localStorage
  const savedSearchQuery = localStorage.getItem(LAST_SEARCH_QUERY_KEY)
  const savedFilterMode = localStorage.getItem(LAST_FILTER_MODE_KEY)
  
  if (savedSearchQuery) {
    searchQuery.value = savedSearchQuery
  }
  
  if (savedFilterMode) {
    filterMode.value = savedFilterMode
  }
  
  loadProducts()

  // Setup intersection observer for infinite scroll
  nextTick(() => {
    if (!sentinelRef.value) return

    const observer = new IntersectionObserver(
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

    observer.observe(sentinelRef.value)

    // Cleanup
    onBeforeUnmount(() => {
      observer.disconnect()
    })
  })
})

// Watch for filter changes and save to localStorage
watch(searchQuery, (newValue) => {
  if (newValue) {
    localStorage.setItem(LAST_SEARCH_QUERY_KEY, newValue)
  } else {
    localStorage.removeItem(LAST_SEARCH_QUERY_KEY)
  }
})

watch(filterMode, (newValue) => {
  localStorage.setItem(LAST_FILTER_MODE_KEY, newValue)
})
</script>
