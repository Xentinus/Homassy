<template>
  <div>
    <div
      class="rounded-lg border-2 transition-all duration-200 h-full flex flex-col"
      :class="[
        isActive
          ? 'border-primary-500 bg-primary-50 dark:bg-primary-900/10'
          : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'
      ]"
    >
      <div class="p-3 flex flex-col h-full">
        <!-- Product Image -->
        <div class="aspect-square bg-gray-100 dark:bg-gray-800 rounded-md mb-2 overflow-hidden flex items-center justify-center relative">
          <img
            v-if="product.productPictureBase64"
            :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
            :alt="product.name"
            :class="[
              'w-full h-full object-contain transition-opacity',
              editable ? 'cursor-pointer hover:opacity-90' : ''
            ]"
            @click.stop="editable && (isImageOverlayOpen = true)"
          />
          <UIcon v-else name="i-lucide-package" class="h-12 w-12 text-gray-400" />
          
          <!-- Hidden file input -->
          <input
            ref="fileInput"
            type="file"
            accept="image/*"
            class="hidden"
            @change="handleFileSelect"
          >
          
          <!-- Action Icons - Top Right -->
          <div v-if="editable" class="absolute top-1 right-1 flex gap-1">
            <!-- Upload Image Button (shown when no image) -->
            <UButton
              v-if="!product.productPictureBase64"
              icon="i-lucide-upload"
              color="primary"
              size="xs"
              :loading="isUploadingImage"
              @click.stop="fileInput?.click()"
            />
            
            <!-- Import from Barcode Button (shown when no image and has barcode) -->
            <UButton
              v-if="!product.productPictureBase64 && product.barcode"
              icon="i-lucide-barcode"
              color="primary"
              size="xs"
              :loading="isImportingImageFromBarcode"
              @click.stop="handleImportImageFromBarcode"
            />
            
            <!-- Delete Image Button (shown when has image) -->
            <UButton
              v-if="product.productPictureBase64"
              icon="i-lucide-trash-2"
              color="error"
              size="xs"
              :loading="isDeletingImage"
              @click.stop="handleDeleteProductImage"
            />
            
            <!-- Dropdown Menu -->
            <UDropdownMenu :items="dropdownItems" size="md">
              <UButton
                icon="i-lucide-ellipsis-vertical"
                size="xs"
                variant="subtle"
                class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm"
                @click.stop
              />
            </UDropdownMenu>
          </div>
        </div>
        
        <!-- Product Info -->
        <div class="flex-1 flex flex-col">
          <div class="flex items-start gap-1 mb-1">
            <h3 
              class="text-sm font-semibold line-clamp-2 flex-1 text-gray-900 dark:text-white"
              v-html="highlightText(product.name, searchQuery)"
            />
            <div class="flex gap-1 flex-shrink-0">
              <UIcon
                v-if="product.isEatable"
                name="i-lucide-utensils"
                class="h-4 w-4 text-primary-500"
                :title="$t('common.eatable')"
              />
              <UIcon
                v-if="product.isFavorite"
                name="i-lucide-heart"
                class="h-4 w-4 text-primary-500"
                :title="$t('common.favorite')"
              />
            </div>
          </div>
          
          <p 
            v-if="product.brand" 
            class="text-xs text-gray-600 dark:text-gray-400 line-clamp-1 mb-1"
            v-html="highlightText(product.brand, searchQuery)"
          />
          
          <p v-if="product.category" class="text-xs text-gray-500 dark:text-gray-500 line-clamp-1 mb-1">
            {{ product.category }}
          </p>
          
          <p 
            v-if="product.barcode" 
            class="text-xs text-gray-500 dark:text-gray-500 font-mono line-clamp-1"
            v-html="highlightText(product.barcode, searchQuery)"
          />
        </div>
      </div>
    </div>

    <!-- Edit Modal -->
    <UModal :open="isEditModalOpen" @update:open="(val) => isEditModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.editProduct') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.editProductModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Name -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.name') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="editForm.name"
              type="text"
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
              v-model="editForm.brand"
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
              v-model="editForm.category"
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
                v-model="editForm.barcode"
                type="text"
                :placeholder="$t('pages.addProduct.form.barcodePlaceholder')"
                inputmode="numeric"
                pattern="[0-9]*"
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
              v-model="editForm.isEatable"
              :label="$t('pages.addProduct.form.isEatableLabel')"
            />
          </div>

          <!-- Notes -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.addProduct.form.notes') }}
            </label>
            <UTextarea
              v-model="editForm.notes"
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
            @click="closeEditModal"
          />
          <UButton
            :label="$t('common.save')"
            :loading="isUpdating"
            @click="handleUpdate"
          />
        </div>
      </template>
    </UModal>

    <!-- Delete Modal -->
    <UModal :open="isDeleteModalOpen" @update:open="(val) => isDeleteModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.deleteProduct') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.deleteProductModal.warning') }}
      </template>

      <template #body>
        <div class="space-y-3">
          <!-- Product Name -->
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('common.name') }}:
            </span>
            <span class="text-sm ml-2">{{ product.name }}</span>
          </div>

          <!-- Brand -->
          <div v-if="product.brand">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.addProduct.form.brand') }}:
            </span>
            <span class="text-sm ml-2">{{ product.brand }}</span>
          </div>

          <!-- Category -->
          <div v-if="product.category">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.addProduct.form.category') }}:
            </span>
            <span class="text-sm ml-2">{{ product.category }}</span>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('common.cancel')"
            color="neutral"
            variant="outline"
            @click="closeDeleteModal"
          />
          <UButton
            :label="$t('common.delete')"
            color="error"
            :loading="isDeleting"
            @click="handleDelete"
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
        v-if="isImageOverlayOpen && product.productPictureBase64"
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

    <!-- Image Cropper Modal -->
    <ImageCropper
      :is-open="isCropperOpen"
      :image-src="cropperImageSrc"
      :default-aspect-ratio="1"
      @close="isCropperOpen = false"
      @cropped="handleCroppedProductImage"
    />
  </div>
