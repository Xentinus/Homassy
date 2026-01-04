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
        <div class="relative bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6 space-y-4">
          <div class="flex gap-6">
            <!-- Product Image -->
            <div v-if="product.productPictureBase64" class="flex-shrink-0">
              <img
                :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
                :alt="product.name"
                class="w-24 h-24 md:w-32 md:h-32 object-contain rounded-lg cursor-pointer hover:opacity-90 transition-opacity"
                @click="isImageOverlayOpen = true"
              >
            </div>
            <div v-else class="flex-shrink-0 w-24 h-24 md:w-32 md:h-32 bg-gray-100 dark:bg-gray-700 rounded-lg flex flex-col items-center justify-center">
              <UIcon name="i-lucide-package" class="h-12 w-12 text-gray-400 dark:text-gray-500" />
              <span class="text-xs text-gray-500 dark:text-gray-400 mt-1">
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
              @consumed="loadProductDetails"
              @updated="loadProductDetails"
              @deleted="loadProductDetails"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Edit Product Modal -->
    <UModal :open="isEditProductModalOpen" @update:open="(val) => isEditProductModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.editProductModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.editProductModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Name -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editProductModal.nameLabel') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="editProductForm.name"
              type="text"
              :placeholder="$t('pages.addProduct.form.namePlaceholder')"
              required
              class="w-full"
            />
          </div>

          <!-- Brand -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editProductModal.brandLabel') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="editProductForm.brand"
              type="text"
              :placeholder="$t('pages.addProduct.form.brandPlaceholder')"
              required
              class="w-full"
            />
          </div>

          <!-- Category -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editProductModal.categoryLabel') }}
            </label>
            <UInput
              v-model="editProductForm.category"
              type="text"
              :placeholder="$t('pages.addProduct.form.categoryPlaceholder')"
              class="w-full"
            />
          </div>

          <!-- Barcode -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editProductModal.barcodeLabel') }}
            </label>
            <UFieldGroup size="md" orientation="horizontal" class="w-full">
              <UInput
                v-model="editProductForm.barcode"
                type="text"
                :placeholder="$t('pages.addProduct.form.barcodePlaceholder')"
                class="flex-1"
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
              v-model="editProductForm.isEatable"
              :label="$t('pages.products.details.editProductModal.isEatableLabel')"
            />
          </div>

          <!-- Notes -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editProductModal.notesLabel') }}
            </label>
            <UTextarea
              v-model="editProductForm.notes"
              :rows="3"
              :placeholder="$t('pages.addProduct.form.notesPlaceholder')"
              class="w-full"
            />
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.products.details.editProductModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeEditProductModal"
          />
          <UButton
            :label="$t('pages.products.details.editProductModal.confirm')"
            :loading="isUpdatingProduct"
            @click="handleUpdateProduct"
          />
        </div>
      </template>
    </UModal>

    <!-- Delete Product Modal -->
    <UModal :open="isDeleteProductModalOpen" @update:open="(val) => isDeleteProductModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.deleteProductModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.deleteProductModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Warning -->
          <div class="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
            <p class="text-sm font-medium text-red-600 dark:text-red-400">
              {{ $t('pages.products.details.deleteProductModal.warning') }}
            </p>
          </div>

          <!-- Product Details -->
          <div class="space-y-3">
            <!-- Product Name -->
            <div>
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteProductModal.productName') }}:
              </span>
              <span class="text-sm ml-2">{{ product?.name }}</span>
            </div>

            <!-- Brand -->
            <div>
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteProductModal.brand') }}:
              </span>
              <span class="text-sm ml-2">{{ product?.brand }}</span>
            </div>

            <!-- Category -->
            <div v-if="product?.category">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteProductModal.category') }}:
              </span>
              <span class="text-sm ml-2">{{ product.category }}</span>
            </div>

            <!-- Barcode -->
            <div v-if="product?.barcode">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteProductModal.barcode') }}:
              </span>
              <span class="text-sm ml-2">{{ product.barcode }}</span>
            </div>

            <!-- Inventory Count -->
            <div class="pt-2 border-t border-gray-200 dark:border-gray-700">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteProductModal.inventoryCount') }}:
              </span>
              <span class="text-sm ml-2">{{ product?.inventoryItems.length || 0 }}</span>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.products.details.deleteProductModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeDeleteProductModal"
          />
          <UButton
            :label="$t('pages.products.details.deleteProductModal.confirm')"
            color="error"
            :loading="isDeletingProduct"
            @click="handleDeleteProduct"
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
import type { DetailedProductInfo, UpdateProductRequest } from '../../types/product'
import type { OpenFoodFactsProduct } from '../../types/openFoodFacts'
import { useProductsApi } from '../../composables/api/useProductsApi'
import { useOpenFoodFactsApi } from '../../composables/api/useOpenFoodFactsApi'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const route = useRoute()
const { t: $t } = useI18n()
const { getProductDetails, toggleFavorite, updateProduct, deleteProduct, uploadProductImage, deleteProductImage } = useProductsApi()
const { getProductByBarcode } = useOpenFoodFactsApi()
const toast = useToast()

