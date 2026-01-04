<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-4">
      <div class="flex items-center gap-3">
        <NuxtLink to="/shopping-lists">
          <UButton
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
          />
        </NuxtLink>
        <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 text-primary-500" />
        <h1 class="text-2xl font-semibold">{{ t('pages.shoppingLists.addProduct.title') }}</h1>
      </div>

      <!-- Stepper -->
      <UStepper
        :model-value="currentStep"
        :items="stepperItems"
        orientation="horizontal"
        @update:model-value="handleStepChange"
      />
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-48 px-4 sm:px-8 lg:px-14 pb-6">
      <!-- Step 0: Product Selection (3 Tabs) -->
      <div v-if="currentStep === 0" class="space-y-6">
        <UTabs v-model="activeTab" :items="tabItems">
          <!-- Tab 1: Search Product -->
          <template #search>
            <div class="space-y-4 py-4">
              <UFieldGroup size="lg" orientation="horizontal" class="w-full">
                <UInput
                  v-model="searchQuery"
                  :placeholder="t('pages.addProduct.search.placeholder')"
                  icon="i-lucide-search"
                  size="lg"
                  class="flex-1"
                />
                <BarcodeScannerButton @scanned="handleSearchBarcodeScanned" />
              </UFieldGroup>

              <!-- Loading State -->
              <div v-if="isSearching" class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
              </div>

              <!-- Empty State -->
              <div
                v-else-if="searchQuery.trim() === ''"
                class="text-center py-12 text-gray-500"
              >
                {{ t('pages.addProduct.search.startTyping') }}
              </div>

              <!-- No Results -->
              <div
                v-else-if="searchResults.length === 0 && !isSearching"
                class="text-center py-12 text-gray-500"
              >
                {{ t('pages.addProduct.search.noResults') }}
              </div>

              <!-- Results Grid -->
              <div
                v-else
                class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4"
              >
                <ProductCard
                  v-for="product in searchResults"
                  :key="product.publicId"
                  :product="product"
                  :is-active="selectedCardId === product.publicId"
                  @click="onProductCardClick(product)"
                />
              </div>
            </div>
          </template>

          <!-- Tab 2: Create New Product -->
          <template #create>
            <div class="py-4">
              <UForm
                :schema="createProductSchema"
                :state="productFormData"
                class="space-y-4"
                @submit="onCreateProduct"
              >
                <UFormField :label="t('pages.addProduct.form.name')" name="name" required>
                  <UInput
                    v-model="productFormData.name"
                    :placeholder="t('pages.addProduct.form.namePlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.brand')" name="brand" required>
                  <UInput
                    v-model="productFormData.brand"
                    :placeholder="t('pages.addProduct.form.brandPlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.category')" name="category">
                  <UInput
                    v-model="productFormData.category"
                    :placeholder="t('pages.addProduct.form.categoryPlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.barcode')" name="barcode">
                  <UFieldGroup size="md" orientation="horizontal" class="w-full">
                    <UInput
                      v-model="productFormData.barcode"
                      :placeholder="t('pages.addProduct.form.barcodePlaceholder')"
                      :disabled="isCreating"
                      inputmode="numeric"
                      pattern="[0-9]*"
                      class="flex-1"
                    />
                    <BarcodeScannerButton :disabled="isCreating" @scanned="handleBarcodeScanned" />
                    <UButton
                      :label="t('pages.addProduct.form.barcodeQuery')"
                      icon="i-lucide-barcode"
                      color="primary"
                      size="sm"
                      :loading="isQueryingBarcode"
                      :disabled="isCreating"
                      @click="queryOpenFoodFacts"
                    />
                  </UFieldGroup>
                </UFormField>

                <UFormField name="isEatable">
                  <UCheckbox
                    v-model="productFormData.isEatable"
                    :label="t('pages.addProduct.form.isEatableLabel')"
                    :disabled="isCreating"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.notes')" name="notes">
                  <UTextarea
                    v-model="productFormData.notes"
                    :placeholder="t('pages.addProduct.form.notesPlaceholder')"
                    :disabled="isCreating"
                    :rows="3"
                    class="w-full"
                  />
                </UFormField>

                <UFormField name="isFavorite">
                  <UCheckbox
                    v-model="productFormData.isFavorite"
                    :label="t('pages.addProduct.form.isFavoriteLabel')"
                    :disabled="isCreating"
                  />
                </UFormField>

                <UButton
                  type="submit"
                  color="primary"
                  block
                  :loading="isCreating"
                  :disabled="isCreating"
                >
                  {{ t('pages.addProduct.form.createButton') }}
                </UButton>
              </UForm>
            </div>
          </template>

          <!-- Tab 3: Custom Item (NEW) -->
          <template #custom>
            <div class="py-4">
              <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
                {{ t('pages.shoppingLists.addProduct.custom.description') }}
              </p>

              <UForm
                :schema="customNameSchema"
                :state="customFormData"
                class="space-y-4"
                @submit="onCustomNameSubmit"
              >
                <UFormField :label="t('pages.shoppingLists.addProduct.custom.label')" name="customName" required>
                  <UInput
                    v-model="customFormData.customName"
                    :placeholder="t('pages.shoppingLists.addProduct.custom.placeholder')"
                    class="w-full"
                  />
                </UFormField>

                <UButton
                  type="submit"
                  color="primary"
                  block
                >
                  {{ t('pages.shoppingLists.addProduct.custom.submitButton') }}
                </UButton>
              </UForm>
            </div>
          </template>
        </UTabs>
      </div>

      <!-- Step 1: Shopping Location (OPTIONAL with Skip) -->
      <div v-if="currentStep === 1" class="space-y-6">
        <!-- Skip Button -->
        <div class="flex justify-end">
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-skip-forward"
            @click="onSkipLocation"
          >
            {{ t('pages.shoppingLists.addProduct.skipLocation') }}
          </UButton>
        </div>

        <UTabs v-model="shoppingLocationTab" :items="shoppingLocationTabItems">
          <!-- Search Shopping Location -->
          <template #search>
            <div class="space-y-4 py-4">
              <UInput
                v-model="shoppingLocationSearchQuery"
                :placeholder="t('pages.addProduct.shoppingLocation.search.placeholder')"
                icon="i-lucide-search"
                size="lg"
                class="w-full"
              />

              <!-- Loading State -->
              <div v-if="isLoadingShoppingLocations" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
              </div>

              <!-- No Results -->
              <div
                v-else-if="filteredShoppingLocations.length === 0"
                class="text-center py-12 text-gray-500"
              >
                {{ t('pages.addProduct.shoppingLocation.search.noResults') }}
              </div>

              <!-- Results Grid -->
              <div
                v-else
                class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4"
              >
                <ShoppingLocationCard
                  v-for="location in paginatedShoppingLocations"
                  :key="location.publicId"
                  :location="location"
                  :is-active="selectedShoppingLocationCardId === location.publicId"
                  @click="onShoppingLocationCardClick(location)"
                />
              </div>

              <!-- Load More Sentinel -->
              <div ref="shoppingLocationSentinelRef" class="h-1" />
            </div>
          </template>

          <!-- Create Shopping Location -->
          <template #create>
            <div class="py-4">
              <UForm
                :schema="createShoppingLocationSchema"
                :state="shoppingLocationFormData"
                class="space-y-4"
                @submit="onCreateShoppingLocation"
              >
                <UFormField :label="t('pages.addProduct.shoppingLocation.form.name')" name="name" required>
                  <UInput
                    v-model="shoppingLocationFormData.name"
                    :placeholder="t('pages.addProduct.shoppingLocation.form.namePlaceholder')"
                    :disabled="isCreatingShoppingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.description')" name="description">
                  <UTextarea
                    v-model="shoppingLocationFormData.description"
                    :placeholder="t('pages.addProduct.shoppingLocation.form.descriptionPlaceholder')"
                    :disabled="isCreatingShoppingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.address')" name="address">
                  <UInput
                    v-model="shoppingLocationFormData.address"
                    :placeholder="t('pages.addProduct.shoppingLocation.form.addressPlaceholder')"
                    :disabled="isCreatingShoppingLocation"
                    class="w-full"
                  />
                </UFormField>

                <div class="grid grid-cols-2 gap-4">
                  <UFormField :label="t('pages.addProduct.shoppingLocation.form.city')" name="city">
                    <UInput
                      v-model="shoppingLocationFormData.city"
                      :placeholder="t('pages.addProduct.shoppingLocation.form.cityPlaceholder')"
                      :disabled="isCreatingShoppingLocation"
                      class="w-full"
                    />
                  </UFormField>

                  <UFormField :label="t('pages.addProduct.shoppingLocation.form.postalCode')" name="postalCode">
                    <UInput
                      v-model="shoppingLocationFormData.postalCode"
                      :placeholder="t('pages.addProduct.shoppingLocation.form.postalCodePlaceholder')"
                      :disabled="isCreatingShoppingLocation"
                      class="w-full"
                    />
                  </UFormField>
                </div>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.country')" name="country">
                  <UInput
                    v-model="shoppingLocationFormData.country"
                    :placeholder="t('pages.addProduct.shoppingLocation.form.countryPlaceholder')"
                    :disabled="isCreatingShoppingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.website')" name="website">
                  <UInput
                    v-model="shoppingLocationFormData.website"
                    type="url"
                    :placeholder="t('pages.addProduct.shoppingLocation.form.websitePlaceholder')"
                    :disabled="isCreatingShoppingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.googleMaps')" name="googleMaps">
                  <UInput
                    v-model="shoppingLocationFormData.googleMaps"
                    type="url"
                    :placeholder="t('pages.addProduct.shoppingLocation.form.googleMapsPlaceholder')"
                    :disabled="isCreatingShoppingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.color')" name="color">
                  <div class="flex items-center gap-3">
                    <UPopover>
                      <UButton
                        color="neutral"
                        variant="outline"
                        :disabled="isCreatingShoppingLocation"
                      >
                        <div class="flex items-center gap-2">
                          <div
                            v-if="shoppingLocationFormData.color"
                            class="w-4 h-4 rounded"
                            :style="{ backgroundColor: shoppingLocationFormData.color }"
                          />
                          <span>{{ shoppingLocationFormData.color || t('pages.addProduct.shoppingLocation.form.chooseColor') }}</span>
                        </div>
                      </UButton>
                      <template #content>
                        <UColorPicker v-model="shoppingLocationFormData.color" />
                      </template>
                    </UPopover>
                    <UButton
                      v-if="shoppingLocationFormData.color"
                      icon="i-lucide-x"
                      color="neutral"
                      variant="ghost"
                      size="sm"
                      :disabled="isCreatingShoppingLocation"
                      @click="shoppingLocationFormData.color = ''"
                    />
                  </div>
                </UFormField>

                <UFormField name="isSharedWithFamily">
                  <UCheckbox
                    v-model="shoppingLocationFormData.isSharedWithFamily"
                    :label="t('pages.addProduct.shoppingLocation.form.isSharedWithFamilyLabel')"
                    :disabled="isCreatingShoppingLocation"
                  />
                </UFormField>

                <UButton
                  type="submit"
                  color="primary"
                  block
                  :loading="isCreatingShoppingLocation"
                  :disabled="isCreatingShoppingLocation"
                >
                  {{ t('pages.addProduct.shoppingLocation.form.createButton') }}
                </UButton>
              </UForm>
            </div>
          </template>
        </UTabs>
      </div>

      <!-- Step 2: Item Details -->
      <div v-if="currentStep === 2" class="space-y-6">
        <UForm
          :schema="createShoppingListItemSchema"
          :state="itemFormData"
          class="space-y-4"
          @submit="onCreateShoppingListItem"
        >
          <UFormField :label="t('pages.shoppingLists.addProduct.item.quantityLabel')" name="quantity" required>
            <UInput
              v-model.number="itemFormData.quantity"
              type="number"
              step="0.01"
              min="0.01"
              :placeholder="t('pages.shoppingLists.addProduct.item.quantityPlaceholder')"
              :disabled="isCreatingItem"
              class="w-full"
            />
          </UFormField>

          <UFormField :label="t('pages.shoppingLists.addProduct.item.unitLabel')" name="unit" required>
            <USelect
              v-model="itemFormData.unit"
              :items="unitOptions"
              :placeholder="t('pages.shoppingLists.addProduct.item.unitPlaceholder')"
              :disabled="isCreatingItem"
              class="w-full"
            />
          </UFormField>

          <UFormField :label="t('pages.shoppingLists.addProduct.item.noteLabel')" name="note">
            <UTextarea
              v-model="itemFormData.note"
              :placeholder="t('pages.shoppingLists.addProduct.item.notePlaceholder')"
              :disabled="isCreatingItem"
              :rows="3"
              class="w-full"
            />
          </UFormField>

          <UFormField :label="t('pages.shoppingLists.addProduct.item.deadlineLabel')" name="deadlineAt">
            <UInputDate v-model="itemFormData.deadlineAt" :disabled="isCreatingItem" class="w-full">
              <template #trailing>
                <UPopover>
                  <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" />
                  <template #content>
                    <UCalendar v-model="itemFormData.deadlineAt" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </UFormField>

          <UFormField :label="t('pages.shoppingLists.addProduct.item.dueLabel')" name="dueAt">
            <UInputDate v-model="itemFormData.dueAt" :disabled="isCreatingItem" class="w-full">
              <template #trailing>
                <UPopover>
                  <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" />
                  <template #content>
                    <UCalendar v-model="itemFormData.dueAt" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </UFormField>

          <UButton
            type="submit"
            color="primary"
            block
            :loading="isCreatingItem"
            :disabled="isCreatingItem"
          >
            {{ t('pages.shoppingLists.addProduct.item.createButton') }}
          </UButton>
        </UForm>
      </div>
    </div>

    <!-- OpenFoodFacts Modal -->
    <UModal
      :open="isOpenFoodFactsModalOpen"
      @update:open="(val) => { if (!val) isOpenFoodFactsModalOpen = false }"
    >
      <template #title>
        {{ t('pages.addProduct.openFoodFacts.modalTitle') }}
      </template>

      <template #description>
        {{ t('pages.addProduct.openFoodFacts.modalDescription') }}
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
                {{ t('pages.addProduct.openFoodFacts.productName') }}:
              </span>
              <p class="text-sm mt-1">{{ openFoodFactsProduct.product_name }}</p>
            </div>

            <div v-if="openFoodFactsProduct?.brands">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ t('pages.addProduct.openFoodFacts.brands') }}:
              </span>
              <p class="text-sm mt-1">{{ openFoodFactsProduct.brands }}</p>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="t('pages.addProduct.openFoodFacts.cancel')"
            color="neutral"
            variant="outline"
            @click="isOpenFoodFactsModalOpen = false"
          />
          <UButton
            :label="t('pages.addProduct.openFoodFacts.import')"
            color="primary"
            @click="importOpenFoodFactsData"
          />
        </div>
      </template>
    </UModal>

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="activeBarcodeHandler" />
  </div>