</template>

<script setup lang="ts">
import type { ProductInfo } from '~/types/product'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import ImageCropper from '~/components/ImageCropper.vue'
import imageCompression from 'browser-image-compression'
import { extractBase64 } from '~/composables/useImageCrop'

interface Props {
  product: ProductInfo
  isActive?: boolean
  editable?: boolean
  searchQuery?: string
}

const props = withDefaults(defineProps<Props>(), {
  isActive: false,
  editable: false,
  searchQuery: ''
})

const emit = defineEmits<{
  updated: []
  deleted: []
}>()

const { t } = useI18n()
const toast = useToast()
const productsApi = useProductsApi()
const openFoodFactsApi = useOpenFoodFactsApi()
const router = useRouter()

// Modal states
const isEditModalOpen = ref(false)
const isDeleteModalOpen = ref(false)
const isImageOverlayOpen = ref(false)
const isOpenFoodFactsModalOpen = ref(false)
const isCropperOpen = ref(false)

// Loading states
const isUpdating = ref(false)
const isDeleting = ref(false)
const isQueryingBarcode = ref(false)
const isImageLoading = ref(true)
const isUploadingImage = ref(false)
const isDeletingImage = ref(false)

// Cropper state
const cropperImageSrc = ref('')
const isImportingImageFromBarcode = ref(false)

// Refs
const fileInput = ref<HTMLInputElement | null>(null)

// OpenFoodFacts state
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)

// Edit form
const editForm = ref<{
  name: string
  brand: string
  category: string
  barcode: string
  isEatable: boolean
  notes: string
}>({
  name: '',
  brand: '',
  category: '',
  barcode: '',
  isEatable: false,
  notes: ''
})

// Ensure barcode contains only numeric characters
watch(() => editForm.value.barcode, (newValue) => {
  if (newValue) {
    editForm.value.barcode = newValue.replace(/\D/g, '')
  }
})

// Helper function to escape regex special characters
const escapeRegex = (str: string): string => {
  return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

// Helper function to highlight search text
const highlightText = (text: string, query: string): string => {
  if (!query || !text) return text
  
  const normalizedQuery = query.toLowerCase().trim()
  const normalizedText = text.toLowerCase()
  
  if (!normalizedText.includes(normalizedQuery)) return text
  
  const regex = new RegExp(`(${escapeRegex(normalizedQuery)})`, 'gi')
  return text.replace(regex, '<span class="font-bold text-primary-600 dark:text-primary-400 bg-primary-100 dark:bg-primary-900/30 px-1 py-0.5 rounded">$1</span>')
}

// Dropdown menu items
const dropdownItems = computed(() => {
  const items = [
    {
      label: t('common.viewInventory'),
      icon: 'i-lucide-package-open',
      onSelect: navigateToDetails
    },
    {
      label: t('common.edit'),
      icon: 'i-lucide-pencil',
      onSelect: openEditModal
    },
    {
      label: t('common.delete'),
      icon: 'i-lucide-trash-2',
      color: 'error' as const,
      onSelect: openDeleteModal
    }
  ]

  return [items]
})

// Methods
const handleFileSelect = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]

  if (!file) return

  // Read file as base64 and open cropper
  const reader = new FileReader()
  reader.onload = (e) => {
    cropperImageSrc.value = e.target?.result as string
    isCropperOpen.value = true
  }
  reader.readAsDataURL(file)

  // Reset input
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}

