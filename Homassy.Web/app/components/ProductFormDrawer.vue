<template>
  <AppDrawer
    :open="open"
    :title="title"
    icon="i-lucide-package"
    :loading="saving"
    @update:open="(v) => emit('update:open', v)"
  >
    <div class="space-y-6">
        <!-- Product image (edit only — upload needs an existing product) -->
        <div v-if="isEdit" class="flex flex-col items-center gap-3">
          <div class="h-28 w-28 rounded-xl overflow-hidden border border-default">
            <ProductImage :src="imagePreview" :category="form.category" />
          </div>
          <input ref="fileInput" type="file" accept="image/*" class="hidden" @change="handleFileSelect">
          <div class="flex items-center gap-2 flex-wrap justify-center">
            <UButton icon="i-lucide-upload" color="primary" variant="soft" :label="t('profile.changePhoto')" :loading="isUploadingImage" @click="fileInput?.click()" />
            <UButton v-if="!imagePreview && form.barcode" icon="i-lucide-barcode" color="primary" variant="soft" :label="t('pages.addProduct.form.imageFromBarcode')" :loading="isImportingImageFromBarcode" @click="handleImportImageFromBarcode" />
            <UButton v-if="imagePreview" icon="i-lucide-trash-2" color="error" variant="soft" :label="t('profile.removePhoto')" :loading="isDeletingImage" @click="handleDeleteProductImage" />
          </div>
        </div>

        <UForm ref="formRef" :schema="schema" :state="form" class="space-y-4" @submit="onSubmit">
          <UFormField :label="t('pages.addProduct.form.name')" name="name" required>
            <UInput v-model="form.name" :placeholder="t('pages.addProduct.form.namePlaceholder')" :disabled="saving" class="w-full" />
          </UFormField>

          <UFormField :label="t('pages.addProduct.form.brand')" name="brand" required>
            <UInput v-model="form.brand" :placeholder="t('pages.addProduct.form.brandPlaceholder')" :disabled="saving" class="w-full" />
          </UFormField>

          <UFormField :label="t('pages.addProduct.form.category')" name="category">
            <USelectMenu v-model="form.category" :items="categoryOptions" value-key="value" virtualize :placeholder="t('pages.addProduct.form.categoryPlaceholder')" :disabled="saving" class="w-full" />
          </UFormField>

          <UFormField :label="t('pages.addProduct.form.unit')" name="unit" required>
            <USelect v-model="form.unit" :items="unitOptions" :placeholder="t('pages.addProduct.form.unitPlaceholder')" :disabled="saving" class="w-full" />
          </UFormField>

          <UFormField :label="t('pages.addProduct.form.barcode')" name="barcode">
            <UFieldGroup size="md" orientation="horizontal" class="w-full">
              <UInput v-model="form.barcode" :placeholder="t('pages.addProduct.form.barcodePlaceholder')" :disabled="saving" inputmode="numeric" pattern="[0-9]*" class="flex-1" />
              <BarcodeScannerButton v-if="showCameraButton" :disabled="saving" />
              <UButton :label="t('pages.addProduct.form.barcodeQuery')" icon="i-lucide-barcode" color="primary" size="sm" :loading="isQueryingBarcode" :disabled="saving" @click="handleBarcodeQuery" />
            </UFieldGroup>
          </UFormField>

          <UFormField name="isEatable">
            <UCheckbox v-model="form.isEatable" :label="t('pages.addProduct.form.isEatableLabel')" :disabled="saving" />
          </UFormField>

          <UFormField v-if="!isEdit" name="isFavorite">
            <UCheckbox v-model="form.isFavorite" :label="t('pages.addProduct.form.isFavoriteLabel')" :disabled="saving" />
          </UFormField>

          <UFormField :label="t('pages.addProduct.form.notes')" name="notes">
            <UTextarea v-model="form.notes" :placeholder="t('pages.addProduct.form.notesPlaceholder')" :disabled="saving" :rows="3" class="w-full" />
          </UFormField>
        </UForm>
      </div>

    <template #footer>
      <UButton :label="t('common.cancel')" color="neutral" variant="ghost" @click="emit('update:open', false)" />
      <UButton
        :label="t('common.save')"
        color="primary"
        icon="i-lucide-save"
        :loading="saving"
        @click="formRef?.submit()"
      />
    </template>
  </AppDrawer>

  <!-- OpenFoodFacts import preview (name / brand) -->
  <AppDrawer
    :open="isOpenFoodFactsModalOpen"
    :title="t('pages.addProduct.openFoodFacts.modalTitle')"
    icon="i-lucide-download"
    fit="content"
    @update:open="(v) => { if (!v) handleCancelImport() }"
  >
    <p class="text-sm text-muted">{{ t('pages.addProduct.openFoodFacts.modalDescription') }}</p>
    <div class="space-y-4">
      <div class="flex justify-center">
        <div class="relative w-40 h-40">
          <img
            v-if="openFoodFactsProduct?.image_base64"
            :src="openFoodFactsProduct.image_base64"
            alt="Product image"
            class="w-full h-full object-contain rounded-lg border border-gray-200 dark:border-gray-700"
          >
          <div v-else class="w-full h-full flex items-center justify-center bg-gray-100 dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
            <UIcon name="i-lucide-package" class="h-16 w-16 text-gray-400" />
          </div>
        </div>
      </div>
      <div class="space-y-3">
        <div v-if="openFoodFactsProduct?.product_name">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ t('pages.addProduct.openFoodFacts.productName') }}:</span>
          <p class="text-sm mt-1">{{ openFoodFactsProduct.product_name }}</p>
        </div>
        <div v-if="openFoodFactsProduct?.brands">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ t('pages.addProduct.openFoodFacts.brands') }}:</span>
          <p class="text-sm mt-1">{{ openFoodFactsProduct.brands }}</p>
        </div>
      </div>
    </div>

    <template #footer>
      <UButton :label="t('pages.addProduct.openFoodFacts.cancel')" color="neutral" variant="outline" @click="handleCancelImport" />
      <UButton :label="t('pages.addProduct.openFoodFacts.import')" color="primary" @click="handleImportProduct" />
    </template>
  </AppDrawer>

  <!-- Image cropper + upload progress in one drawer (phase-switched) -->
  <ImageCropper
    :is-open="isCropperOpen"
    :image-src="cropperImageSrc"
    :default-aspect-ratio="1"
    :upload-status="uploadStatus"
    :upload-progress="uploadProgress"
    :upload-stage="uploadStage"
    :upload-error-message="uploadErrorMessage"
    @close="isCropperOpen = false"
    @cropped="handleCroppedProductImage"
    @cancel-upload="handleCancelUpload"
    @close-upload="handleCloseUpload"
  />
