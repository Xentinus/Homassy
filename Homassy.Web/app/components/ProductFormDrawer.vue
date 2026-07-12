<template>
  <UDrawer
    :open="open"
    :dismissible="false"
    :ui="{
      content: 'max-h-[90dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'min-h-0 overflow-y-auto p-4 sm:p-6',
      footer: 'shrink-0 flex flex-row items-center justify-end gap-2 border-t border-default p-4 sm:px-6'
    }"
    @update:open="(v) => emit('update:open', v)"
  >
    <template #header>
      <div ref="headerEl" class="flex items-center gap-3 w-full" style="touch-action: none">
        <UIcon name="i-lucide-package" class="h-7 w-7 shrink-0 text-primary-500" />
        <DrawerTitle class="text-xl sm:text-2xl font-semibold">{{ title }}</DrawerTitle>
        <DrawerDescription class="sr-only">{{ title }}</DrawerDescription>
        <UButton
          class="ml-auto"
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          :aria-label="t('common.close')"
          @click="emit('update:open', false)"
        />
      </div>
    </template>

    <template #body>
      <UForm ref="formRef" :schema="schema" :state="form" class="space-y-4" @submit="onSubmit">
        <UFormField :label="t('pages.addProduct.form.name')" name="name" required>
          <UInput v-model="form.name" :placeholder="t('pages.addProduct.form.namePlaceholder')" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('pages.addProduct.form.brand')" name="brand" required>
          <UInput v-model="form.brand" :placeholder="t('pages.addProduct.form.brandPlaceholder')" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('pages.addProduct.form.category')" name="category">
          <USelectMenu v-model="form.category" :items="categoryOptions" value-key="value" :placeholder="t('pages.addProduct.form.categoryPlaceholder')" :disabled="saving" class="w-full" />
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
    </template>

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
  </UDrawer>

  <!-- OpenFoodFacts import preview -->
  <UModal
    :open="isOpenFoodFactsModalOpen"
    :dismissible="false"
    @update:open="(v) => { if (!v) handleCancelImport() }"
  >
    <template #title>{{ t('pages.addProduct.openFoodFacts.modalTitle') }}</template>
    <template #description>{{ t('pages.addProduct.openFoodFacts.modalDescription') }}</template>
    <template #body>
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
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton :label="t('pages.addProduct.openFoodFacts.cancel')" color="neutral" variant="outline" @click="handleCancelImport" />
        <UButton :label="t('pages.addProduct.openFoodFacts.import')" color="primary" @click="handleImportProduct" />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import { z } from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import { useSelectValueApi } from '~/composables/api/useSelectValueApi'
import { useCameraAvailability } from '~/composables/useCameraAvailability'
import { Unit, SelectValueType } from '~/types/enums'
import type { ProductInfo } from '~/types/product'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import type { SelectValue } from '~/types/selectValue'

/**
 * Create/edit a product (catalog) in a modern bottom-sheet drawer (UForm + Zod). Adds the previously
 * missing `unit` field, keeps barcode scan + OpenFoodFacts import, and fixes the edit regressions
 * (category is a real select; notes are never sent empty, so existing notes are never clobbered).
 * Image upload stays on the card / detail screen. Owns the API call and emits `saved` with the DTO.
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
}>()

const { t } = useI18n()
const toast = useToast()
const { createProduct, updateProduct } = useProductsApi()
const openFoodFactsApi = useOpenFoodFactsApi()
const { getSelectValues } = useSelectValueApi()
const { showCameraButton } = useCameraAvailability()

const isEdit = computed(() => !!props.product)
const title = computed(() => isEdit.value
  ? t('pages.products.details.editProduct')
  : t('pages.addProduct.form.createProduct'))

const schema = z.object({
  name: z.string({ required_error: t('pages.addProduct.form.nameRequired') }).min(1, t('pages.addProduct.form.nameRequired')),
  brand: z.string({ required_error: t('pages.addProduct.form.brandRequired') }).min(1, t('pages.addProduct.form.brandRequired')),
  category: z.string().optional(),
  unit: z.nativeEnum(Unit),
  barcode: z.string().optional(),
  isEatable: z.boolean().optional().default(false),
  isFavorite: z.boolean().optional().default(false),
  notes: z.string().optional()
})
type Schema = z.output<typeof schema>

const emptyForm = () => ({
  name: '',
  brand: '',
  category: undefined as string | undefined,
  unit: Unit.Piece,
  barcode: '',
  isEatable: false,
  isFavorite: false,
  notes: ''
})

const form = ref(emptyForm())
const saving = ref(false)
const formRef = ref()
const headerEl = ref<HTMLElement | null>(null)

useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => saving.value
})

// Category options (from the shared select-value endpoint), loaded once.
const categoryOptionsRaw = ref<SelectValue[]>([])
const categoryOptions = computed(() =>
  categoryOptionsRaw.value.map(cat => ({ label: t(`enums.productCategory.${cat.text}`), value: cat.text }))
)

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

// Seed the form each time the drawer opens.
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
  } else {
    form.value = emptyForm()
  }
})

// --- OpenFoodFacts import ----------------------------------------------------
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
async function onSubmit(event: FormSubmitEvent<Schema>) {
  const data = event.data
  saving.value = true
  try {
    let res
    if (props.product) {
      // Never send notes empty — the product DTO can't read existing notes, so an empty value would
      // silently clobber them. Only send when the user actually typed something.
      res = await updateProduct(props.product.publicId, {
        name: data.name.trim(),
        brand: data.brand.trim(),
        category: data.category || undefined,
        unit: data.unit,
        barcode: data.barcode?.trim() || undefined,
        isEatable: data.isEatable,
        notes: data.notes?.trim() || undefined
      })
    } else {
      res = await createProduct({
        name: data.name.trim(),
        brand: data.brand.trim(),
        category: data.category || null,
        unit: data.unit,
        barcode: data.barcode?.trim() || null,
        isEatable: data.isEatable,
        isFavorite: data.isFavorite,
        notes: data.notes?.trim() || null
      })
    }

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
    } else {
      toast.add({ title: t('common.error'), description: t('pages.addProduct.form.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
    }
  } catch (error) {
    console.error('Failed to save product:', error)
    toast.add({ title: t('common.error'), description: t('pages.addProduct.form.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    saving.value = false
  }
}
</script>
