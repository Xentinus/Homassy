<template>
  <AppDrawer
    :open="open"
    :title="product?.name || $t('pages.products.details.overview')"
    icon="i-lucide-package"
    @update:open="(value) => emit('update:open', value)"
  >
    <!-- Loading -->
    <div v-if="isLoading" class="space-y-6">
      <USkeleton class="h-32 w-full" />
      <USkeleton class="h-40 w-full" />
      <USkeleton class="h-40 w-full" />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="text-center py-12">
      <UIcon name="i-lucide-alert-circle" class="h-16 w-16 mx-auto text-red-500 mb-4" />
      <p class="text-lg text-gray-600 dark:text-gray-400">
        {{ $t('pages.products.details.productNotFound') }}
      </p>
    </div>

    <!-- Content -->
    <div v-else-if="product" class="space-y-6">
      <ProductInfoPanel
        :product="product"
        @image-click="isImageOverlayOpen = true"
        @toggle-favorite="handleToggleFavorite"
      />

      <InventoryItemList
        :items="sortedInventoryItems"
        :product-name="product.name"
        @refresh="refreshAll"
      />

      <ProductHistoryList
        :items="history"
        :loading="isLoadingHistory"
      />
    </div>
  </AppDrawer>

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
      class="fixed inset-0 z-[100] bg-black/80 flex items-center justify-center p-4 cursor-pointer"
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
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import type {
  DetailedProductInfo,
  ProductHistoryEventInfo,
  InventoryGridProductInfo,
  InventoryUpsertedEvent,
  InventoryDeletedEvent,
  ProductDeletedEvent,
  ProductFavoriteChangedEvent
} from '../types/product'

interface Props {
  open: boolean
  productPublicId: string | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { t: $t } = useI18n()
const { getProductDetails, getProductHistory, toggleFavorite } = useProductsApi()
const inventorySocket = useInventorySocket()
const toast = useToast()

// State
const product = ref<DetailedProductInfo | null>(null)
const history = ref<ProductHistoryEventInfo[]>([])
const isLoading = ref(false)
const isLoadingHistory = ref(false)
const error = ref(false)
const isImageOverlayOpen = ref(false)

const sortedInventoryItems = computed(() => {
  if (!product.value) return []
  return [...product.value.inventoryItems].sort((a, b) => {
    if (!a.expirationAt && !b.expirationAt) return 0
    if (!a.expirationAt) return 1
    if (!b.expirationAt) return -1
    return new Date(a.expirationAt).getTime() - new Date(b.expirationAt).getTime()
  })
})

// Data loading
const loadProductDetails = async () => {
  const publicId = props.productPublicId
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
    }
  } catch (err) {
    console.error('Failed to load product details:', err)
    error.value = true
  } finally {
    isLoading.value = false
  }
}

const loadHistory = async () => {
  const publicId = props.productPublicId
  if (!publicId) return

  isLoadingHistory.value = true
  try {
    const response = await getProductHistory(publicId)
    history.value = response.success && response.data ? response.data : []
  } catch (err) {
    console.error('Failed to load product history:', err)
    history.value = []
  } finally {
    isLoadingHistory.value = false
  }
}

// Reload product + history together (after an inventory mutation or a realtime event).
const refreshAll = () => {
  loadProductDetails()
  loadHistory()
}

// Load whenever the drawer opens for a product.
watch(() => [props.open, props.productPublicId] as const, ([isOpen, publicId]) => {
  if (isOpen && publicId) {
    loadProductDetails()
    loadHistory()
  } else if (!isOpen) {
    // Reset so a stale product doesn't flash on the next open.
    product.value = null
    history.value = []
    error.value = false
  }
})

// Favorite
const handleToggleFavorite = async () => {
  if (!product.value) return
  try {
    const response = await toggleFavorite(product.value.publicId)
    if (response.success) {
      product.value.isFavorite = !product.value.isFavorite
    }
  } catch (err) {
    console.error('Failed to toggle favorite:', err)
    toast.add({ title: $t('common.error'), color: 'error' })
  }
}

// --- Realtime: keep the open product in sync with changes from other users / automation ---
const isThisProduct = (publicId: string) => props.open && publicId === props.productPublicId

const handleRealtimeInventoryUpserted = (payload: InventoryUpsertedEvent) => {
  if (isThisProduct(payload.product.publicId)) refreshAll()
}

const handleRealtimeInventoryDeleted = (payload: InventoryDeletedEvent) => {
  if (isThisProduct(payload.productPublicId)) refreshAll()
}

const handleRealtimeProductUpdated = (updated: InventoryGridProductInfo) => {
  if (isThisProduct(updated.publicId)) refreshAll()
}

const handleRealtimeProductFavoriteChanged = (payload: ProductFavoriteChangedEvent) => {
  if (isThisProduct(payload.publicId) && product.value) {
    product.value.isFavorite = payload.isFavorite
  }
}

const handleRealtimeProductDeleted = (payload: ProductDeletedEvent) => {
  if (isThisProduct(payload.publicId)) emit('update:open', false)
}

onMounted(() => {
  inventorySocket.ensureConnected()
  inventorySocket.on('InventoryUpserted', handleRealtimeInventoryUpserted)
  inventorySocket.on('InventoryDeleted', handleRealtimeInventoryDeleted)
  inventorySocket.on('ProductUpdated', handleRealtimeProductUpdated)
  inventorySocket.on('ProductFavoriteChanged', handleRealtimeProductFavoriteChanged)
  inventorySocket.on('ProductDeleted', handleRealtimeProductDeleted)
})

onBeforeUnmount(() => {
  inventorySocket.off('InventoryUpserted', handleRealtimeInventoryUpserted)
  inventorySocket.off('InventoryDeleted', handleRealtimeInventoryDeleted)
  inventorySocket.off('ProductUpdated', handleRealtimeProductUpdated)
  inventorySocket.off('ProductFavoriteChanged', handleRealtimeProductFavoriteChanged)
  inventorySocket.off('ProductDeleted', handleRealtimeProductDeleted)
})
</script>
