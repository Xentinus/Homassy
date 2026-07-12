<template>
  <div>
    <div
      class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 p-3 transition-all duration-200 flex flex-col overflow-hidden card-animate"
      :class="cardBorderClass"
    >
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
          <div v-if="editable" class="absolute top-1 right-1 flex gap-1 opacity-100 sm:opacity-0 sm:group-hover:opacity-100 transition-opacity duration-200">
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
        <div class="flex items-start gap-2 mb-1">
          <h3 
            class="text-sm font-bold break-words flex-1 text-gray-900 dark:text-white"
            v-html="highlightText(product.name, searchQuery)"
          />
          <div class="flex gap-1 flex-shrink-0">
            <UIcon
              v-if="product.isEatable"
              name="i-lucide-utensils"
              class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0"
              :title="$t('common.eatable')"
            />
            <UIcon
              v-if="product.isFavorite"
              name="i-lucide-heart"
              class="h-3.5 w-3.5 text-pink-600 dark:text-pink-400 flex-shrink-0"
              :title="$t('common.favorite')"
            />
          </div>
        </div>
        
        <p 
          v-if="product.brand" 
          class="text-xs text-gray-500 dark:text-gray-400 break-words font-medium line-clamp-1 mb-1"
          v-html="highlightText(product.brand, searchQuery)"
      />
        
        <p v-if="product.category" class="text-xs text-gray-500 dark:text-gray-500 line-clamp-1 mb-1">
          {{ formatProductCategory(product.category) }}
        </p>
        
        <p 
          v-if="product.barcode" 
          class="text-xs text-gray-500 dark:text-gray-500 font-mono line-clamp-1"
          v-html="highlightText(product.barcode, searchQuery)"
        />
      </div>
    </div>

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
            <span class="text-sm ml-2">{{ formatProductCategory(product.category) }}</span>
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

    <!-- Upload Progress Modal -->
    <UploadProgressModal
      :is-open="isUploadProgressOpen"
      :progress="uploadProgress"
      :stage="uploadStage"
      :status="uploadStatus"
      :error-message="uploadErrorMessage"
      @update:is-open="isUploadProgressOpen = $event"
      @cancel="handleCancelUpload"
      @close="handleCloseUploadModal"
    />
  </div>
</template>

<script setup lang="ts">
import type { ProductInfo } from '~/types/product'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useProgressApi } from '~/composables/api/useProgressApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import ImageCropper from '~/components/ImageCropper.vue'
import UploadProgressModal from '~/components/UploadProgressModal.vue'
import imageCompression from 'browser-image-compression'
import { extractBase64 } from '~/composables/useImageCrop'
import { useEnumLabel } from '~/composables/useEnumLabel'

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
  edit: [product: ProductInfo]
  updated: []
  deleted: [publicId: string]
}>()

const { t } = useI18n()
const toast = useToast()
const productsApi = useProductsApi()
const progressApi = useProgressApi()
const openFoodFactsApi = useOpenFoodFactsApi()
const router = useRouter()
const { formatProductCategory } = useEnumLabel()

// Modal states
const isDeleteModalOpen = ref(false)
const isImageOverlayOpen = ref(false)
const isCropperOpen = ref(false)
const isUploadProgressOpen = ref(false)

// Loading states
const isDeleting = ref(false)
const isUploadingImage = ref(false)
const isDeletingImage = ref(false)

// Cropper state
const cropperImageSrc = ref('')
const isImportingImageFromBarcode = ref(false)

// Upload progress state
const currentUploadJobId = ref<string | null>(null)
const uploadProgress = ref(0)
const uploadStage = ref('validating')
const uploadStatus = ref<'inprogress' | 'completed' | 'failed' | 'cancelled'>('inprogress')
const uploadErrorMessage = ref<string | undefined>(undefined)
let stopPolling: (() => void) | null = null

// Refs
const fileInput = ref<HTMLInputElement | null>(null)

// Dynamic border classes based on state
const cardBorderClass = computed(() => {
  if (props.isActive) {
    return 'border-primary-400 dark:border-primary-500'
  }
  if (props.product.isFavorite) {
    return 'border-pink-400 dark:border-pink-500'
  }
  return 'border-gray-200 dark:border-gray-700'
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
      onSelect: () => emit('edit', props.product)
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

    // Convert and upload with progress
    const reader = new FileReader()
    reader.onload = async () => {
      const compressedBase64 = reader.result as string
      const base64Data = extractBase64(compressedBase64)

      await uploadWithProgress(base64Data)
    }
    reader.readAsDataURL(compressed)
  } catch (error) {
    console.error('Failed to upload image:', error)
    isUploadingImage.value = false
  }
}

const uploadWithProgress = async (base64Data: string) => {
  try {
    // Open progress modal
    isUploadProgressOpen.value = true
    uploadProgress.value = 0
    uploadStage.value = 'validating'
    uploadStatus.value = 'inprogress'
    uploadErrorMessage.value = undefined

    // Start async upload
    const response = await productsApi.uploadProductImageWithProgress(props.product.publicId, {
      productPublicId: props.product.publicId,
      imageBase64: base64Data
    })

    if (response.data?.jobId) {
      currentUploadJobId.value = response.data.jobId

      // Start polling for progress
      stopPolling = progressApi.pollProgress(response.data.jobId, (progress) => {
        uploadProgress.value = progress.percentage
        uploadStage.value = progress.stage
        uploadStatus.value = progress.status
        uploadErrorMessage.value = progress.errorMessage

        // If completed or failed, stop polling and update UI
        if (progress.status === 'completed') {
          isUploadingImage.value = false
          emit('updated')
          // Auto-close modal after success
          setTimeout(() => {
            handleCloseUploadModal()
          }, 500)
        } else if (progress.status === 'failed' || progress.status === 'cancelled') {
          isUploadingImage.value = false
        }
      })
    }
  } catch (error) {
    console.error('Failed to start upload:', error)
    isUploadingImage.value = false
    isUploadProgressOpen.value = false
  }
}

const handleCancelUpload = async () => {
  if (currentUploadJobId.value) {
    try {
      await progressApi.cancelJob(currentUploadJobId.value)
      if (stopPolling) {
        stopPolling()
        stopPolling = null
      }
      isUploadingImage.value = false
    } catch (error) {
      console.error('Failed to cancel upload:', error)
    }
  }
}

const handleCloseUploadModal = () => {
  if (stopPolling) {
    stopPolling()
    stopPolling = null
  }
  isUploadProgressOpen.value = false
  currentUploadJobId.value = null
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
    emit('deleted', props.product.publicId)
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

<style scoped>
@keyframes slideInUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.card-animate {
  animation: slideInUp 0.4s ease-out;
}
</style>