</template>

<script setup lang="ts">
import { z } from 'zod'
import { watchDebounced } from '@vueuse/core'
import { computed, nextTick } from 'vue'
import type { ProductInfo, CreateProductRequest } from '~/types/product'
import type { ShoppingLocationInfo, CreateShoppingLocationRequest } from '~/types/location'
import type { CreateShoppingListItemRequest } from '~/types/shoppingList'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import { Unit } from '~/types/enums'
import type { FormSubmitEvent } from '#ui/types'

// Middleware & Layout
definePageMeta({
  middleware: 'auth',
  layout: 'auth'
})

// Composables
const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const toast = useToast()
const { getProducts, createProduct } = useProductsApi()
const { getShoppingLocations, createShoppingLocation } = useLocationsApi()
const { createShoppingListItem } = useShoppingListApi()
const { getProductByBarcode } = useOpenFoodFactsApi()

// =========================
// Shopping List ID Validation
// =========================
const shoppingListId = ref<string | null>(null)

onMounted(() => {
  const listId = route.query.listId as string | undefined

  if (!listId) {
    toast.add({
      title: t('toast.error'),
      description: t('pages.shoppingLists.addProduct.noListSelected'),
      color: 'error'
    })
    router.push('/shopping-lists')
    return
  }

  shoppingListId.value = listId
})

