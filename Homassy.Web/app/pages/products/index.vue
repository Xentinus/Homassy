<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header -->
    <div class="space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-package" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('pages.products.title') }}</h1>
        </div>
        <NuxtLink to="/products/add-product">
          <UButton color="primary" size="sm" trailing-icon="i-lucide-plus">
            Termék hozzáadása
          </UButton>
        </NuxtLink>
      </div>
      <p class="text-gray-600 dark:text-gray-400">{{ $t('pages.products.description') }}</p>
    </div>

    <!-- Search Section (full width input) -->
    <div class="flex flex-col sm:flex-row gap-2">
      <div class="flex-1">
        <UInput
          v-model="searchQuery"
          class="w-full"
          type="text"
          :placeholder="$t('common.searchPlaceholder')"
          trailing-icon="i-lucide-search"
          @keyup.enter="onEnter"
          @blur="onBlur"
        />
      </div>
    </div>

    <!-- Content Section -->
    <div>

    <!-- Loading State -->
    <div v-if="isLoading" class="space-y-3">
      <USkeleton class="h-32 w-full" />
      <USkeleton class="h-32 w-full" />
      <USkeleton class="h-32 w-full" />
    </div>

    <!-- No Results -->
    <div v-else-if="products.length === 0" class="text-center py-12">
      <p class="text-gray-500 dark:text-gray-400">{{ $t('pages.products.noResults') }}</p>
    </div>

    <!-- Products Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <DetailedProductCard v-for="product in products" :key="product.publicId" :product="product" />
    </div>

    <!-- Pagination -->
    <div v-if="totalPages > 1" class="flex flex-col sm:flex-row items-center justify-between gap-4 pt-6 border-t border-gray-200 dark:border-gray-700">
      <p class="text-sm text-gray-600 dark:text-gray-400">
        {{ $t('common.paginationInfo', { current: currentPage, total: totalPages, items: totalCount }) }}
      </p>
      <div class="flex items-center gap-2">
        <UButton
          color="neutral"
          variant="ghost"
          size="sm"
          :disabled="currentPage === 1"
          trailing-icon="i-lucide-chevron-left"
          @click="previousPage"
        >
          {{ $t('common.previous') }}
        </UButton>

        <!-- Page Numbers -->
        <div class="flex gap-1">
          <UButton
            v-for="page in visiblePages"
            :key="page"
            :color="page === currentPage ? 'primary' : 'neutral'"
            :variant="page === currentPage ? 'soft' : 'ghost'"
            size="sm"
            @click="goToPage(page)"
          >
            {{ page }}
          </UButton>
        </div>

        <UButton
          color="neutral"
          variant="ghost"
          size="sm"
          :disabled="currentPage === totalPages"
          trailing-icon="i-lucide-chevron-right"
          @click="nextPage"
        >
          {{ $t('common.next') }}
</UButton>
      </div>
    </div>
  </div>
</div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useProductsApi } from '../../composables/api/useProductsApi'
import type { DetailedProductInfo } from '../../types/product'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { getDetailedProducts } = useProductsApi()

// navigation uses NuxtLink in template

// State
const products = ref<DetailedProductInfo[]>([])
const isLoading = ref(false)
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = ref(12)
const totalCount = ref(0)
const totalPages = ref(0)

// Computed
const visiblePages = computed(() => {
  const pages: number[] = []
  const maxVisible = 5
  let startPage = Math.max(1, currentPage.value - Math.floor(maxVisible / 2))
  const endPage = Math.min(totalPages.value, startPage + maxVisible - 1)

  if (endPage - startPage + 1 < maxVisible) {
    startPage = Math.max(1, endPage - maxVisible + 1)
  }

  for (let i = startPage; i <= endPage; i++) {
    pages.push(i)
  }

  return pages
})

// Methods
const loadProducts = async () => {
  isLoading.value = true
  try {
    const response = await getDetailedProducts({
      pageNumber: currentPage.value,
      pageSize: pageSize.value,
      searchTerm: searchQuery.value || undefined
    })

    if (response.success && response.data) {
      products.value = response.data.items
      totalCount.value = response.data.totalCount
      totalPages.value = response.data.totalPages
      currentPage.value = response.data.pageNumber
      // remember the term we just searched for to avoid duplicate searches
      lastSearchTerm.value = searchQuery.value
    }
  } finally {
    isLoading.value = false
  }
}

// remember last searched term to avoid triggering when unchanged
const lastSearchTerm = ref('')

// Trigger search: resets to first page and loads products. Returns true if a search was executed.
const performFilter = (): boolean => {
  if (searchQuery.value === lastSearchTerm.value) return false
  currentPage.value = 1
  loadProducts()
  return true
}

// Prevent duplicate search when Enter is followed by blur
const lastTriggeredByEnter = ref(false)

const onEnter = () => {
  const didSearch = performFilter()
  if (didSearch) lastTriggeredByEnter.value = true
}

const onBlur = () => {
  if (lastTriggeredByEnter.value) {
    lastTriggeredByEnter.value = false
    return
  }
  performFilter()
}

const goToPage = (page: number) => {
  currentPage.value = page
  loadProducts()
}

const nextPage = () => {
  if (currentPage.value < totalPages.value) {
    currentPage.value++
    loadProducts()
  }
}

const previousPage = () => {
  if (currentPage.value > 1) {
    currentPage.value--
    loadProducts()
  }
}

// Lifecycle
onMounted(() => {
  loadProducts()
})
</script>