// State
const product = ref<DetailedProductInfo | null>(null)
const isLoading = ref(false)
const error = ref(false)
const isImageOverlayOpen = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const isUploadingImage = ref(false)
const isDeletingImage = ref(false)
const isImportingImageFromBarcode = ref(false)

// Edit product modal state
const isEditProductModalOpen = ref(false)
const editProductForm = ref<UpdateProductRequest>({
  name: undefined,
  brand: undefined,
  category: undefined,
  barcode: undefined,
  isEatable: undefined,
  notes: undefined
})
const isUpdatingProduct = ref(false)

// OpenFoodFacts state for edit modal
const isOpenFoodFactsModalOpen = ref(false)
const isQueryingBarcode = ref(false)
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)
const isImageLoading = ref(true)

// Delete product modal state
const isDeleteProductModalOpen = ref(false)
const isDeletingProduct = ref(false)

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

const handleFileSelect = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  
  if (!file || !product.value) return
  
  const productPublicId = product.value.publicId
  isUploadingImage.value = true
  
  try {
    // Convert to base64
    const reader = new FileReader()
    reader.onload = async (e) => {
      const base64 = e.target?.result as string
      if (!base64) return
      
      const base64Data = base64.split(',')[1] // Remove data:image/...;base64, prefix
      if (!base64Data) return
      
      const response = await uploadProductImage(productPublicId, {
        productPublicId: productPublicId,
        imageBase64: base64Data
      })
      
      if (response.success) {
        // Immediately update the image without full reload
        if (product.value) {
          product.value.productPictureBase64 = base64Data
        }
      }
    }
    reader.readAsDataURL(file)
  } catch (error) {
    console.error('Failed to upload image:', error)
  } finally {
    isUploadingImage.value = false
    if (fileInput.value) {
      fileInput.value.value = '' // Reset input
    }
  }
}

const handleDeleteProductImage = async () => {
  if (!product.value) return
  
  const productPublicId = product.value.publicId
  isDeletingImage.value = true
  
  try {
    const response = await deleteProductImage(productPublicId)
    
    if (response.success) {
      // Immediately remove the image without full reload
      if (product.value) {
        product.value.productPictureBase64 = undefined
      }
    }
  } catch (error) {
    console.error('Failed to delete image:', error)
  } finally {
    isDeletingImage.value = false
  }
}