// =========================
// Stepper State
// =========================
const currentStep = ref(0)
const stepperItems = computed(() => [
  { label: t('pages.shoppingLists.addProduct.stepLabels.step1') },
  { label: t('pages.shoppingLists.addProduct.stepLabels.step2') },
  { label: t('pages.shoppingLists.addProduct.stepLabels.step3') }
])

const handleStepChange = (newStep: number) => {
  if (newStep <= currentStep.value) {
    currentStep.value = newStep
  }
}

// =========================
// Step 0: Product Selection State
// =========================
const activeTab = ref(0)
const tabItems = computed(() => [
  { label: t('pages.shoppingLists.addProduct.tabs.search'), value: 0, slot: 'search' },
  { label: t('pages.shoppingLists.addProduct.tabs.create'), value: 1, slot: 'create' },
  { label: t('pages.shoppingLists.addProduct.tabs.custom'), value: 2, slot: 'custom' }
])

// Product Selection Mode
const productSelectionMode = ref<'product' | 'custom' | null>(null)
const selectedProductId = ref<string | null>(null)
const customProductName = ref<string>('')
const selectedCardId = ref<string | null>(null)

// Tab 1: Search Product
const searchQuery = ref('')
const searchResults = ref<ProductInfo[]>([])
const isSearching = ref(false)

