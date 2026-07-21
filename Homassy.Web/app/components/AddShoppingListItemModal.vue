<template>
  <WizardDrawer
    :open="open"
    :title="t('pages.shoppingLists.addProduct.title')"
    icon="i-lucide-shopping-cart"
    :steps="stepperItems"
    :current-step="currentStep"
    :can-proceed="canProceed"
    :can-go-back="canGoBack"
    :loading="isCreatingItem"
    :override-footer="showCreateProduct || showCreateLocation"
    @update:open="onOpenChange"
    @previous="goPrevious"
    @next="goNext"
    @finish="finish"
  >
    <!-- Inline create sub-forms replace the stepper footer with a Create action. -->
    <template #footer>
      <UButton
        :label="t('common.backToSearch')"
        color="neutral"
        variant="ghost"
        icon="i-lucide-arrow-left"
        :disabled="isCreating || isCreatingShoppingLocation"
        @click="goPrevious"
      />
      <UButton
        v-if="showCreateProduct"
        :label="t('pages.addProduct.form.createButton')"
        color="primary"
        icon="i-lucide-plus"
        :loading="isCreating"
        @click="() => createProductFormRef?.submit()"
      />
      <UButton
        v-else-if="showCreateLocation"
        :label="t('pages.addProduct.shoppingLocation.form.createButton')"
        color="primary"
        icon="i-lucide-plus"
        :loading="isCreatingShoppingLocation"
        @click="() => createLocationFormRef?.submit()"
      />
    </template>

    <template #default>
      <!-- Step 0: Product Selection (mode-based) -->
      <div v-if="currentStep === 0" class="space-y-6">
        <!-- Product mode: search + create-product sub-view -->
        <template v-if="mode === 'product'">
          <PickOrCreate
            v-model:query="searchQuery"
            v-model:show-create="showCreateProduct"
            :placeholder="t('pages.addProduct.search.placeholder')"
            :create-label="t('pages.shoppingLists.addProduct.pick.createProduct', { name: searchQuery.trim() })"
            :filter-count="productFilterCount"
            @create="onStartCreateProduct"
          >
            <template #search-trailing>
              <BarcodeScannerButton v-if="showCameraButton" @scanned="handleSearchBarcodeScanned" />
            </template>

            <template #filters>
              <div role="group" :aria-label="t('pages.shoppingLists.addProduct.pick.properties')">
                <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ t('pages.shoppingLists.addProduct.pick.properties') }}</p>
                <div class="flex flex-wrap gap-2">
                  <UButton :label="t('pages.shoppingLists.addProduct.pick.favorite')" icon="i-lucide-star" size="sm" class="rounded-full" :color="productFavoriteFilter ? 'primary' : 'neutral'" :variant="productFavoriteFilter ? 'solid' : 'outline'" :aria-pressed="productFavoriteFilter" @click="productFavoriteFilter = !productFavoriteFilter" />
                  <UButton :label="t('pages.shoppingLists.addProduct.pick.eatable')" icon="i-lucide-utensils" size="sm" class="rounded-full" :color="productEatableFilter ? 'primary' : 'neutral'" :variant="productEatableFilter ? 'solid' : 'outline'" :aria-pressed="productEatableFilter" @click="productEatableFilter = !productEatableFilter" />
                </div>
              </div>
            </template>

            <template #chips>
              <UButton v-if="productFavoriteFilter" :label="t('pages.shoppingLists.addProduct.pick.favorite')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="productFavoriteFilter = false" />
              <UButton v-if="productEatableFilter" :label="t('pages.shoppingLists.addProduct.pick.eatable')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="productEatableFilter = false" />
            </template>

            <template #results>
              <div v-if="isSearching" class="space-y-2">
                <USkeleton class="h-14 w-full" />
                <USkeleton class="h-14 w-full" />
                <USkeleton class="h-14 w-full" />
              </div>
              <div v-else-if="searchQuery.trim() === ''" class="text-center py-10 text-sm text-gray-500">
                {{ t('pages.addProduct.search.startTyping') }}
              </div>
              <div v-else-if="filteredProductResults.length === 0" class="text-center py-8 text-sm text-gray-500">
                {{ productFilterCount > 0 ? t('pages.shoppingLists.addProduct.pick.noFilterMatch') : t('pages.addProduct.search.noResults') }}
              </div>
              <ul v-else class="flex flex-col gap-2">
                <li v-for="product in filteredProductResults" :key="product.publicId">
                  <button type="button" class="w-full flex items-center justify-between gap-3 rounded-lg border px-3 py-2.5 text-left transition" :class="selectedCardId === product.publicId ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40' : 'border-default hover:bg-elevated/50'" @click="onProductCardClick(product)">
                    <span class="flex min-w-0 flex-col">
                      <span class="flex items-center gap-1.5 truncate text-sm font-medium">
                        <UIcon v-if="product.isFavorite" name="i-lucide-star" class="h-3.5 w-3.5 shrink-0 text-primary-500" />
                        {{ product.name }}
                      </span>
                      <span class="truncate text-xs text-gray-500 dark:text-gray-400">{{ [product.brand, t(`enums.unit.${product.unit}`)].filter(Boolean).join(' · ') }}</span>
                    </span>
                    <UIcon v-if="selectedCardId === product.publicId" name="i-lucide-check" class="h-5 w-5 shrink-0 text-primary-500" />
                  </button>
                </li>
              </ul>
            </template>

            <template #create>
              <UForm
              ref="createProductFormRef"
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
                <USelectMenu
                  v-model="productFormData.category"
                  :items="categoryOptions"
                  value-key="value"
                  :placeholder="t('pages.addProduct.form.categoryPlaceholder')"
                  :disabled="isCreating"
                  class="w-full"
                />
              </UFormField>

              <UFormField :label="t('pages.addProduct.form.unit')" name="unit" required>
                <USelect
                  v-model="productFormData.unit"
                  :items="unitOptions"
                  :placeholder="t('pages.addProduct.form.unitPlaceholder')"
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
                  <BarcodeScannerButton v-if="showCameraButton" :disabled="isCreating" @scanned="handleBarcodeScanned" />
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
              </UForm>
            </template>
          </PickOrCreate>
        </template>

        <!-- Custom mode: custom name form -->
        <template v-else>
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
          </UForm>
        </template>
      </div>

      <!-- Step 1: Shopping Location (optional — "Next" proceeds without one) -->
      <div v-if="currentStep === 1" class="space-y-6">

        <PickOrCreate
          v-model:query="shoppingLocationSearchQuery"
          v-model:show-create="showCreateLocation"
          :placeholder="t('pages.addProduct.shoppingLocation.search.placeholder')"
          :create-label="t('pages.shoppingLists.addProduct.pick.createLocation', { name: shoppingLocationSearchQuery.trim() })"
          :filter-count="locationFilterCount"
          @create="onStartCreateLocation"
        >
          <template #filters>
            <FilterChipGroup
              v-if="locationCityOptions.length > 1"
              v-model="locationCityFilter"
              :label="t('pages.shoppingLists.addProduct.pick.city')"
              :options="locationCityOptions"
            />
            <div role="group" :aria-label="t('pages.shoppingLists.addProduct.pick.properties')">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ t('pages.shoppingLists.addProduct.pick.properties') }}</p>
              <UButton :label="t('pages.shoppingLists.addProduct.pick.shared')" icon="i-lucide-users" size="sm" class="rounded-full" :color="locationSharedFilter ? 'primary' : 'neutral'" :variant="locationSharedFilter ? 'solid' : 'outline'" :aria-pressed="locationSharedFilter" @click="locationSharedFilter = !locationSharedFilter" />
            </div>
          </template>

          <template #chips>
            <UButton v-if="locationCityFilter !== 'all'" :label="locationCityFilter" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="locationCityFilter = 'all'" />
            <UButton v-if="locationSharedFilter" :label="t('pages.shoppingLists.addProduct.pick.shared')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="locationSharedFilter = false" />
          </template>

          <template #results>
            <div v-if="isLoadingShoppingLocations" class="space-y-2">
              <USkeleton class="h-14 w-full" />
              <USkeleton class="h-14 w-full" />
              <USkeleton class="h-14 w-full" />
            </div>
            <div v-else-if="filteredShoppingLocations.length === 0" class="text-center py-8 text-sm text-gray-500">
              {{ t('pages.addProduct.shoppingLocation.search.noResults') }}
            </div>
            <ul v-else class="flex flex-col gap-2">
              <li v-for="location in paginatedShoppingLocations" :key="location.publicId">
                <button type="button" class="w-full flex items-center justify-between gap-3 rounded-lg border px-3 py-2.5 text-left transition" :class="selectedShoppingLocationCardId === location.publicId ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40' : 'border-default hover:bg-elevated/50'" @click="onShoppingLocationCardClick(location)">
                  <span class="flex min-w-0 flex-col">
                    <span class="flex items-center gap-1.5 truncate text-sm font-medium">
                      <span v-if="location.color" class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ backgroundColor: location.color }" />
                      {{ location.name }}
                      <UIcon v-if="location.isSharedWithFamily" name="i-lucide-users" class="h-3.5 w-3.5 shrink-0 text-primary-500" />
                    </span>
                    <span v-if="location.address || location.city" class="truncate text-xs text-gray-500 dark:text-gray-400">{{ [location.address, location.city].filter(Boolean).join(', ') }}</span>
                  </span>
                  <UIcon v-if="selectedShoppingLocationCardId === location.publicId" name="i-lucide-check" class="h-5 w-5 shrink-0 text-primary-500" />
                </button>
              </li>
            </ul>
            <div ref="shoppingLocationSentinelRef" class="h-1" />
          </template>

          <template #create>
            <div class="py-1">
              <UForm
                ref="createLocationFormRef"
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

                <UFormField :label="t('profile.shoppingLocations.storeTypes')" name="storeTypes">
                  <USelectMenu
                    v-model="shoppingLocationFormData.storeTypes"
                    :items="storeTypeOptions"
                    value-key="value"
                    multiple
                    :disabled="isCreatingShoppingLocation"
                    :placeholder="t('profile.shoppingLocations.storeTypesPlaceholder')"
                    :search-input="{ placeholder: t('common.search') }"
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
              </UForm>
            </div>
          </template>
        </PickOrCreate>
      </div>

      <!-- Step 2: Item Details -->
      <div v-if="currentStep === 2" class="space-y-6">
        <UForm
          ref="stepTwoFormRef"
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
            >
              <template v-if="productSelectionMode === 'product' && selectedProductUnit !== undefined" #trailing>
                <span class="text-sm text-gray-500 dark:text-gray-400">{{ t(`enums.unit.${selectedProductUnit}`) }}</span>
              </template>
            </UInput>
          </UFormField>

          <!-- Unit is only chosen for standalone/custom items; product items inherit it -->
          <UFormField
            v-if="productSelectionMode === 'custom'"
            :label="t('pages.shoppingLists.addProduct.item.unitLabel')"
            name="unit"
            required
          >
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
            <UInputDate v-model="itemFormData.deadlineAt" :locale="inputDateLocale" :disabled="isCreatingItem" class="w-full">
              <template #trailing>
                <UPopover>
                  <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" />
                  <template #content>
                    <UCalendar v-model="itemFormData.deadlineAt" :locale="inputDateLocale" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </UFormField>

          <UFormField :label="t('pages.shoppingLists.addProduct.item.dueLabel')" name="dueAt">
            <UInputDate v-model="itemFormData.dueAt" :locale="inputDateLocale" :disabled="isCreatingItem" class="w-full">
              <template #trailing>
                <UPopover>
                  <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" />
                  <template #content>
                    <UCalendar v-model="itemFormData.dueAt" :locale="inputDateLocale" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </UFormField>
        </UForm>
      </div>

      <!-- OpenFoodFacts Modal -->
      <AppDrawer
        :open="isOpenFoodFactsModalOpen"
        :title="t('pages.addProduct.openFoodFacts.modalTitle')"
        icon="i-lucide-download"
        fit="content"
        @update:open="(val) => { if (!val) isOpenFoodFactsModalOpen = false }"
      >
        <p class="text-sm text-muted">{{ t('pages.addProduct.openFoodFacts.modalDescription') }}</p>
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

        <template #footer>
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
        </template>
      </AppDrawer>

      <!-- Barcode Scanner Modal -->
      <BarcodeScannerModal :on-barcode-detected="activeBarcodeHandler" />
    </template>
  </WizardDrawer>