</template>

<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import type { FormSubmitEvent } from '@nuxt/ui'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useProgressApi } from '~/composables/api/useProgressApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import { useSelectValueApi } from '~/composables/api/useSelectValueApi'
import { useCameraAvailability } from '~/composables/useCameraAvailability'
import ImageCropper from '~/components/ImageCropper.vue'
import imageCompression from 'browser-image-compression'
import { extractBase64 } from '~/composables/useImageCrop'
import { Unit, SelectValueType } from '~/types/enums'
import type { ProductInfo } from '~/types/product'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import type { SelectValue } from '~/types/selectValue'
import { emptyProductForm, type ProductSchema } from '~/composables/useProductFormSchema'

/**
 * Create/edit a product (catalog) in a modern bottom-sheet drawer (UForm + Zod). The product card is
 * image-free — the photo and its management (upload / crop / OpenFoodFacts import / remove) live here,
 * shown when the card is opened. Adds the `unit` field, keeps barcode scan + OFF name/brand import,
 * and never sends notes empty (so existing notes are not clobbered). Owns the API calls.
 *
 * Validation and the request payloads come from `useProductFormSchema`, so this form submits
 * exactly what the inventory and shopping-list product forms submit.
 */
const props = withDefaults(defineProps<{
  open: boolean
  product?: ProductInfo | null
}>(), {
  product: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [product: ProductInfo]
  updated: []
}>()

const { t } = useI18n()
const toast = useToast()
const { createProduct, updateProduct, uploadProductImageWithProgress, uploadProductImage, deleteProductImage } = useProductsApi()
const progressApi = useProgressApi()
const openFoodFactsApi = useOpenFoodFactsApi()
const { getSelectValues } = useSelectValueApi()
const { showCameraButton } = useCameraAvailability()

const isEdit = computed(() => !!props.product)
const title = computed(() => isEdit.value
  ? t('pages.products.details.editProduct')
  : t('pages.addProduct.form.createProduct'))

// Schema and payload mapping are shared with the inventory / shopping-list product forms.
const { productSchema: schema, toCreateProductRequest, toUpdateProductRequest } = useProductFormSchema()
const { toFormErrors } = useApiFormErrors()

const form = ref(emptyProductForm())
const saving = ref(false)
const formRef = ref()

// Category options (from the shared select-value endpoint), loaded once.
const categoryOptionsRaw = ref<SelectValue[]>([])
const { categoryOptions } = useProductCategoryOptions(categoryOptionsRaw)

const unitOptions = computed(() =>
  Object.entries(Unit)
    .filter(([key]) => isNaN(Number(key)))
    .map(([, value]) => ({ label: t(`enums.unit.${value}`), value: value as Unit }))
)

onMounted(async () => {
  const res = await getSelectValues(SelectValueType.ProductCategory)
  if (res.success && res.data) categoryOptionsRaw.value = res.data
})

// Keep the barcode numeric.
watch(() => form.value.barcode, (v) => {
  if (v) form.value.barcode = v.replace(/\D/g, '')
})

// --- Product image -----------------------------------------------------------
// A ready-to-render src, not raw base64: it is the stored image's URL while the drawer is just
// showing the product, and a data URI in the moment between cropping and the upload finishing.
const imagePreview = ref<string | undefined>(undefined)
const fileInput = ref<HTMLInputElement | null>(null)
const isUploadingImage = ref(false)
const isDeletingImage = ref(false)
const isImportingImageFromBarcode = ref(false)
const isCropperOpen = ref(false)
const cropperImageSrc = ref('')
const currentUploadJobId = ref<string | null>(null)
const uploadProgress = ref(0)
const uploadStage = ref('validating')
const uploadStatus = ref<'idle' | 'inprogress' | 'completed' | 'failed' | 'cancelled'>('idle')
const uploadErrorMessage = ref<string | undefined>(undefined)
let stopPolling: (() => void) | null = null

// Seed the form (and image) each time the drawer opens.
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  if (props.product) {
    form.value = {
      name: props.product.name,
      brand: props.product.brand,
      category: props.product.category ?? undefined,
      unit: props.product.unit,
      barcode: props.product.barcode || '',
      isEatable: props.product.isEatable,
      isFavorite: props.product.isFavorite,
      notes: ''
    }
    imagePreview.value = props.product.productImageUrl || undefined
  } else {
    form.value = emptyProductForm()
    imagePreview.value = undefined
  }
})