// Tab 2: Create Product
const productFormData = ref<CreateProductRequest>({
  name: '',
  brand: '',
  category: '',
  barcode: '',
  isEatable: false,
  notes: '',
  isFavorite: false
})
const isCreating = ref(false)
const isQueryingBarcode = ref(false)
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)
const isOpenFoodFactsModalOpen = ref(false)
const isImageLoading = ref(true)

// Tab 3: Custom Name
const customFormData = ref({
  customName: ''
})

// =========================
// Step 1: Shopping Location State
// =========================
const shoppingLocationTab = ref(0)
const shoppingLocationTabItems = computed(() => [
  { label: t('pages.addProduct.shoppingLocation.tabs.search'), value: 0, slot: 'search' },
  { label: t('pages.addProduct.shoppingLocation.tabs.create'), value: 1, slot: 'create' }
])

const allShoppingLocations = ref<ShoppingLocationInfo[]>([])
const shoppingLocationSearchQuery = ref('')
const isLoadingShoppingLocations = ref(false)
const selectedShoppingLocationId = ref<string | null>(null)
const selectedShoppingLocationCardId = ref<string | null>(null)
const currentShoppingLocationPage = ref(1)
const shoppingLocationPageSize = 20
const loadingMoreShoppingLocations = ref(false)
const shoppingLocationSentinelRef = ref<HTMLElement | null>(null)