</template>

<script setup lang="ts">
import { z } from 'zod'
import { watchDebounced } from '@vueuse/core'
import { computed, nextTick, ref, watch, onMounted, onUnmounted } from 'vue'
import type { ProductInfo, CreateProductRequest } from '~/types/product'
import type { ShoppingLocationInfo, ShoppingLocationRequest } from '~/types/location'
import type { CreateShoppingListItemRequest } from '~/types/shoppingList'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import type { SelectValue } from '~/types/selectValue'
import { Unit, ProductCategory, SelectValueType, StoreType } from '~/types/enums'
import type { DateValue } from '@internationalized/date'
import type { FormSubmitEvent } from '#ui/types'

// Props & Emits
const props = defineProps<{
  open: boolean
  listId: string | null
  mode: 'product' | 'custom'
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

// Composables
const { t } = useI18n()
const { inputDateLocale } = useInputDateLocale()
const toast = useToast()
const { getProducts, createProduct } = useProductsApi()
const { getShoppingLocations, createShoppingLocation } = useLocationsApi()
const { createShoppingListItem } = useShoppingListApi()
const { getProductByBarcode } = useOpenFoodFactsApi()
const { getSelectValues } = useSelectValueApi()
const { showCameraButton } = useCameraAvailability()

// =========================
// Stepper State
// =========================
const currentStep = ref(0)
const stepperItems = computed(() => [
  { label: t('pages.shoppingLists.addProduct.stepLabels.step1') },
  { label: t('pages.shoppingLists.addProduct.stepLabels.step2') },
  { label: t('pages.shoppingLists.addProduct.stepLabels.step3') }
])

// --- Footer navigation -------------------------------------------------------
// Explicit Previous / Next / Finish drives the wizard; selecting a product or
// location only marks the selection and no longer auto-advances.
const stepTwoFormRef = ref()
const createProductFormRef = ref()
const createLocationFormRef = ref()

// Inline create sub-view for the location step (mirrors showCreateProduct).
const showCreateLocation = ref(false)

// Client-side pick-list filters (mirroring the inventory page's filter style).
const productFavoriteFilter = ref(false)
const productEatableFilter = ref(false)
const locationCityFilter = ref('all')
const locationSharedFilter = ref(false)

const productFilterCount = computed(() =>
  (productFavoriteFilter.value ? 1 : 0) + (productEatableFilter.value ? 1 : 0)
)
const locationFilterCount = computed(() =>
  (locationCityFilter.value !== 'all' ? 1 : 0) + (locationSharedFilter.value ? 1 : 0)
)

// Prefill the create form's name from what was typed, then reveal it.
const onStartCreateProduct = () => {
  productFormData.value.name = searchQuery.value.trim()
}
const onStartCreateLocation = () => {
  shoppingLocationFormData.value.name = shoppingLocationSearchQuery.value.trim()
}

const canGoBack = computed(() =>
  showCreateProduct.value || showCreateLocation.value || currentStep.value > 0
)

// Whether the current step's requirements are met to move on.
const canProceed = computed(() => {
  if (currentStep.value === 0) {
    // The create-product sub-view has its own "create" action.
    if (showCreateProduct.value) return false
    return props.mode === 'product'
      ? !!selectedProductId.value
      : customFormData.value.customName.trim().length >= 2
  }
  // Location is optional; only block while its create sub-view is open.
  if (currentStep.value === 1) return !showCreateLocation.value
  return true
})

const goPrevious = () => {
  if (showCreateProduct.value) {
    showCreateProduct.value = false
    return
  }
  if (showCreateLocation.value) {
    showCreateLocation.value = false
    return
  }
  if (currentStep.value > 0) currentStep.value = currentStep.value - 1
}

const goNext = () => {
  if (!canProceed.value) return
  if (currentStep.value === 0) {
    if (props.mode === 'product') {
      productSelectionMode.value = 'product'
      customProductName.value = ''
    } else {
      customProductName.value = customFormData.value.customName.trim()
      selectedProductId.value = null
      selectedProductUnit.value = undefined
      productSelectionMode.value = 'custom'
    }
    currentStep.value = 1
  } else if (currentStep.value === 1) {
    currentStep.value = 2
  }
}

// Submit the item form (step 2) from the footer's Finish button.
const finish = () => stepTwoFormRef.value?.submit()

// =========================
// Step 0: Product Selection State
// =========================
// Product mode toggles between the search sub-view and the create-product sub-view.
const showCreateProduct = ref(false)

// Product Selection Mode
const productSelectionMode = ref<'product' | 'custom' | null>(null)
const selectedProductId = ref<string | null>(null)
// Unit of the selected/created product — used to show a read-only suffix in product mode.
const selectedProductUnit = ref<Unit | undefined>(undefined)
const customProductName = ref<string>('')
const selectedCardId = ref<string | null>(null)

// Search Product
const searchQuery = ref('')
const searchResults = ref<ProductInfo[]>([])
const isSearching = ref(false)

// Search results narrowed by the active pick-list filters.
const filteredProductResults = computed(() =>
  searchResults.value.filter(p =>
    (!productFavoriteFilter.value || p.isFavorite)
    && (!productEatableFilter.value || p.isEatable)
  )
)

// Create Product
const productFormData = ref<CreateProductRequest>({
  name: '',
  brand: '',
  category: undefined,
  unit: Unit.Piece,
  barcode: '',
  isEatable: false,
  notes: '',
  isFavorite: false
})
const isCreating = ref(false)
const categoryOptionsRaw = ref<SelectValue[]>([])
const categoryOptions = computed(() => {
  return categoryOptionsRaw.value.map(cat => ({
    label: t(`enums.productCategory.${cat.text}`),
    value: parseInt(cat.text)
  }))
})
const isQueryingBarcode = ref(false)
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)
const isOpenFoodFactsModalOpen = ref(false)
const isImageLoading = ref(true)