const handleCroppedProductImage = async (base64: string) => {
  isCropperOpen.value = false
  isUploadingImage.value = true

  try {
    // Compress
    const blob = await fetch(base64).then(r => r.blob())
    const compressed = await imageCompression(blob as File, {
      maxWidthOrHeight: 500,
      maxSizeMB: 0.5,
      useWebWorker: true
    })

    // Convert and upload
    const reader = new FileReader()
    reader.onload = async () => {
      const compressedBase64 = reader.result as string
      const base64Data = extractBase64(compressedBase64)

      await productsApi.uploadProductImage(props.product.publicId, {
        productPublicId: props.product.publicId,
        imageBase64: base64Data
      })

      emit('updated')
    }
    reader.readAsDataURL(compressed)
  } catch (error) {
    console.error('Failed to upload image:', error)
  } finally {
    isUploadingImage.value = false
  }
}

const handleDeleteProductImage = async () => {
  const productPublicId = props.product.publicId
  isDeletingImage.value = true
  
  try {
    await productsApi.deleteProductImage(productPublicId)
    emit('updated')
  } catch (error) {
    console.error('Failed to delete image:', error)
  } finally {
    isDeletingImage.value = false
  }
}

const handleImportImageFromBarcode = async () => {
  if (!props.product.barcode) return
  
  isImportingImageFromBarcode.value = true
  
  try {
    const response = await openFoodFactsApi.getProductByBarcode(props.product.barcode.trim())
    
    if (response.success && response.data && response.data.image_base64) {
      let base64Data = response.data.image_base64
      if (base64Data.includes(',')) {
        base64Data = base64Data.split(',')[1] || ''
      }
      
      await productsApi.uploadProductImage(props.product.publicId, {
        productPublicId: props.product.publicId,
        imageBase64: base64Data
      })
      
      emit('updated')
    } else {
      toast.add({
        title: t('toast.error'),
        description: t('pages.addProduct.openFoodFacts.noProductError'),
        color: 'error'
      })
    }
  } catch (error) {
    console.error('Failed to import image from barcode:', error)
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.openFoodFacts.noProductError'),
      color: 'error'
    })
  } finally {
    isImportingImageFromBarcode.value = false
  }
}

const navigateToDetails = () => {
  router.push(`/products/${props.product.publicId}`)
}

const handleBarcodeQuery = async () => {
  if (!editForm.value.barcode || editForm.value.barcode.trim() === '') {
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.openFoodFacts.noBarcodeError'),
      color: 'error'
    })
    return
  }

  isQueryingBarcode.value = true
  try {
    const response = await openFoodFactsApi.getProductByBarcode(editForm.value.barcode.trim())
    
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
      editForm.value.name = openFoodFactsProduct.value.product_name
    }
    if (openFoodFactsProduct.value.brands) {
      editForm.value.brand = openFoodFactsProduct.value.brands
    }
  }
  handleCancelImport()
}

const handleCancelImport = () => {
  isOpenFoodFactsModalOpen.value = false
  openFoodFactsProduct.value = null
  isImageLoading.value = true
}

const openEditModal = () => {
  editForm.value = {
    name: props.product.name,
    brand: props.product.brand,
    category: props.product.category || '',
    barcode: props.product.barcode || '',
    isEatable: props.product.isEatable,
    notes: ''
  }
  isEditModalOpen.value = true
}

const closeEditModal = () => {
  isEditModalOpen.value = false
}

const handleUpdate = async () => {
  if (!editForm.value.name.trim()) {
    toast.add({
      title: t('common.error'),
      description: t('pages.products.details.editProductModal.nameRequired'),
      color: 'error'
    })
    return
  }

  if (!editForm.value.brand.trim()) {
    toast.add({
      title: t('common.error'),
      description: t('pages.products.details.editProductModal.brandRequired'),
      color: 'error'
    })
    return
  }

  isUpdating.value = true
  try {
    await productsApi.updateProduct(props.product.publicId, {
      name: editForm.value.name,
      brand: editForm.value.brand,
      category: editForm.value.category || undefined,
      barcode: editForm.value.barcode || undefined,
      isEatable: editForm.value.isEatable,
      notes: editForm.value.notes || undefined
    })

    closeEditModal()
    emit('updated')
  } catch (error) {
    console.error('Failed to update product:', error)
    toast.add({
      title: t('common.error'),
      description: t('pages.products.details.editProductModal.updateFailed'),
      color: 'error'
    })
  } finally {
    isUpdating.value = false
  }
}

const openDeleteModal = () => {
  isDeleteModalOpen.value = true
}

const closeDeleteModal = () => {
  isDeleteModalOpen.value = false
}

const handleDelete = async () => {
  isDeleting.value = true
  try {
    await productsApi.deleteProduct(props.product.publicId)

    closeDeleteModal()
    emit('deleted')
  } catch (error) {
    console.error('Failed to delete product:', error)
    toast.add({
      title: t('common.error'),
      description: t('pages.products.details.deleteProductModal.deleteFailed'),
      color: 'error'
    })
  } finally {
    isDeleting.value = false
  }
}
</script>