function handleFileSelect(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = (e) => {
    cropperImageSrc.value = e.target?.result as string
    uploadStatus.value = 'idle'
    isCropperOpen.value = true
  }
  reader.readAsDataURL(file)
  if (fileInput.value) fileInput.value.value = ''
}

async function handleCroppedProductImage(base64: string) {
  if (!props.product) { isCropperOpen.value = false; return }
  // Keep the drawer open; switch it to the upload progress view.
  uploadStatus.value = 'inprogress'
  uploadProgress.value = 0
  uploadStage.value = 'validating'
  uploadErrorMessage.value = undefined
  isUploadingImage.value = true
  try {
    const blob = await fetch(base64).then(r => r.blob())
    const compressed = await imageCompression(blob as File, { maxWidthOrHeight: 500, maxSizeMB: 0.5, useWebWorker: true })
    const reader = new FileReader()
    reader.onload = async () => {
      const base64Data = extractBase64(reader.result as string)
      await uploadWithProgress(base64Data)
    }
    reader.readAsDataURL(compressed)
  } catch (error) {
    console.error('Failed to upload image:', error)
    isUploadingImage.value = false
    uploadStatus.value = 'failed'
  }
}

async function uploadWithProgress(base64Data: string) {
  if (!props.product) return
  try {
    uploadProgress.value = 0
    uploadStage.value = 'validating'
    uploadStatus.value = 'inprogress'
    uploadErrorMessage.value = undefined

    const response = await uploadProductImageWithProgress(props.product.publicId, {
      productPublicId: props.product.publicId,
      imageBase64: base64Data
    })

    if (response.data?.jobId) {
      currentUploadJobId.value = response.data.jobId
      stopPolling = progressApi.pollProgress(response.data.jobId, (progress) => {
        uploadProgress.value = progress.percentage
        uploadStage.value = progress.stage
        uploadStatus.value = progress.status
        uploadErrorMessage.value = progress.errorMessage
        if (progress.status === 'completed') {
          isUploadingImage.value = false
          // The uploaded bytes, not the new URL: showing them straight away avoids a round trip,
          // and the parent's `updated` refetch brings the canonical (server-processed) URL in.
          imagePreview.value = `data:image/jpeg;base64,${base64Data}`
          emit('updated')
          setTimeout(() => handleCloseUpload(), 500)
        } else if (progress.status === 'failed' || progress.status === 'cancelled') {
          isUploadingImage.value = false
        }
      })
    }
  } catch (error) {
    console.error('Failed to start upload:', error)
    isUploadingImage.value = false
    uploadStatus.value = 'failed'
  }
}