const shoppingLocationFormData = ref<CreateShoppingLocationRequest>({
  name: '',
  description: '',
  color: '',
  address: '',
  city: '',
  postalCode: '',
  country: '',
  website: '',
  googleMaps: '',
  isSharedWithFamily: true
})
const isCreatingShoppingLocation = ref(false)

// =========================
// Step 2: Item Details State
// =========================
const itemFormData = ref({
  quantity: 1,
  unit: undefined as Unit | undefined,
  note: '',
  deadlineAt: null as any,
  dueAt: null as any
})
const isCreatingItem = ref(false)

// =========================
// Zod Schemas
// =========================
const createProductSchema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters').max(128, 'Name must not exceed 128 characters'),
  brand: z.string().min(2, 'Brand must be at least 2 characters').max(128, 'Brand must not exceed 128 characters'),
  category: z.string().max(128, 'Category must not exceed 128 characters').optional().or(z.literal('')),
  barcode: z.string().max(50, 'Barcode must not exceed 50 characters').optional().or(z.literal('')),
  isEatable: z.boolean().optional().default(false),
  notes: z.string().max(500, 'Notes must not exceed 500 characters').optional().or(z.literal('')),
  isFavorite: z.boolean().optional().default(false)
})

const customNameSchema = z.object({
  customName: z.string().min(2, 'Name must be at least 2 characters').max(128, 'Name must not exceed 128 characters')
})

const createShoppingLocationSchema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters').max(128, 'Name must not exceed 128 characters'),
  description: z.string().max(500, 'Description must not exceed 500 characters').optional().or(z.literal('')),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Color must be a valid hex color').optional().or(z.literal('')),
  address: z.string().max(256, 'Address must not exceed 256 characters').optional().or(z.literal('')),
  city: z.string().max(128, 'City must not exceed 128 characters').optional().or(z.literal('')),
  postalCode: z.string().max(20, 'Postal code must not exceed 20 characters').optional().or(z.literal('')),
  country: z.string().max(128, 'Country must not exceed 128 characters').optional().or(z.literal('')),
  website: z.string().url('Must be a valid URL').optional().or(z.literal('')),
  googleMaps: z.string().url('Must be a valid URL').optional().or(z.literal('')),
  isSharedWithFamily: z.boolean().optional().default(true)
})

const createShoppingListItemSchema = z.object({
  quantity: z.number({ required_error: 'Quantity is required' }).min(0.001, 'Quantity must be greater than 0'),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
  note: z.string().max(500, 'Note must not exceed 500 characters').optional(),
  deadlineAt: z.any().nullable().optional(),
  dueAt: z.any().nullable().optional()
})