// Custom Name
const customFormData = ref({
  customName: ''
})

// =========================
// Step 1: Shopping Location State
// =========================
const allShoppingLocations = ref<ShoppingLocationInfo[]>([])
const shoppingLocationSearchQuery = ref('')
const isLoadingShoppingLocations = ref(false)
const selectedShoppingLocationId = ref<string | null>(null)
const selectedShoppingLocationCardId = ref<string | null>(null)
const currentShoppingLocationPage = ref(1)
const shoppingLocationPageSize = 20
const loadingMoreShoppingLocations = ref(false)
const shoppingLocationSentinelRef = ref<HTMLElement | null>(null)

const shoppingLocationFormData = ref<ShoppingLocationRequest>({
  name: '',
  description: '',
  color: '',
  address: '',
  city: '',
  postalCode: '',
  country: '',
  website: '',
  googleMaps: '',
  storeTypes: [] as StoreType[],
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
  deadlineAt: null as DateValue | null,
  dueAt: null as DateValue | null
})
const isCreatingItem = ref(false)

// =========================
// Zod Schemas
// =========================
const createProductSchema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters').max(128, 'Name must not exceed 128 characters'),
  brand: z.string().min(2, 'Brand must be at least 2 characters').max(128, 'Brand must not exceed 128 characters'),
  category: z.nativeEnum(ProductCategory).optional(),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
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
  storeTypes: z.array(z.nativeEnum(StoreType)).optional().default([]),
  isSharedWithFamily: z.boolean().optional().default(true)
})