async function handleCancelUpload() {
  if (currentUploadJobId.value) {
    try {
      await progressApi.cancelJob(currentUploadJobId.value)
      if (stopPolling) { stopPolling(); stopPolling = null }
      isUploadingImage.value = false
    } catch (error) {
      console.error('Failed to cancel upload:', error)
    }
  }
  uploadStatus.value = 'cancelled'
}

function handleCloseUpload() {
  if (stopPolling) { stopPolling(); stopPolling = null }
  isCropperOpen.value = false
  currentUploadJobId.value = null
  uploadStatus.value = 'idle'
}

async function handleDeleteProductImage() {
  if (!props.product) return
  isDeletingImage.value = true
  try {
    await deleteProductImage(props.product.publicId)
    imagePreview.value = undefined
    emit('updated')
  } catch (error) {
    console.error('Failed to delete image:', error)
  } finally {
    isDeletingImage.value = false
  }
}

async function handleImportImageFromBarcode() {
  if (!props.product || !form.value.barcode) return
  isImportingImageFromBarcode.value = true
  try {
    const response = await openFoodFactsApi.getProductByBarcode(form.value.barcode.trim())
    if (response.success && response.data && response.data.image_base64) {
      let base64Data = response.data.image_base64
      if (base64Data.includes(',')) base64Data = base64Data.split(',')[1] || ''
      await uploadProductImage(props.product.publicId, { productPublicId: props.product.publicId, imageBase64: base64Data })
      imagePreview.value = `data:image/jpeg;base64,${base64Data}`
      emit('updated')
    } else {
      toast.add({ title: t('toast.error'), description: t('pages.addProduct.openFoodFacts.noProductError'), color: 'error' })
    }
  } catch (error) {
    console.error('Failed to import image from barcode:', error)
    toast.add({ title: t('toast.error'), description: t('pages.addProduct.openFoodFacts.noProductError'), color: 'error' })
  } finally {
    isImportingImageFromBarcode.value = false
  }
}

// --- OpenFoodFacts name/brand import ----------------------------------------
const isQueryingBarcode = ref(false)
const isOpenFoodFactsModalOpen = ref(false)
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)

async function handleBarcodeQuery() {
  if (!form.value.barcode?.trim()) {
    toast.add({ title: t('toast.error'), description: t('pages.addProduct.openFoodFacts.noBarcodeError'), color: 'error' })
    return
  }
  isQueryingBarcode.value = true
  try {
    const response = await openFoodFactsApi.getProductByBarcode(form.value.barcode.trim())
    if (response.success && response.data) {
      openFoodFactsProduct.value = response.data
      isOpenFoodFactsModalOpen.value = true
    } else {
      toast.add({ title: t('toast.error'), description: t('pages.addProduct.openFoodFacts.noProductError'), color: 'error' })
    }
  } catch (error) {
    console.error('OpenFoodFacts query failed:', error)
    toast.add({ title: t('toast.error'), description: t('pages.addProduct.openFoodFacts.noProductError'), color: 'error' })
  } finally {
    isQueryingBarcode.value = false
  }
}

function handleImportProduct() {
  if (openFoodFactsProduct.value) {
    if (openFoodFactsProduct.value.product_name) form.value.name = openFoodFactsProduct.value.product_name
    if (openFoodFactsProduct.value.brands) form.value.brand = openFoodFactsProduct.value.brands
  }
  handleCancelImport()
}

function handleCancelImport() {
  isOpenFoodFactsModalOpen.value = false
  openFoodFactsProduct.value = null
}

// Called by the parent page's shared barcode scanner when this drawer is open.
function applyScannedBarcode(barcode: string) {
  form.value.barcode = barcode
  nextTick(() => handleBarcodeQuery())
}
defineExpose({ applyScannedBarcode })

// --- Submit ------------------------------------------------------------------
async function onSubmit(event: FormSubmitEvent<ProductSchema>) {
  const data = event.data
  saving.value = true
  const failureMessage = { errorMessage: t('pages.addProduct.form.saveFailed') }

  try {
    const res = props.product
      ? await updateProduct(props.product.publicId, toUpdateProductRequest(data), failureMessage)
      : await createProduct(toCreateProductRequest(data), failureMessage)

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
    } else {
      // The API answered, so useApiClient has already shown the one toast. All that is left
      // is to put whatever it could pin on a field next to that field.
      formRef.value?.setErrors(toFormErrors(res.validationErrors, Object.keys(form.value)))
    }
  } catch (error) {
    // Only a request that never reached the API lands here, and useApiClient stays silent
    // for those, so this is the single report.
    console.error('Failed to save product:', error)
    toast.add({ title: t('common.error'), description: t('pages.addProduct.form.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    saving.value = false
  }
}
</script>