// =========================
// Computed Properties
// =========================
const unitOptions = computed(() => {
  return Object.entries(Unit)
    .filter(([key]) => isNaN(Number(key)))
    .map(([_key, value]) => ({
      label: t(`enums.unit.${value}`),
      value: value as Unit
    }))
})

const filteredShoppingLocations = computed(() => {
  const query = shoppingLocationSearchQuery.value.toLowerCase().trim()

  if (!query) {
    return allShoppingLocations.value
  }

  return allShoppingLocations.value.filter(location =>
    location.name?.toLowerCase().includes(query) ||
    location.description?.toLowerCase().includes(query) ||
    location.address?.toLowerCase().includes(query) ||
    location.city?.toLowerCase().includes(query)
  )
})

const paginatedShoppingLocations = computed(() => {
  return filteredShoppingLocations.value.slice(
    0,
    currentShoppingLocationPage.value * shoppingLocationPageSize
  )
})

// =========================
// Watchers
// =========================

// Ensure barcode contains only numeric characters
watch(() => productFormData.value.barcode, (newValue) => {
  if (newValue) {
    productFormData.value.barcode = newValue.replace(/\D/g, '')
  }
})

// Product search debounce
watchDebounced(
  searchQuery,
  async (newQuery) => {
    if (newQuery.trim() === '') {
      searchResults.value = []
      return
    }

    isSearching.value = true
    try {
      const response = await getProducts({
        searchText: newQuery,
        pageNumber: 1,
        pageSize: 20
      })

      if (response.success && response.data) {
        searchResults.value = response.data.items
      }
    } catch (error) {
      console.error('Product search failed:', error)
    } finally {
      isSearching.value = false
    }
  },
  { debounce: 300 }
)

// Load shopping locations when Step 1 is entered
watch(currentStep, async (newStep) => {
  if (newStep === 1 && allShoppingLocations.value.length === 0) {
    isLoadingShoppingLocations.value = true
    try {
      const response = await getShoppingLocations({ returnAll: true })
      if (response.success && response.data) {
        allShoppingLocations.value = response.data.items
      }
    } catch (error) {
      console.error('Failed to load shopping locations:', error)
    } finally {
      isLoadingShoppingLocations.value = false
    }
  }
})

// Reset pagination when search query changes
watch(shoppingLocationSearchQuery, () => {
  currentShoppingLocationPage.value = 1
})

// Intersection Observer for lazy loading shopping locations
onMounted(() => {
  if (shoppingLocationSentinelRef.value) {
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && !loadingMoreShoppingLocations.value) {
          const hasMore = paginatedShoppingLocations.value.length < filteredShoppingLocations.value.length
          if (hasMore) {
            loadingMoreShoppingLocations.value = true
            currentShoppingLocationPage.value++
            loadingMoreShoppingLocations.value = false
          }
        }
      },
      { threshold: 0.1 }
    )

    observer.observe(shoppingLocationSentinelRef.value)

    onUnmounted(() => {
      observer.disconnect()
    })
  }
})

// =========================
// Step 0: Product Selection Handlers
// =========================

// Tab 1: Product Card Click
const onProductCardClick = (product: ProductInfo) => {
  selectedCardId.value = product.publicId
  selectedProductId.value = product.publicId
  customProductName.value = ''
  productSelectionMode.value = 'product'
  currentStep.value = 1
}

// Tab 2: Create Product
const onCreateProduct = async (event: FormSubmitEvent<typeof createProductSchema>) => {
  isCreating.value = true
  try {
    const productData: CreateProductRequest = {
      name: event.data.name,
      brand: event.data.brand,
      category: event.data.category || undefined,
      barcode: event.data.barcode || undefined,
      isEatable: event.data.isEatable || false,
      notes: event.data.notes || undefined,
      isFavorite: event.data.isFavorite || false
    }

    const response = await createProduct(productData)
    if (response.success && response.data) {
      selectedProductId.value = response.data.publicId
      customProductName.value = ''
      productSelectionMode.value = 'product'
      currentStep.value = 1
    }
  } catch (error) {
    console.error('Product creation failed:', error)
  } finally {
    isCreating.value = false
  }
}