const handleImportImageFromBarcode = async () => {
  if (!product.value || !product.value.barcode) return
  
  isImportingImageFromBarcode.value = true
  
  try {
    const response = await getProductByBarcode(product.value.barcode.trim())
    
    if (response.success && response.data && response.data.image_base64) {
      // Remove the "data:image/jpeg;base64," prefix if it exists
      let base64Data = response.data.image_base64
      if (base64Data.includes(',')) {
        base64Data = base64Data.split(',')[1]
      }
      
      // Upload the image
      const uploadResponse = await uploadProductImage(product.value.publicId, {
        productPublicId: product.value.publicId,
        imageBase64: base64Data
      })
      
      if (uploadResponse.success) {
        // Immediately update the image without full reload
        if (product.value) {
          product.value.productPictureBase64 = base64Data
        }
      }
    } else {
      toast.add({
        title: $t('toast.error'),
        description: $t('pages.addProduct.openFoodFacts.noProductError'),
        color: 'error'
      })
    }
  } catch (error) {
    console.error('Failed to import image from barcode:', error)
    toast.add({
      title: $t('toast.error'),
      description: $t('pages.addProduct.openFoodFacts.noProductError'),
      color: 'error'
    })
  } finally {
    isImportingImageFromBarcode.value = false
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

// OpenFoodFacts barcode query handler
const handleBarcodeQuery = async () => {
  // Validate barcode exists
  if (!editProductForm.value.barcode || editProductForm.value.barcode.trim() === '') {
    toast.add({
      title: $t('toast.error'),
      description: $t('pages.addProduct.openFoodFacts.noBarcodeError'),
      color: 'error'
    })
    return
  }

  isQueryingBarcode.value = true
  try {
    const response = await getProductByBarcode(editProductForm.value.barcode.trim())
    
    // Only open modal if request was successful and has data
    if (response.success && response.data) {
      openFoodFactsProduct.value = response.data
      isImageLoading.value = true
      isOpenFoodFactsModalOpen.value = true
    } else {
      // Show error toast
      toast.add({
        title: $t('toast.error'),
        description: $t('pages.addProduct.openFoodFacts.noProductError'),
        color: 'error'
      })
    }
  } catch (error) {
    console.error('OpenFoodFacts query failed:', error)
    toast.add({
      title: $t('toast.error'),
      description: $t('pages.addProduct.openFoodFacts.noProductError'),
      color: 'error'
    })
  } finally {
    isQueryingBarcode.value = false
  }
}

const handleImportProduct = () => {
  if (openFoodFactsProduct.value) {
    // Import product name and brand
    if (openFoodFactsProduct.value.product_name) {
      editProductForm.value.name = openFoodFactsProduct.value.product_name
    }
    if (openFoodFactsProduct.value.brands) {
      editProductForm.value.brand = openFoodFactsProduct.value.brands
    }
  }
  handleCancelImport()
}

const handleCancelImport = () => {
  isOpenFoodFactsModalOpen.value = false
  openFoodFactsProduct.value = null
  isImageLoading.value = true
}

// Edit product modal methods
const openEditProductModal = () => {
  if (!product.value) return

  // Pre-fill form with current values
  editProductForm.value = {
    name: product.value.name,
    brand: product.value.brand,
    category: product.value.category || undefined,
    barcode: product.value.barcode || undefined,
    isEatable: product.value.isEatable,
    notes: undefined // Notes not available in DetailedProductInfo
  }
  isEditProductModalOpen.value = true
}

const closeEditProductModal = () => {
  isEditProductModalOpen.value = false
  editProductForm.value = {
    name: undefined,
    brand: undefined,
    category: undefined,
    barcode: undefined,
    isEatable: undefined,
    notes: undefined
  }
}

const handleUpdateProduct = async () => {
  if (!product.value) return

  isUpdatingProduct.value = true

  try {
    const response = await updateProduct(product.value.publicId, editProductForm.value)

    if (response.success) {
      closeEditProductModal()
      await loadProductDetails() // Refresh product data
    }
  } catch (error) {
    console.error('Failed to update product:', error)
  } finally {
    isUpdatingProduct.value = false
  }
}

// Delete product modal methods
const openDeleteProductModal = () => {
  isDeleteProductModalOpen.value = true
}

const closeDeleteProductModal = () => {
  isDeleteProductModalOpen.value = false
}

const handleDeleteProduct = async () => {
  if (!product.value) return

  isDeletingProduct.value = true

  try {
    const response = await deleteProduct(product.value.publicId)

    if (response.success) {
      closeDeleteProductModal()
      // Navigate back to products list
      await navigateTo('/products')
    }
  } catch (error) {
    console.error('Failed to delete product:', error)
  } finally {
    isDeletingProduct.value = false
  }
}

// Lifecycle
onMounted(() => {
  loadProductDetails()
})
</script>