const createShoppingListItemSchema = z.object({
  quantity: z.number({ required_error: 'Quantity is required' }).min(0.001, 'Quantity must be greater than 0'),
  // Unit is only required for standalone/custom items; product items inherit the product's unit.
  unit: z.nativeEnum(Unit).optional(),
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

// Store-type multi-select options for the inline "create location" form.
const storeTypeOptions = computed(() => {
  return Object.entries(StoreType)
    .filter(([key]) => isNaN(Number(key)))
    .map(([_key, value]) => ({
      label: t(`enums.storeType.${value}`),
      value: value as StoreType
    }))
})

// City options for the location filter (distinct cities present in the data).
const locationCityOptions = computed(() => {
  const cities = Array.from(
    new Set(allShoppingLocations.value.map(l => l.city).filter((c): c is string => !!c))
  ).sort((a, b) => a.localeCompare(b, 'hu'))
  return [
    { label: t('pages.shoppingLists.addProduct.pick.allCities'), value: 'all' },
    ...cities.map(c => ({ label: c, value: c }))
  ]
})

const filteredShoppingLocations = computed(() => {
  const query = shoppingLocationSearchQuery.value.toLowerCase().trim()

  let result = allShoppingLocations.value

  if (query) {
    result = result.filter(location =>
      location.name?.toLowerCase().includes(query) ||
      location.description?.toLowerCase().includes(query) ||
      location.address?.toLowerCase().includes(query) ||
      location.city?.toLowerCase().includes(query)
    )
  }

  // Pick-list filters
  if (locationCityFilter.value !== 'all') {
    result = result.filter(location => location.city === locationCityFilter.value)
  }
  if (locationSharedFilter.value) {
    result = result.filter(location => location.isSharedWithFamily)
  }

  // Sort alphabetically by name
  result = result.sort((a, b) => {
    return a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu')
  })

  return result
})

const paginatedShoppingLocations = computed(() => {
  return filteredShoppingLocations.value.slice(
    0,
    currentShoppingLocationPage.value * shoppingLocationPageSize
  )
})

// Barcode scanner handler is context-aware: search sub-view vs create-product sub-view.
const activeBarcodeHandler = computed(() => {
  return showCreateProduct.value ? handleBarcodeScanned : handleSearchBarcodeScanned
})

// =========================
// Open/Reset lifecycle
// =========================
const resetState = () => {
  currentStep.value = 0
  showCreateProduct.value = false

  productSelectionMode.value = null
  selectedProductId.value = null
  selectedProductUnit.value = undefined
  customProductName.value = ''
  selectedCardId.value = null

  searchQuery.value = ''
  searchResults.value = []
  isSearching.value = false

  productFormData.value = {
    name: '',
    brand: '',
    category: undefined,
    unit: Unit.Piece,
    barcode: '',
    isEatable: false,
    notes: '',
    isFavorite: false
  }
  isQueryingBarcode.value = false
  openFoodFactsProduct.value = null
  isOpenFoodFactsModalOpen.value = false

  customFormData.value = { customName: '' }

  shoppingLocationSearchQuery.value = ''
  selectedShoppingLocationId.value = null
  selectedShoppingLocationCardId.value = null
  currentShoppingLocationPage.value = 1

  // Pick-or-create sub-views + filters
  showCreateProduct.value = false
  showCreateLocation.value = false
  productFavoriteFilter.value = false
  productEatableFilter.value = false
  locationCityFilter.value = 'all'
  locationSharedFilter.value = false

  shoppingLocationFormData.value = {
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
  }

  itemFormData.value = {
    quantity: 1,
    unit: undefined,
    note: '',
    deadlineAt: null,
    dueAt: null
  }
}

// Reset the wizard to a clean state each time the modal opens.
watch(() => props.open, (isOpen) => {
  if (isOpen) resetState()
})

const onOpenChange = (val: boolean) => {
  emit('update:open', val)
}

// Fetch category options once.
onMounted(async () => {
  const response = await getSelectValues(SelectValueType.ProductCategory)
  if (response.success && response.data) {
    categoryOptionsRaw.value = response.data
  }
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
        const sortedItems = response.data.items.sort((a, b) => {
          return a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu')
        })
        searchResults.value = sortedItems
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
  if (newStep === 1) {
    if (allShoppingLocations.value.length === 0) {
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
  }
})

// Reset pagination when search query changes
watch(shoppingLocationSearchQuery, () => {
  currentShoppingLocationPage.value = 1
})

// Intersection Observer for lazy loading shopping locations. The sentinel only exists
// while Step 1 is rendered, so (re)attach the observer whenever it appears in the DOM.
let shoppingLocationObserver: IntersectionObserver | null = null
watch(shoppingLocationSentinelRef, (sentinel) => {
  if (shoppingLocationObserver) {
    shoppingLocationObserver.disconnect()
    shoppingLocationObserver = null
  }

  if (sentinel && typeof IntersectionObserver !== 'undefined') {
    shoppingLocationObserver = new IntersectionObserver(
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
    shoppingLocationObserver.observe(sentinel)
  }
})

onUnmounted(() => {
  if (shoppingLocationObserver) {
    shoppingLocationObserver.disconnect()
    shoppingLocationObserver = null
  }
})

// =========================
// Step 0: Product Selection Handlers
// =========================

// Product Card Click
const onProductCardClick = (product: ProductInfo) => {
  // Toggle selection; advancing is done via the footer "Next" button.
  const alreadySelected = selectedCardId.value === product.publicId
  selectedCardId.value = alreadySelected ? null : product.publicId
  selectedProductId.value = alreadySelected ? null : product.publicId
  selectedProductUnit.value = alreadySelected ? undefined : product.unit
  customProductName.value = ''
  productSelectionMode.value = 'product'
}

// Create Product
const onCreateProduct = async (event: FormSubmitEvent<typeof createProductSchema>) => {
  isCreating.value = true
  try {
    const productData: CreateProductRequest = {
      name: event.data.name,
      brand: event.data.brand,
      category: event.data.category || undefined,
      unit: event.data.unit,
      barcode: event.data.barcode || undefined,
      isEatable: event.data.isEatable || false,
      notes: event.data.notes || undefined,
      isFavorite: event.data.isFavorite || false
    }

    const response = await createProduct(productData)
    if (response.success && response.data) {
      selectedProductId.value = response.data.publicId
      selectedProductUnit.value = response.data.unit
      customProductName.value = ''
      productSelectionMode.value = 'product'
      showCreateProduct.value = false
      currentStep.value = 1
    }
  } catch (error) {
    console.error('Product creation failed:', error)
  } finally {
    isCreating.value = false
  }
}

// OpenFoodFacts Query
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

// Barcode scanner handler (for Create Product sub-view)
const handleBarcodeScanned = (barcode: string) => {
  productFormData.value.barcode = barcode
  // Auto-trigger OpenFoodFacts query
  nextTick(() => {
    queryOpenFoodFacts()
  })
}

// Barcode scanner handler (for Search sub-view)
const handleSearchBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Custom Name Submit
const onCustomNameSubmit = (event: FormSubmitEvent<typeof customNameSchema>) => {
  customProductName.value = event.data.customName
  selectedProductId.value = null
  selectedProductUnit.value = undefined
  productSelectionMode.value = 'custom'
  currentStep.value = 1
}

// =========================
// Step 1: Shopping Location Handlers
// =========================

const onShoppingLocationCardClick = (location: ShoppingLocationInfo) => {
  // Toggle selection; advancing is done via the footer "Next" button. Leaving it
  // unselected is allowed (location is optional) — Next then proceeds without one.
  const alreadySelected = selectedShoppingLocationCardId.value === location.publicId
  selectedShoppingLocationCardId.value = alreadySelected ? null : location.publicId
  selectedShoppingLocationId.value = alreadySelected ? null : location.publicId
}

const onCreateShoppingLocation = async (event: FormSubmitEvent<typeof createShoppingLocationSchema>) => {
  isCreatingShoppingLocation.value = true
  try {
    const locationData: ShoppingLocationRequest = {
      name: event.data.name,
      description: event.data.description || undefined,
      color: event.data.color || undefined,
      address: event.data.address || undefined,
      city: event.data.city || undefined,
      postalCode: event.data.postalCode || undefined,
      country: event.data.country || undefined,
      website: event.data.website || undefined,
      googleMaps: event.data.googleMaps || undefined,
      storeTypes: event.data.storeTypes ?? [],
      isSharedWithFamily: event.data.isSharedWithFamily ?? true
    }

    const response = await createShoppingLocation(locationData)
    if (response.success && response.data) {
      selectedShoppingLocationId.value = response.data.publicId
      selectedShoppingLocationCardId.value = response.data.publicId
      showCreateLocation.value = false
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
  if (!props.listId) {
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
      shoppingListPublicId: props.listId,
      productPublicId: productSelectionMode.value === 'product' ? selectedProductId.value! : undefined,
      customName: productSelectionMode.value === 'custom' ? customProductName.value : undefined,
      shoppingLocationPublicId: selectedShoppingLocationId.value || undefined,
      quantity: event.data.quantity,
      // Product items inherit the product's unit server-side; only send it for custom items.
      unit: productSelectionMode.value === 'custom' ? event.data.unit : undefined,
      note: event.data.note?.trim() || undefined,
      deadlineAt: deadlineAtString,
      dueAt: dueAtString
    }

    const response = await createShoppingListItem(requestData)

    if (response.success && response.data) {
      // The new item arrives on the list via the realtime socket (ItemUpserted),
      // so just close the modal — no navigation or manual refresh needed.
      emit('update:open', false)
    }
  } catch (error) {
    console.error('Shopping list item creation failed:', error)
  } finally {
    isCreatingItem.value = false
  }
}
</script>