// Tab 2: OpenFoodFacts Query
const queryOpenFoodFacts = async () => {
  if (!productFormData.value.barcode) {
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.openFoodFacts.noBarcodeError'),
      color: 'error'
    })
    return
  }

  isQueryingBarcode.value = true
  try {
    const response = await getProductByBarcode(productFormData.value.barcode)

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

const importOpenFoodFactsData = () => {
  if (openFoodFactsProduct.value) {
    productFormData.value.name = openFoodFactsProduct.value.product_name || productFormData.value.name
    productFormData.value.brand = openFoodFactsProduct.value.brands || productFormData.value.brand
    productFormData.value.isEatable = true
  }
  isOpenFoodFactsModalOpen.value = false
}

// Barcode scanner handler (for Create Product tab)
const handleBarcodeScanned = (barcode: string) => {
  productFormData.value.barcode = barcode
  // Auto-trigger OpenFoodFacts query
  nextTick(() => {
    queryOpenFoodFacts()
  })
}

// Barcode scanner handler (for Search Product tab)
const handleSearchBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Dynamic handler based on context
const activeBarcodeHandler = computed(() => {
  return activeTab.value === 0 ? handleSearchBarcodeScanned : handleBarcodeScanned
})

// Tab 3: Custom Name Submit
const onCustomNameSubmit = (event: FormSubmitEvent<typeof customNameSchema>) => {
  customProductName.value = event.data.customName
  selectedProductId.value = null
  productSelectionMode.value = 'custom'
  currentStep.value = 1
}

// =========================
// Step 1: Shopping Location Handlers
// =========================

const onSkipLocation = () => {
  selectedShoppingLocationId.value = null
  currentStep.value = 2
}

const onShoppingLocationCardClick = (location: ShoppingLocationInfo) => {
  selectedShoppingLocationCardId.value = location.publicId
  selectedShoppingLocationId.value = location.publicId
  currentStep.value = 2
}

const onCreateShoppingLocation = async (event: FormSubmitEvent<typeof createShoppingLocationSchema>) => {
  isCreatingShoppingLocation.value = true
  try {
    const locationData: CreateShoppingLocationRequest = {
      name: event.data.name,
      description: event.data.description || undefined,
      color: event.data.color || undefined,
      address: event.data.address || undefined,
      city: event.data.city || undefined,
      postalCode: event.data.postalCode || undefined,
      country: event.data.country || undefined,
      website: event.data.website || undefined,
      googleMaps: event.data.googleMaps || undefined,
      isSharedWithFamily: event.data.isSharedWithFamily ?? true
    }

    const response = await createShoppingLocation(locationData)
    if (response.success && response.data) {
      selectedShoppingLocationId.value = response.data.publicId
      currentStep.value = 2
    }
  } catch (error) {
    console.error('Shopping location creation failed:', error)
  } finally {
    isCreatingShoppingLocation.value = false
  }
}

// =========================
// Step 2: Shopping List Item Handler
// =========================

const onCreateShoppingListItem = async (event: FormSubmitEvent<typeof createShoppingListItemSchema>) => {
  if (!shoppingListId.value) {
    toast.add({
      title: t('toast.error'),
      description: t('pages.shoppingLists.addProduct.noListSelected'),
      color: 'error'
    })
    return
  }

  isCreatingItem.value = true

  try {
    // Convert CalendarDate to ISO string if present
    let deadlineAtString: string | undefined = undefined
    if (event.data.deadlineAt) {
      const date = event.data.deadlineAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      deadlineAtString = localDate.toISOString()
    }

    let dueAtString: string | undefined = undefined
    if (event.data.dueAt) {
      const date = event.data.dueAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      dueAtString = localDate.toISOString()
    }

    const requestData: CreateShoppingListItemRequest = {
      shoppingListPublicId: shoppingListId.value,
      productPublicId: productSelectionMode.value === 'product' ? selectedProductId.value! : undefined,
      customName: productSelectionMode.value === 'custom' ? customProductName.value : undefined,
      shoppingLocationPublicId: selectedShoppingLocationId.value || undefined,
      quantity: event.data.quantity,
      unit: event.data.unit,
      note: event.data.note?.trim() || undefined,
      deadlineAt: deadlineAtString,
      dueAt: dueAtString
    }

    const response = await createShoppingListItem(requestData)

    if (response.success && response.data) {
      router.push('/shopping-lists')
    }
  } catch (error) {
    console.error('Shopping list item creation failed:', error)
  } finally {
    isCreatingItem.value = false
  }
}
</script>
