<template>
  <div>
    <!-- Fixed Header with Back Button -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 border-b border-gray-200 dark:border-gray-800">
      <div class="flex items-center gap-3">
        <NuxtLink to="/products">
          <UButton
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
          />
        </NuxtLink>
        <UIcon name="i-lucide-package" class="h-6 w-6 text-primary-500" />
        <div>
          <h1 class="text-2xl font-semibold">{{ $t('pages.products.details.overview') }}</h1>
        </div>
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-32 px-4 sm:px-8 lg:px-14 pb-6">
      <!-- Loading State -->
      <div v-if="isLoading" class="space-y-6">
        <USkeleton class="h-64 w-full" />
        <USkeleton class="h-48 w-full" />
        <USkeleton class="h-48 w-full" />
      </div>

      <!-- Error State -->
      <div v-else-if="error" class="text-center py-12">
        <UIcon name="i-lucide-alert-circle" class="h-16 w-16 mx-auto text-red-500 mb-4" />
        <p class="text-lg text-gray-600 dark:text-gray-400">
          {{ $t('pages.products.details.productNotFound') }}
        </p>
      </div>

      <!-- Product Details -->
      <div v-else-if="product" class="space-y-6">
        <!-- Product Info Card -->
        <div class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6 space-y-4">
          <div class="flex gap-6">
            <!-- Product Image -->
            <div v-if="product.productPictureBase64" class="flex-shrink-0">
              <img
                :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
                :alt="product.name"
                class="w-32 h-32 object-cover rounded-lg cursor-pointer hover:opacity-90 transition-opacity"
                @click="isImageOverlayOpen = true"
              >
            </div>
            <div v-else class="flex-shrink-0 w-32 h-32 bg-gray-100 dark:bg-gray-700 rounded-lg flex flex-col items-center justify-center">
              <UIcon name="i-lucide-package" class="h-12 w-12 text-gray-400 dark:text-gray-500" />
              <span class="text-xs text-gray-500 dark:text-gray-400 mt-2">
                {{ $t('pages.products.details.noImage') }}
              </span>
            </div>

            <!-- Product Details -->
            <div class="flex-1 space-y-3">
              <div>
                <h2 class="text-2xl font-bold">{{ product.name }}</h2>
                <p class="text-lg text-gray-600 dark:text-gray-400">{{ product.brand }}</p>
                
                <!-- Status Icons -->
                <div class="flex items-center gap-2 mt-2">
                  <UIcon
                    v-if="product.isEatable"
                    name="i-lucide-utensils"
                    class="h-5 w-5 text-primary-500"
                  />
                  <UIcon
                    :name="product.isFavorite ? 'i-lucide-heart' : 'i-lucide-heart-plus'"
                    class="h-5 w-5 text-primary-500 cursor-pointer hover:opacity-80 transition-opacity"
                    @click="handleToggleFavorite"
                  />
                </div>
              </div>

              <div class="flex flex-wrap gap-4 text-sm">
                <div v-if="product.category" class="flex items-center gap-2">
                  <UIcon name="i-lucide-tag" class="text-gray-500" />
                  <span>{{ product.category }}</span>
                </div>
                <div v-if="product.barcode" class="flex items-center gap-2">
                  <UIcon name="i-lucide-barcode" class="text-gray-500" />
                  <span>{{ product.barcode }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Inventory Items Section -->
        <div class="space-y-4">
          <h3 class="text-xl font-semibold flex items-center gap-2">
            <UIcon name="i-lucide-package-2" class="text-primary-500" />
            {{ $t('pages.products.details.inventoryHeader') }}
          </h3>

          <div v-if="product.inventoryItems.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
            {{ $t('pages.products.details.noInventoryItems') }}
          </div>

          <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
            <InventoryItemDetail
              v-for="item in product.inventoryItems"
              :key="item.publicId"
              :item="item"
              :product-name="product.name"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Image Overlay -->
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-200 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="isImageOverlayOpen && product?.productPictureBase64"
        class="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4 cursor-pointer"
        @click="isImageOverlayOpen = false"
        @keydown.esc="isImageOverlayOpen = false"
      >
        <img
          :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
          :alt="product.name"
          class="max-w-full max-h-full object-contain"
        >
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import type { DetailedProductInfo } from '../../types/product'
import { useProductsApi } from '../../composables/api/useProductsApi'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const route = useRoute()
const { t: $t } = useI18n()
const { getProductDetails, toggleFavorite } = useProductsApi()
const toast = useToast()

// State
const product = ref<DetailedProductInfo | null>(null)
const isLoading = ref(false)
const error = ref(false)
const isImageOverlayOpen = ref(false)

// Methods
const handleToggleFavorite = async () => {
  if (!product.value) return
  
  try {
    const response = await toggleFavorite(product.value.publicId)
    if (response.success) {
      // Toggle the local state
      product.value.isFavorite = !product.value.isFavorite
    }
  } catch (err) {
    console.error('Failed to toggle favorite:', err)
    toast.add({
      title: $t('common.error'),
      color: 'error'
    })
  }
}

const loadProductDetails = async () => {
  const publicId = route.params.publicId as string

  if (!publicId) {
    error.value = true
    return
  }

  isLoading.value = true
  error.value = false

  try {
    const response = await getProductDetails(publicId)

    if (response.success && response.data) {
      product.value = response.data
    } else {
      error.value = true
      // Redirect to products list after showing error
      setTimeout(() => {
        navigateTo('/products')
      }, 2000)
    }
  } catch (err) {
    console.error('Failed to load product details:', err)
    error.value = true
    toast.add({
      title: $t('pages.products.details.productNotFound'),
      color: 'error'
    })
    // Redirect to products list
    setTimeout(() => {
      navigateTo('/products')
    }, 2000)
  } finally {
    isLoading.value = false
  }
}

// Lifecycle
onMounted(() => {
  loadProductDetails()
})
</script>
