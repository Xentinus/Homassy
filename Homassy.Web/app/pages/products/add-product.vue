<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-4">
      <div class="flex items-center gap-3">
        <NuxtLink to="/products">
          <UButton
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
          />
        </NuxtLink>
        <UIcon name="i-lucide-package-plus" class="h-7 w-7 text-primary-500" />
        <h1 class="text-2xl font-semibold">{{ t('pages.addProduct.title') }}</h1>
      </div>

      <!-- Stepper -->
      <UStepper
        v-model="currentStep"
        :items="stepperItems"
        orientation="horizontal"
      />
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-40 px-4 sm:px-8 lg:px-14 pb-6">
      <!-- Step 1: Product Selection -->
      <div v-if="currentStep === 0" class="space-y-6">
        <UTabs v-model="activeTab" :items="tabItems">
          <template #search>
            <!-- Tab 1: Search -->
            <div class="space-y-4 py-4">
              <UInput
                v-model="searchQuery"
                :placeholder="t('pages.addProduct.search.placeholder')"
                icon="i-lucide-search"
                size="lg"
                class="w-full"
              />

              <!-- Loading State -->
              <div v-if="isSearching" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
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
                class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4"
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

          <template #create>
            <!-- Tab 2: Create New -->
            <div class="py-4">
              <UForm
                :schema="createProductSchema"
                :state="formData"
                class="space-y-4"
                @submit="onCreateProduct"
              >
                <UFormField :label="t('pages.addProduct.form.name')" name="name" required>
                  <UInput
                    v-model="formData.name"
                    :placeholder="t('pages.addProduct.form.namePlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.brand')" name="brand" required>
                  <UInput
                    v-model="formData.brand"
                    :placeholder="t('pages.addProduct.form.brandPlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.category')" name="category">
                  <UInput
                    v-model="formData.category"
                    :placeholder="t('pages.addProduct.form.categoryPlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.barcode')" name="barcode">
                  <UInput
                    v-model="formData.barcode"
                    :placeholder="t('pages.addProduct.form.barcodePlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.isEatable')" name="isEatable">
                  <UCheckbox
                    v-model="formData.isEatable"
                    :label="t('pages.addProduct.form.isEatableLabel')"
                    :disabled="isCreating"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.notes')" name="notes">
                  <UTextarea
                    v-model="formData.notes"
                    :placeholder="t('pages.addProduct.form.notesPlaceholder')"
                    :disabled="isCreating"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.form.isFavorite')" name="isFavorite">
                  <UCheckbox
                    v-model="formData.isFavorite"
                    :label="t('pages.addProduct.form.isFavoriteLabel')"
                    :disabled="isCreating"
                  />
                </UFormField>

                <UButton
                  type="submit"
                  color="primary"
                  block
                  :loading="isCreating"
                  icon="i-lucide-plus"
                >
                  {{ t('pages.addProduct.form.createButton') }}
                </UButton>
              </UForm>
            </div>
          </template>
        </UTabs>
      </div>

      <!-- Step 2: Placeholder -->
      <div v-else-if="currentStep === 1" class="text-center py-12">
        <p class="text-xl text-gray-600">{{ t('pages.addProduct.placeholders.step2') }}</p>
        <p v-if="selectedProductId" class="text-sm text-gray-500 mt-2">
          {{ t('pages.addProduct.placeholders.selectedProductId', { id: selectedProductId }) }}
        </p>
      </div>

      <!-- Step 3: Placeholder -->
      <div v-else-if="currentStep === 2" class="text-center py-12">
        <p class="text-xl text-gray-600">{{ t('pages.addProduct.placeholders.step3') }}</p>
        <p v-if="selectedProductId" class="text-sm text-gray-500 mt-2">
          {{ t('pages.addProduct.placeholders.selectedProductId', { id: selectedProductId }) }}
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { watchDebounced } from '@vueuse/core'
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import { useProductsApi } from '~/composables/api/useProductsApi'
import type { ProductInfo, CreateProductRequest } from '~/types/product'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { getProducts, createProduct } = useProductsApi()
const { t } = useI18n()

// Stepper state
const currentStep = ref(0)
const stepperItems = computed(() => [
  { label: t('pages.addProduct.stepLabels.step1') },
  { label: t('pages.addProduct.stepLabels.step2') },
  { label: t('pages.addProduct.stepLabels.step3') }
])

// Selected product
const selectedProductId = ref<string | null>(null)

// Tab state
const activeTab = ref(0)
const tabItems = computed(() => [
  { label: t('pages.addProduct.tabs.search'), value: 0, slot: 'search' },
  { label: t('pages.addProduct.tabs.create'), value: 1, slot: 'create' }
])

// Search state
const searchQuery = ref('')
const searchResults = ref<ProductInfo[]>([])
const isSearching = ref(false)
const selectedCardId = ref<string | null>(null)

// Create form state
const isCreating = ref(false)
const formData = ref<CreateProductRequest>({
  name: '',
  brand: '',
  category: '',
  barcode: '',
  isEatable: false,
  notes: '',
  isFavorite: false
})

// Zod schema
const createProductSchema = z.object({
  name: z.string({ required_error: 'A termék neve kötelező' })
    .min(1, 'A termék neve kötelező'),
  brand: z.string({ required_error: 'A márka kötelező' })
    .min(1, 'A márka kötelező'),
  category: z.string().optional(),
  barcode: z.string().optional(),
  isEatable: z.boolean().optional().default(false),
  notes: z.string().optional(),
  isFavorite: z.boolean().optional().default(false)
})

type CreateProductSchema = z.output<typeof createProductSchema>

// Watch search query with debounce
watchDebounced(
  searchQuery,
  async (newValue) => {
    if (newValue.trim() === '') {
      searchResults.value = []
      return
    }

    isSearching.value = true
    try {
      const response = await getProducts({
        searchText: newValue,
        pageSize: 20
      })
      if (response.success && response.data) {
        searchResults.value = response.data.items
      }
    } catch (error) {
      console.error('Search failed:', error)
      searchResults.value = []
    } finally {
      isSearching.value = false
    }
  },
  { debounce: 300 }
)

// Handlers
const onProductCardClick = (product: ProductInfo) => {
  selectedCardId.value = product.publicId
  selectedProductId.value = product.publicId
  currentStep.value = 1
}

const onCreateProduct = async (event: FormSubmitEvent<CreateProductSchema>) => {
  isCreating.value = true
  try {
    // Convert empty strings to null for optional fields
    const productData: CreateProductRequest = {
      name: event.data.name,
      brand: event.data.brand,
      category: event.data.category?.trim() || null,
      barcode: event.data.barcode?.trim() || null,
      notes: event.data.notes?.trim() || null,
      isEatable: event.data.isEatable,
      isFavorite: event.data.isFavorite
    }

    const response = await createProduct(productData)
    if (response.success && response.data) {
      selectedProductId.value = response.data.publicId
      currentStep.value = 1
    }
  } catch (error) {
    console.error('Product creation failed:', error)
  } finally {
    isCreating.value = false
  }
}
</script>
