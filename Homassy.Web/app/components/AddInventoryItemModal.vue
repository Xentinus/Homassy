<template>
  <WizardDrawer
    :open="open"
    :title="t('pages.addProduct.title')"
    icon="i-lucide-package-plus"
    :steps="stepperItems"
    :current-step="currentStep"
    :can-proceed="canProceed"
    :can-go-back="canGoBack"
    :finish-disabled="finishDisabled"
    :finish-label="finishLabel"
    :loading="isCreatingInventory || isCreatingMultiple"
    :override-footer="showCreateProduct || showCreateStorageLocation || showCreateShoppingLocation"
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
        :disabled="isCreating || isCreatingLocation || isCreatingShoppingLocation"
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
        v-else-if="showCreateStorageLocation"
        :label="t('pages.addProduct.location.form.createButton')"
        color="primary"
        icon="i-lucide-plus"
        :loading="isCreatingLocation"
        @click="() => createStorageLocationFormRef?.submit()"
      />
      <UButton
        v-else-if="showCreateShoppingLocation"
        :label="t('pages.addProduct.shoppingLocation.form.createButton')"
        color="primary"
        icon="i-lucide-plus"
        :loading="isCreatingShoppingLocation"
        @click="() => createShoppingLocationFormRef?.submit()"
      />
    </template>

    <template #default>
      <!-- Step 0: Product selection (search or create) -->
      <div v-if="currentStep === 0" class="space-y-6">
        <PickOrCreate
          v-model:query="searchQuery"
          v-model:show-create="showCreateProduct"
          :placeholder="t('pages.addProduct.search.placeholder')"
          :create-label="t('pages.addProduct.pick.createProduct', { name: searchQuery.trim() })"
          :filter-count="productFilterCount"
          @create="onStartCreateProduct"
        >
          <template #search-trailing>
            <BarcodeScannerButton v-if="showCameraButton" @scanned="handleSearchBarcodeScanned" />
          </template>

          <template #filters>
            <div role="group" :aria-label="t('pages.addProduct.pick.properties')">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ t('pages.addProduct.pick.properties') }}</p>
              <div class="flex flex-wrap gap-2">
                <UButton :label="t('pages.addProduct.pick.favorite')" icon="i-lucide-star" size="sm" class="rounded-full" :color="productFavoriteFilter ? 'primary' : 'neutral'" :variant="productFavoriteFilter ? 'solid' : 'outline'" :aria-pressed="productFavoriteFilter" @click="productFavoriteFilter = !productFavoriteFilter" />
                <UButton :label="t('pages.addProduct.pick.eatable')" icon="i-lucide-utensils" size="sm" class="rounded-full" :color="productEatableFilter ? 'primary' : 'neutral'" :variant="productEatableFilter ? 'solid' : 'outline'" :aria-pressed="productEatableFilter" @click="productEatableFilter = !productEatableFilter" />
              </div>
            </div>
          </template>

          <template #chips>
            <UButton v-if="productFavoriteFilter" :label="t('pages.addProduct.pick.favorite')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="productFavoriteFilter = false" />
            <UButton v-if="productEatableFilter" :label="t('pages.addProduct.pick.eatable')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="productEatableFilter = false" />
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
              {{ productFilterCount > 0 ? t('pages.addProduct.pick.noFilterMatch') : t('pages.addProduct.search.noResults') }}
            </div>
            <ul v-else class="flex flex-col gap-2">
              <li v-for="product in filteredProductResults" :key="product.publicId">
                <button type="button" class="w-full flex items-center justify-between gap-3 rounded-lg border px-3 py-2.5 text-left transition" :class="selectedProductId === product.publicId ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40' : 'border-default hover:bg-elevated/50'" @click="onProductCardClick(product)">
                  <span class="flex min-w-0 flex-col">
                    <span class="flex items-center gap-1.5 truncate text-sm font-medium">
                      <UIcon v-if="product.isFavorite" name="i-lucide-star" class="h-3.5 w-3.5 shrink-0 text-primary-500" />
                      {{ product.name }}
                    </span>
                    <span class="truncate text-xs text-gray-500 dark:text-gray-400">{{ [product.brand, t(`enums.unit.${product.unit}`)].filter(Boolean).join(' · ') }}</span>
                  </span>
                  <UIcon v-if="selectedProductId === product.publicId" name="i-lucide-check" class="h-5 w-5 shrink-0 text-primary-500" />
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
                <UInput v-model="productFormData.name" :placeholder="t('pages.addProduct.form.namePlaceholder')" :disabled="isCreating" class="w-full" />
              </UFormField>

              <UFormField :label="t('pages.addProduct.form.brand')" name="brand" required>
                <UInput v-model="productFormData.brand" :placeholder="t('pages.addProduct.form.brandPlaceholder')" :disabled="isCreating" class="w-full" />
              </UFormField>

              <UFormField :label="t('pages.addProduct.form.category')" name="category">
                <USelectMenu v-model="productFormData.category" :items="categoryOptions" value-key="value" :placeholder="t('pages.addProduct.form.categoryPlaceholder')" :disabled="isCreating" class="w-full" />
              </UFormField>

              <UFormField :label="t('pages.addProduct.form.unit')" name="unit" required>
                <USelect v-model="productFormData.unit" :items="unitOptions" :placeholder="t('pages.addProduct.form.unitPlaceholder')" :disabled="isCreating" class="w-full" />
              </UFormField>

              <UFormField :label="t('pages.addProduct.form.barcode')" name="barcode">
                <UFieldGroup size="md" orientation="horizontal" class="w-full">
                  <UInput v-model="productFormData.barcode" :placeholder="t('pages.addProduct.form.barcodePlaceholder')" :disabled="isCreating" inputmode="numeric" pattern="[0-9]*" class="flex-1" />
                  <BarcodeScannerButton v-if="showCameraButton" :disabled="isCreating" @scanned="handleBarcodeScanned" />
                  <UButton :label="t('pages.addProduct.form.barcodeQuery')" icon="i-lucide-barcode" color="primary" size="sm" :loading="isQueryingBarcode" :disabled="isCreating" @click="handleBarcodeQuery" />
                </UFieldGroup>
              </UFormField>

              <UFormField name="isEatable">
                <UCheckbox v-model="productFormData.isEatable" :label="t('pages.addProduct.form.isEatableLabel')" :disabled="isCreating" />
              </UFormField>

              <UFormField :label="t('pages.addProduct.form.notes')" name="notes">
                <UTextarea v-model="productFormData.notes" :placeholder="t('pages.addProduct.form.notesPlaceholder')" :disabled="isCreating" :rows="3" class="w-full" />
              </UFormField>

              <UFormField name="isFavorite">
                <UCheckbox v-model="productFormData.isFavorite" :label="t('pages.addProduct.form.isFavoriteLabel')" :disabled="isCreating" />
              </UFormField>
            </UForm>
          </template>
        </PickOrCreate>
      </div>

      <!-- Step 1: Storage location (required) -->
      <div v-if="currentStep === 1" class="space-y-6">
        <PickOrCreate
          v-model:query="storageLocationSearchQuery"
          v-model:show-create="showCreateStorageLocation"
          :placeholder="t('pages.addProduct.location.search.placeholder')"
          :create-label="t('pages.addProduct.pick.createLocation', { name: storageLocationSearchQuery.trim() })"
          :filter-count="storageFilterCount"
          @create="onStartCreateStorageLocation"
        >
          <template #filters>
            <div role="group" :aria-label="t('pages.addProduct.pick.properties')">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ t('pages.addProduct.pick.properties') }}</p>
              <div class="flex flex-wrap gap-2">
                <UButton :label="t('pages.addProduct.pick.freezer')" icon="i-lucide-snowflake" size="sm" class="rounded-full" :color="storageFreezerFilter ? 'primary' : 'neutral'" :variant="storageFreezerFilter ? 'solid' : 'outline'" :aria-pressed="storageFreezerFilter" @click="storageFreezerFilter = !storageFreezerFilter" />
                <UButton :label="t('pages.addProduct.pick.shared')" icon="i-lucide-users" size="sm" class="rounded-full" :color="storageSharedFilter ? 'primary' : 'neutral'" :variant="storageSharedFilter ? 'solid' : 'outline'" :aria-pressed="storageSharedFilter" @click="storageSharedFilter = !storageSharedFilter" />
              </div>
            </div>
          </template>

          <template #chips>
            <UButton v-if="storageFreezerFilter" :label="t('pages.addProduct.pick.freezer')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="storageFreezerFilter = false" />
            <UButton v-if="storageSharedFilter" :label="t('pages.addProduct.pick.shared')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="storageSharedFilter = false" />
          </template>

          <template #results>
            <div v-if="isLoadingStorageLocations" class="space-y-2">
              <USkeleton class="h-14 w-full" />
              <USkeleton class="h-14 w-full" />
              <USkeleton class="h-14 w-full" />
            </div>
            <div v-else-if="filteredStorageLocations.length === 0" class="text-center py-8 text-sm text-gray-500">
              {{ storageFilterCount > 0 ? t('pages.addProduct.pick.noFilterMatch') : t('pages.addProduct.location.search.noResults') }}
            </div>
            <ul v-else class="flex flex-col gap-2">
              <li v-for="location in paginatedStorageLocations" :key="location.publicId">
                <button type="button" class="w-full flex items-center justify-between gap-3 rounded-lg border px-3 py-2.5 text-left transition" :class="selectedStorageLocationId === location.publicId ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40' : 'border-default hover:bg-elevated/50'" @click="onStorageLocationCardClick(location)">
                  <span class="flex min-w-0 flex-col">
                    <span class="flex items-center gap-1.5 truncate text-sm font-medium">
                      <span v-if="location.color" class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ backgroundColor: location.color }" />
                      {{ location.name }}
                      <UIcon v-if="location.isFreezer" name="i-lucide-snowflake" class="h-3.5 w-3.5 shrink-0 text-blue-500" />
                      <UIcon v-if="location.isSharedWithFamily" name="i-lucide-users" class="h-3.5 w-3.5 shrink-0 text-primary-500" />
                    </span>
                    <span v-if="location.description" class="truncate text-xs text-gray-500 dark:text-gray-400">{{ location.description }}</span>
                  </span>
                  <UIcon v-if="selectedStorageLocationId === location.publicId" name="i-lucide-check" class="h-5 w-5 shrink-0 text-primary-500" />
                </button>
              </li>
            </ul>
            <div ref="storageSentinelRef" class="h-1" />
          </template>

          <template #create>
            <div class="py-1">
              <UForm
                ref="createStorageLocationFormRef"
                :schema="createLocationSchema"
                :state="locationFormData"
                class="space-y-4"
                @submit="onCreateLocation"
              >
                <UFormField :label="t('pages.addProduct.location.form.name')" name="name" required>
                  <UInput v-model="locationFormData.name" :placeholder="t('pages.addProduct.location.form.namePlaceholder')" :disabled="isCreatingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.location.form.description')" name="description">
                  <UTextarea v-model="locationFormData.description" :placeholder="t('pages.addProduct.location.form.descriptionPlaceholder')" :disabled="isCreatingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.location.form.color')" name="color">
                  <div class="flex items-center gap-3">
                    <UPopover>
                      <UButton color="neutral" variant="outline" :disabled="isCreatingLocation">
                        <div class="flex items-center gap-2">
                          <div v-if="locationFormData.color" class="w-4 h-4 rounded" :style="{ backgroundColor: locationFormData.color }" />
                          <span>{{ locationFormData.color || t('pages.addProduct.location.form.chooseColor') }}</span>
                        </div>
                      </UButton>
                      <template #content>
                        <UColorPicker v-model="locationFormData.color" />
                      </template>
                    </UPopover>
                    <UButton v-if="locationFormData.color" icon="i-lucide-x" color="neutral" variant="ghost" size="sm" :disabled="isCreatingLocation" @click="locationFormData.color = ''" />
                  </div>
                </UFormField>

                <UFormField name="isFreezer">
                  <UCheckbox v-model="locationFormData.isFreezer" :label="t('pages.addProduct.location.form.isFreezerLabel')" :disabled="isCreatingLocation" />
                </UFormField>

                <UFormField name="isSharedWithFamily">
                  <UCheckbox v-model="locationFormData.isSharedWithFamily" :label="t('pages.addProduct.location.form.isSharedWithFamilyLabel')" :disabled="isCreatingLocation" />
                </UFormField>

              </UForm>
            </div>
          </template>
        </PickOrCreate>
      </div>

      <!-- Step 2: Shopping location (optional — "Next" proceeds without one) -->
      <div v-if="currentStep === 2" class="space-y-6">
        <PickOrCreate
          v-model:query="shoppingLocationSearchQuery"
          v-model:show-create="showCreateShoppingLocation"
          :placeholder="t('pages.addProduct.shoppingLocation.search.placeholder')"
          :create-label="t('pages.addProduct.pick.createShoppingLocation', { name: shoppingLocationSearchQuery.trim() })"
          :filter-count="shoppingFilterCount"
          @create="onStartCreateShoppingLocation"
        >
          <template #filters>
            <FilterChipGroup
              v-if="shoppingCityOptions.length > 1"
              v-model="shoppingCityFilter"
              :label="t('pages.addProduct.pick.city')"
              :options="shoppingCityOptions"
            />
            <div role="group" :aria-label="t('pages.addProduct.pick.properties')">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ t('pages.addProduct.pick.properties') }}</p>
              <UButton :label="t('pages.addProduct.pick.shared')" icon="i-lucide-users" size="sm" class="rounded-full" :color="shoppingSharedFilter ? 'primary' : 'neutral'" :variant="shoppingSharedFilter ? 'solid' : 'outline'" :aria-pressed="shoppingSharedFilter" @click="shoppingSharedFilter = !shoppingSharedFilter" />
            </div>
          </template>

          <template #chips>
            <UButton v-if="shoppingCityFilter !== 'all'" :label="shoppingCityFilter" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="shoppingCityFilter = 'all'" />
            <UButton v-if="shoppingSharedFilter" :label="t('pages.addProduct.pick.shared')" size="xs" color="primary" variant="soft" trailing-icon="i-lucide-x" class="rounded-full" @click="shoppingSharedFilter = false" />
          </template>

          <template #results>
            <div v-if="isLoadingShoppingLocations" class="space-y-2">
              <USkeleton class="h-14 w-full" />
              <USkeleton class="h-14 w-full" />
              <USkeleton class="h-14 w-full" />
            </div>
            <div v-else-if="filteredShoppingLocations.length === 0" class="text-center py-8 text-sm text-gray-500">
              {{ shoppingFilterCount > 0 ? t('pages.addProduct.pick.noFilterMatch') : t('pages.addProduct.shoppingLocation.search.noResults') }}
            </div>
            <ul v-else class="flex flex-col gap-2">
              <li v-for="location in paginatedShoppingLocations" :key="location.publicId">
                <button type="button" class="w-full flex items-center justify-between gap-3 rounded-lg border px-3 py-2.5 text-left transition" :class="selectedShoppingLocationId === location.publicId ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40' : 'border-default hover:bg-elevated/50'" @click="onShoppingLocationCardClick(location)">
                  <span class="flex min-w-0 flex-col">
                    <span class="flex items-center gap-1.5 truncate text-sm font-medium">
                      <span v-if="location.color" class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ backgroundColor: location.color }" />
                      {{ location.name }}
                      <UIcon v-if="location.isSharedWithFamily" name="i-lucide-users" class="h-3.5 w-3.5 shrink-0 text-primary-500" />
                    </span>
                    <span v-if="location.address || location.city" class="truncate text-xs text-gray-500 dark:text-gray-400">{{ [location.address, location.city].filter(Boolean).join(', ') }}</span>
                  </span>
                  <UIcon v-if="selectedShoppingLocationId === location.publicId" name="i-lucide-check" class="h-5 w-5 shrink-0 text-primary-500" />
                </button>
              </li>
            </ul>
            <div ref="shoppingSentinelRef" class="h-1" />
          </template>

          <template #create>
            <div class="py-1">
              <UForm
                ref="createShoppingLocationFormRef"
                :schema="createShoppingLocationSchema"
                :state="shoppingLocationFormData"
                class="space-y-4"
                @submit="onCreateShoppingLocation"
              >
                <UFormField :label="t('pages.addProduct.shoppingLocation.form.name')" name="name" required>
                  <UInput v-model="shoppingLocationFormData.name" :placeholder="t('pages.addProduct.shoppingLocation.form.namePlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.description')" name="description">
                  <UTextarea v-model="shoppingLocationFormData.description" :placeholder="t('pages.addProduct.shoppingLocation.form.descriptionPlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.address')" name="address">
                  <UInput v-model="shoppingLocationFormData.address" :placeholder="t('pages.addProduct.shoppingLocation.form.addressPlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                </UFormField>

                <div class="grid grid-cols-2 gap-4">
                  <UFormField :label="t('pages.addProduct.shoppingLocation.form.city')" name="city">
                    <UInput v-model="shoppingLocationFormData.city" :placeholder="t('pages.addProduct.shoppingLocation.form.cityPlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                  </UFormField>

                  <UFormField :label="t('pages.addProduct.shoppingLocation.form.postalCode')" name="postalCode">
                    <UInput v-model="shoppingLocationFormData.postalCode" :placeholder="t('pages.addProduct.shoppingLocation.form.postalCodePlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                  </UFormField>
                </div>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.country')" name="country">
                  <UInput v-model="shoppingLocationFormData.country" :placeholder="t('pages.addProduct.shoppingLocation.form.countryPlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.website')" name="website">
                  <UInput v-model="shoppingLocationFormData.website" type="url" :placeholder="t('pages.addProduct.shoppingLocation.form.websitePlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.googleMaps')" name="googleMaps">
                  <UInput v-model="shoppingLocationFormData.googleMaps" type="url" :placeholder="t('pages.addProduct.shoppingLocation.form.googleMapsPlaceholder')" :disabled="isCreatingShoppingLocation" class="w-full" />
                </UFormField>

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.color')" name="color">
                  <div class="flex items-center gap-3">
                    <UPopover>
                      <UButton color="neutral" variant="outline" :disabled="isCreatingShoppingLocation">
                        <div class="flex items-center gap-2">
                          <div v-if="shoppingLocationFormData.color" class="w-4 h-4 rounded" :style="{ backgroundColor: shoppingLocationFormData.color }" />
                          <span>{{ shoppingLocationFormData.color || t('pages.addProduct.shoppingLocation.form.chooseColor') }}</span>
                        </div>
                      </UButton>
                      <template #content>
                        <UColorPicker v-model="shoppingLocationFormData.color" />
                      </template>
                    </UPopover>
                    <UButton v-if="shoppingLocationFormData.color" icon="i-lucide-x" color="neutral" variant="ghost" size="sm" :disabled="isCreatingShoppingLocation" @click="shoppingLocationFormData.color = ''" />
                  </div>
                </UFormField>

                <UFormField name="isSharedWithFamily">
                  <UCheckbox v-model="shoppingLocationFormData.isSharedWithFamily" :label="t('pages.addProduct.shoppingLocation.form.isSharedWithFamilyLabel')" :disabled="isCreatingShoppingLocation" />
                </UFormField>

              </UForm>
            </div>
          </template>
        </PickOrCreate>
      </div>

      <!-- Step 3: Create inventory (bulk / individual) -->
      <div v-if="currentStep === 3" class="space-y-6">
        <!-- Mode selection -->
        <div v-if="inventoryHandlingMode === null" class="space-y-4">
          <h3 class="text-lg font-semibold">{{ t('pages.addProduct.inventory.modeSelection.title') }}</h3>
          <p class="text-sm text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.modeSelection.description') }}</p>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-2">
            <button
              type="button"
              class="p-6 border-2 border-gray-300 dark:border-gray-700 rounded-lg hover:border-primary-500 dark:hover:border-primary-400 transition-colors text-left"
              @click="inventoryHandlingMode = 'bulk'"
            >
              <div class="flex items-center gap-3 mb-2">
                <UIcon name="i-lucide-layers" class="h-6 w-6 text-primary-500" />
                <span class="text-lg font-semibold">{{ t('pages.addProduct.inventory.modeSelection.bulk') }}</span>
              </div>
              <p class="text-sm text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.modeSelection.bulkDescription') }}</p>
            </button>

            <button
              type="button"
              class="p-6 border-2 border-gray-300 dark:border-gray-700 rounded-lg hover:border-primary-500 dark:hover:border-primary-400 transition-colors text-left"
              @click="inventoryHandlingMode = 'individual'"
            >
              <div class="flex items-center gap-3 mb-2">
                <UIcon name="i-lucide-grid-2x2" class="h-6 w-6 text-primary-500" />
                <span class="text-lg font-semibold">{{ t('pages.addProduct.inventory.modeSelection.individual') }}</span>
              </div>
              <p class="text-sm text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.modeSelection.individualDescription') }}</p>
            </button>
          </div>
        </div>

        <!-- Bulk mode -->
        <div v-else-if="inventoryHandlingMode === 'bulk'" class="space-y-6">
          <h3 class="text-lg font-semibold">{{ t('pages.addProduct.inventory.bulk.title') }}</h3>

          <UForm :schema="createInventorySchema" :state="inventoryFormData" class="space-y-4">
            <UFormField :label="t('pages.addProduct.inventory.form.quantity')" name="quantity" required>
              <UInput v-model.number="inventoryFormData.quantity" type="number" step="0.01" min="0.01" :placeholder="t('pages.addProduct.inventory.form.quantityPlaceholder')" :disabled="isCreatingInventory" class="w-full">
                <template v-if="productUnitLabel" #trailing>
                  <span class="text-sm text-gray-500 dark:text-gray-400">{{ productUnitLabel }}</span>
                </template>
              </UInput>
            </UFormField>

            <UFormField :label="t('pages.addProduct.inventory.form.expirationAt')" name="expirationAt">
              <UInputDate ref="expirationDateInput" v-model="inventoryFormData.expirationAt" :locale="inputDateLocale" :disabled="isCreatingInventory" class="w-full">
                <template #trailing>
                  <UPopover :reference="expirationDateInput?.inputsRef[0]?.$el">
                    <UButton color="neutral" variant="link" size="sm" icon="i-lucide-calendar" aria-label="Select a date" class="px-0" :disabled="isCreatingInventory" />
                    <template #content>
                      <UCalendar v-model="inventoryFormData.expirationAt" :locale="inputDateLocale" class="p-2" />
                    </template>
                  </UPopover>
                </template>
              </UInputDate>
            </UFormField>

            <UFormField :label="t('pages.addProduct.inventory.form.price')" name="price">
              <UInput v-model.number="inventoryFormData.price" type="number" step="0.01" min="0" :placeholder="t('pages.addProduct.inventory.form.pricePlaceholder')" :disabled="isCreatingInventory" class="w-full" />
            </UFormField>

            <UFormField v-if="inventoryFormData.price !== undefined && inventoryFormData.price > 0" :label="t('pages.addProduct.inventory.form.currency')" name="currency">
              <USelect v-model="inventoryFormData.currency" :items="currencyOptions" :placeholder="t('pages.addProduct.inventory.form.currencyPlaceholder')" :disabled="isCreatingInventory" class="w-full" />
            </UFormField>

            <UFormField :label="t('pages.addProduct.inventory.form.receiptNumber')" name="receiptNumber">
              <UInput v-model="inventoryFormData.receiptNumber" :placeholder="t('pages.addProduct.inventory.form.receiptNumberPlaceholder')" :disabled="isCreatingInventory" class="w-full" />
            </UFormField>

            <UFormField :label="t('pages.addProduct.inventory.form.isSharedWithFamily')" name="isSharedWithFamily">
              <UCheckbox v-model="inventoryFormData.isSharedWithFamily" :label="t('pages.addProduct.inventory.form.isSharedWithFamilyLabel')" :disabled="isCreatingInventory" />
            </UFormField>
          </UForm>

          <!-- Live preview -->
          <div v-if="isBulkFormValid" class="p-4 border border-primary-500 dark:border-primary-400 rounded-lg bg-primary-50 dark:bg-primary-900/20">
            <h4 class="text-sm font-semibold mb-3 flex items-center gap-2">
              <UIcon name="i-lucide-eye" class="h-4 w-4" />
              {{ t('pages.addProduct.inventory.preview.title') }}
            </h4>
            <div class="space-y-2 text-sm">
              <div class="flex justify-between">
                <span class="text-gray-600 dark:text-gray-400">{{ t('common.quantity') }}:</span>
                <span class="font-medium">{{ inventoryFormData.quantity }} {{ inventoryFormData.unit ? t(`enums.unit.${inventoryFormData.unit}`) : '' }}</span>
              </div>
              <div v-if="inventoryFormData.expirationAt" class="flex justify-between">
                <span class="text-gray-600 dark:text-gray-400">{{ t('common.expirationDate') }}:</span>
                <span class="font-medium">{{ formatCalendarDate(inventoryFormData.expirationAt) }}</span>
              </div>
              <div v-if="inventoryFormData.price && inventoryFormData.price > 0" class="flex justify-between">
                <span class="text-gray-600 dark:text-gray-400">{{ t('common.price') }}:</span>
                <span class="font-medium">{{ inventoryFormData.price }} {{ inventoryFormData.currency ? t(`enums.currency.${inventoryFormData.currency}`) : '' }}</span>
              </div>
              <div v-if="inventoryFormData.receiptNumber" class="flex justify-between">
                <span class="text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.form.receiptNumber') }}:</span>
                <span class="font-medium">{{ inventoryFormData.receiptNumber }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.form.isSharedWithFamily') }}:</span>
                <UIcon :name="inventoryFormData.isSharedWithFamily ? 'i-lucide-check' : 'i-lucide-x'" :class="inventoryFormData.isSharedWithFamily ? 'text-green-500' : 'text-gray-400'" class="h-5 w-5" />
              </div>
            </div>
          </div>
        </div>

        <!-- Individual mode -->
        <div v-else-if="inventoryHandlingMode === 'individual'" class="space-y-6">
          <h3 class="text-lg font-semibold">{{ t('pages.addProduct.inventory.individual.title') }}</h3>

          <!-- Shared context -->
          <div class="p-4 bg-gray-50 dark:bg-gray-800 rounded-lg space-y-3">
            <h4 class="text-sm font-semibold flex items-center gap-2">
              <UIcon name="i-lucide-info" class="h-4 w-4" />
              {{ t('pages.addProduct.inventory.sharedContext.title') }}
            </h4>
            <div class="flex flex-wrap gap-2">
              <UBadge color="primary" variant="soft" size="md">
                <span class="flex items-center gap-1">
                  <UIcon name="i-lucide-map-pin" class="h-3 w-3" />
                  {{ getStorageLocationName() }}
                </span>
              </UBadge>
              <UBadge v-if="selectedShoppingLocationId" color="secondary" variant="soft" size="md">
                <span class="flex items-center gap-1">
                  <UIcon name="i-lucide-shopping-cart" class="h-3 w-3" />
                  {{ getShoppingLocationName() }}
                </span>
              </UBadge>
            </div>

            <UFormField :label="t('pages.addProduct.inventory.form.isSharedWithFamily')" name="isSharedWithFamily">
              <UCheckbox v-model="inventoryFormData.isSharedWithFamily" :label="t('pages.addProduct.inventory.form.isSharedWithFamilyLabel')" />
            </UFormField>
          </div>

          <!-- Add item form -->
          <div class="p-6 border-2 border-dashed border-gray-300 dark:border-gray-700 rounded-lg">
            <h4 class="text-md font-semibold mb-4">{{ t('pages.addProduct.inventory.addItemForm.title') }}</h4>

            <UForm :schema="individualItemSchema" :state="inventoryFormData" class="space-y-4" @submit="onAddIndividualItem">
              <UFormField :label="t('pages.addProduct.inventory.form.quantity')" name="quantity" required>
                <UInput v-model.number="inventoryFormData.quantity" type="number" step="0.01" min="0.01" :placeholder="t('pages.addProduct.inventory.form.quantityPlaceholder')" class="w-full">
                  <template v-if="productUnitLabel" #trailing>
                    <span class="text-sm text-gray-500 dark:text-gray-400">{{ productUnitLabel }}</span>
                  </template>
                </UInput>
              </UFormField>

              <UFormField :label="t('pages.addProduct.inventory.form.expirationAt')" name="expirationAt">
                <UInputDate ref="expirationDateInput" v-model="inventoryFormData.expirationAt" :locale="inputDateLocale" class="w-full">
                  <template #trailing>
                    <UPopover :reference="expirationDateInput?.inputsRef[0]?.$el">
                      <UButton color="neutral" variant="link" size="sm" icon="i-lucide-calendar" aria-label="Select a date" class="px-0" />
                      <template #content>
                        <UCalendar v-model="inventoryFormData.expirationAt" :locale="inputDateLocale" class="p-2" />
                      </template>
                    </UPopover>
                  </template>
                </UInputDate>
              </UFormField>

              <div class="grid grid-cols-2 gap-4">
                <UFormField :label="t('pages.addProduct.inventory.form.price')" name="price">
                  <UInput v-model.number="inventoryFormData.price" type="number" step="0.01" min="0" :placeholder="t('pages.addProduct.inventory.form.pricePlaceholder')" class="w-full" />
                </UFormField>

                <UFormField v-if="inventoryFormData.price !== undefined && inventoryFormData.price > 0" :label="t('pages.addProduct.inventory.form.currency')" name="currency">
                  <USelect v-model="inventoryFormData.currency" :items="currencyOptions" :placeholder="t('pages.addProduct.inventory.form.currencyPlaceholder')" class="w-full" />
                </UFormField>
              </div>

              <UFormField :label="t('pages.addProduct.inventory.form.receiptNumber')" name="receiptNumber">
                <UInput v-model="inventoryFormData.receiptNumber" :placeholder="t('pages.addProduct.inventory.form.receiptNumberPlaceholder')" class="w-full" />
              </UFormField>

              <UButton type="submit" color="primary" block icon="i-lucide-plus">
                {{ t('pages.addProduct.inventory.addItemForm.addButton') }}
              </UButton>
            </UForm>
          </div>

          <!-- Items list -->
          <div v-if="individualItems.length > 0" class="space-y-4">
            <h4 class="text-md font-semibold">{{ t('pages.addProduct.inventory.itemsList.title') }} ({{ individualItems.length }})</h4>

            <div class="grid grid-cols-1 gap-3">
              <div v-for="item in individualItems" :key="item.id" class="relative p-4 border border-gray-300 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-800">
                <button v-if="individualItems.length > 1" class="absolute top-2 right-2 text-gray-400 hover:text-red-500 dark:hover:text-red-400 transition-colors" @click="removeIndividualItem(item.id)">
                  <UIcon name="i-lucide-x" class="h-5 w-5" />
                </button>

                <div class="space-y-2 text-sm">
                  <div class="flex items-center gap-2">
                    <UIcon name="i-lucide-package-2" class="h-4 w-4 text-primary-500" />
                    <span class="font-semibold">{{ item.quantity }} {{ t(`enums.unit.${item.unit}`) }}</span>
                  </div>
                  <div v-if="item.expirationAt" class="text-gray-600 dark:text-gray-400">
                    <span class="font-medium">{{ t('common.expirationDate') }}:</span>
                    {{ formatCalendarDate(item.expirationAt) }}
                  </div>
                  <div v-if="item.price && item.price > 0" class="text-gray-600 dark:text-gray-400">
                    <span class="font-medium">{{ t('common.price') }}:</span>
                    {{ item.price }} {{ item.currency ? t(`enums.currency.${item.currency}`) : '' }}
                  </div>
                  <div v-if="item.receiptNumber" class="text-gray-600 dark:text-gray-400 truncate">
                    <span class="font-medium">{{ t('pages.addProduct.inventory.form.receiptNumber') }}:</span>
                    {{ item.receiptNumber }}
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty state -->
          <div v-else class="text-center py-12 text-gray-500">
            <UIcon name="i-lucide-package-x" class="h-12 w-12 mx-auto mb-3 text-gray-400" />
            <p>{{ t('pages.addProduct.inventory.itemsList.empty') }}</p>
          </div>
        </div>
      </div>

      <!-- OpenFoodFacts modal -->
      <AppDrawer :open="isOpenFoodFactsModalOpen" :title="t('pages.addProduct.openFoodFacts.modalTitle')" icon="i-lucide-download" fit="content" @update:open="(val) => { if (!val) handleCancelImport() }">
        <p class="text-sm text-muted">{{ t('pages.addProduct.openFoodFacts.modalDescription') }}</p>
        <div class="space-y-4">
          <div class="flex justify-center">
            <div class="relative w-40 h-40">
              <USkeleton v-if="isImageLoading && openFoodFactsProduct?.image_base64" class="w-full h-full rounded-lg" />
              <img v-if="openFoodFactsProduct?.image_base64" :src="openFoodFactsProduct.image_base64" alt="Product image" class="w-full h-full object-contain rounded-lg border border-gray-200 dark:border-gray-700" :class="{ 'opacity-0': isImageLoading }" @load="isImageLoading = false">
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

      <!-- Barcode scanner modal -->
      <BarcodeScannerModal :on-barcode-detected="activeBarcodeHandler" />

      <!-- Preview modal (individual mode) -->
      <InventoryItemsPreviewModal
        v-if="isPreviewModalOpen"
        :is-open="isPreviewModalOpen"
        :items="previewItems"
        :storage-location-name="getStorageLocationName()"
        :shopping-location-name="selectedShoppingLocationId ? getShoppingLocationName() : undefined"
        :is-shared-with-family="inventoryFormData.isSharedWithFamily ?? true"
        @update:is-open="isPreviewModalOpen = $event"
        @confirm="handlePreviewConfirm"
      />

      <!-- Progress modal -->
      <InventoryCreationProgressModal
        :is-open="isProgressModalOpen"
        :items="progressItems"
        :is-processing="isCreatingMultiple"
        @update:is-open="isProgressModalOpen = $event"
        @close="handleProgressModalClose"
      />
    </template>
  </WizardDrawer>
</template>

<script setup lang="ts">
import { z } from 'zod'
import { watchDebounced } from '@vueuse/core'
import { computed, nextTick, ref, watch, onMounted, onUnmounted } from 'vue'
import type { ProductInfo, CreateProductRequest, CreateInventoryItemRequest } from '~/types/product'
import type { StorageLocationInfo, StorageLocationRequest, ShoppingLocationInfo, ShoppingLocationRequest } from '~/types/location'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import type { SelectValue } from '~/types/selectValue'
import { Unit, Currency, ProductCategory, SelectValueType } from '~/types/enums'
import type { FormSubmitEvent } from '#ui/types'

// Props & Emits
const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  created: []
}>()

// Composables
const { t } = useI18n()
const { inputDateLocale } = useInputDateLocale()
const toast = useToast()
const { getProducts, createProduct, createInventoryItem } = useProductsApi()
const { getStorageLocations, createStorageLocation, getShoppingLocations, createShoppingLocation } = useLocationsApi()
const { getProductByBarcode } = useOpenFoodFactsApi()
const { getSelectValues } = useSelectValueApi()
const { showCameraButton } = useCameraAvailability()

// Local type for form data with CalendarDate
interface InventoryFormData {
  quantity: number
  unit?: Unit
  expirationAt?: any | null
  price?: number
  currency?: Currency
  receiptNumber?: string
  isSharedWithFamily?: boolean
}

interface IndividualInventoryItem {
  id: string
  quantity: number
  unit: Unit
  expirationAt: any | null
  price: number | undefined
  currency: Currency | undefined
  receiptNumber: string | undefined
}

interface ProgressItem {
  id: string
  displayText: string
  status: 'pending' | 'in-progress' | 'success' | 'error'
  errorMessage?: string
}

// =========================
// Stepper state
// =========================
const currentStep = ref(0)
const stepperItems = computed(() => [
  { label: t('pages.addProduct.stepLabels.step1') },
  { label: t('pages.addProduct.stepLabels.step2') },
  { label: t('pages.addProduct.stepLabels.step3') },
  { label: t('pages.addProduct.stepLabels.step4') }
])

// =========================
// Step 0: Product selection
// =========================
const showCreateProduct = ref(false)
const createProductFormRef = ref()
const createStorageLocationFormRef = ref()
const createShoppingLocationFormRef = ref()
const searchQuery = ref('')
const searchResults = ref<ProductInfo[]>([])
const isSearching = ref(false)
const selectedProductId = ref<string | null>(null)
// Unit of the selected/created product — inventory inherits it.
const selectedProductUnit = ref<Unit | undefined>(undefined)

const productFavoriteFilter = ref(false)
const productEatableFilter = ref(false)
const productFilterCount = computed(() =>
  (productFavoriteFilter.value ? 1 : 0) + (productEatableFilter.value ? 1 : 0)
)
const filteredProductResults = computed(() =>
  searchResults.value.filter(p =>
    (!productFavoriteFilter.value || p.isFavorite)
    && (!productEatableFilter.value || p.isEatable)
  )
)

const isCreating = ref(false)
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
const categoryOptionsRaw = ref<SelectValue[]>([])
const categoryOptions = computed(() =>
  categoryOptionsRaw.value.map(cat => ({
    label: t(`enums.productCategory.${cat.text}`),
    value: parseInt(cat.text)
  }))
)

// OpenFoodFacts / barcode
const isQueryingBarcode = ref(false)
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)
const isOpenFoodFactsModalOpen = ref(false)
const isImageLoading = ref(true)
const activeBarcodeHandler = computed(() =>
  showCreateProduct.value ? handleBarcodeScanned : handleSearchBarcodeScanned
)

// =========================
// Step 1: Storage location
// =========================
const allStorageLocations = ref<StorageLocationInfo[]>([])
const storageLocationSearchQuery = ref('')
const isLoadingStorageLocations = ref(false)
const selectedStorageLocationId = ref<string | null>(null)
const showCreateStorageLocation = ref(false)
const storageSharedFilter = ref(false)
const storageFreezerFilter = ref(false)
const currentStoragePage = ref(1)
const storagePageSize = 20
const storageSentinelRef = ref<HTMLElement | null>(null)
const isCreatingLocation = ref(false)
const locationFormData = ref<StorageLocationRequest>({
  name: '',
  description: '',
  color: '',
  isFreezer: false,
  isSharedWithFamily: true
})

const storageFilterCount = computed(() =>
  (storageSharedFilter.value ? 1 : 0) + (storageFreezerFilter.value ? 1 : 0)
)
const filteredStorageLocations = computed(() => {
  const query = storageLocationSearchQuery.value.toLowerCase().trim()
  let result = allStorageLocations.value
  if (query) {
    result = result.filter(l =>
      l.name.toLowerCase().includes(query)
      || (l.description && l.description.toLowerCase().includes(query))
    )
  }
  if (storageFreezerFilter.value) result = result.filter(l => l.isFreezer)
  if (storageSharedFilter.value) result = result.filter(l => l.isSharedWithFamily)
  return [...result].sort((a, b) => a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu'))
})
const paginatedStorageLocations = computed(() =>
  filteredStorageLocations.value.slice(0, currentStoragePage.value * storagePageSize)
)

// =========================
// Step 2: Shopping location
// =========================
const allShoppingLocations = ref<ShoppingLocationInfo[]>([])
const shoppingLocationSearchQuery = ref('')
const isLoadingShoppingLocations = ref(false)
const selectedShoppingLocationId = ref<string | null>(null)
const showCreateShoppingLocation = ref(false)
const shoppingCityFilter = ref('all')
const shoppingSharedFilter = ref(false)
const currentShoppingPage = ref(1)
const shoppingPageSize = 20
const shoppingSentinelRef = ref<HTMLElement | null>(null)
const isCreatingShoppingLocation = ref(false)
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
  isSharedWithFamily: true
})

const shoppingFilterCount = computed(() =>
  (shoppingCityFilter.value !== 'all' ? 1 : 0) + (shoppingSharedFilter.value ? 1 : 0)
)
const shoppingCityOptions = computed(() => {
  const cities = Array.from(
    new Set(allShoppingLocations.value.map(l => l.city).filter((c): c is string => !!c))
  ).sort((a, b) => a.localeCompare(b, 'hu'))
  return [
    { label: t('pages.addProduct.pick.allCities'), value: 'all' },
    ...cities.map(c => ({ label: c, value: c }))
  ]
})
const filteredShoppingLocations = computed(() => {
  const query = shoppingLocationSearchQuery.value.toLowerCase().trim()
  let result = allShoppingLocations.value
  if (query) {
    result = result.filter(l =>
      l.name.toLowerCase().includes(query)
      || (l.description && l.description.toLowerCase().includes(query))
      || (l.address && l.address.toLowerCase().includes(query))
      || (l.city && l.city.toLowerCase().includes(query))
    )
  }
  if (shoppingCityFilter.value !== 'all') result = result.filter(l => l.city === shoppingCityFilter.value)
  if (shoppingSharedFilter.value) result = result.filter(l => l.isSharedWithFamily)
  return [...result].sort((a, b) => a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu'))
})
const paginatedShoppingLocations = computed(() =>
  filteredShoppingLocations.value.slice(0, currentShoppingPage.value * shoppingPageSize)
)

// =========================
// Step 3: Inventory
// =========================
const isCreatingInventory = ref(false)
const expirationDateInput = ref()
const inventoryFormData = ref<InventoryFormData>({
  quantity: 1,
  unit: undefined,
  expirationAt: null,
  price: undefined,
  currency: undefined,
  receiptNumber: undefined,
  isSharedWithFamily: true
})
const inventoryHandlingMode = ref<'bulk' | 'individual' | null>(null)
const individualItems = ref<IndividualInventoryItem[]>([])
const isPreviewModalOpen = ref(false)
const isProgressModalOpen = ref(false)
const progressItems = ref<ProgressItem[]>([])
const isCreatingMultiple = ref(false)

// The inventory always inherits the selected product's unit.
const productUnitLabel = computed(() =>
  selectedProductUnit.value !== undefined ? t(`enums.unit.${selectedProductUnit.value}`) : ''
)
watch(selectedProductUnit, (unit) => {
  inventoryFormData.value.unit = unit
}, { immediate: true })

const isBulkFormValid = computed(() =>
  inventoryFormData.value.quantity > 0 && inventoryFormData.value.unit !== undefined
)

const previewItems = computed(() =>
  inventoryHandlingMode.value === 'bulk' && inventoryFormData.value.unit
    ? [{
        id: '1',
        quantity: inventoryFormData.value.quantity,
        unit: inventoryFormData.value.unit,
        expirationAt: inventoryFormData.value.expirationAt || null,
        price: inventoryFormData.value.price,
        currency: inventoryFormData.value.currency,
        receiptNumber: inventoryFormData.value.receiptNumber
      }]
    : individualItems.value
)

// =========================
// Options / schemas
// =========================
const unitOptions = computed(() =>
  Object.entries(Unit)
    .filter(([key]) => isNaN(Number(key)))
    .map(([_key, value]) => ({ label: t(`enums.unit.${value}`), value: value as Unit }))
)
const currencyOptions = computed(() => [
  { label: t('enums.currency.135'), value: Currency.Huf },
  { label: t('enums.currency.105'), value: Currency.Eur },
  { label: t('enums.currency.279'), value: Currency.Usd }
])

const createProductSchema = z.object({
  name: z.string({ required_error: 'A termék neve kötelező' }).min(1, 'A termék neve kötelező'),
  brand: z.string({ required_error: 'A márka kötelező' }).min(1, 'A márka kötelező'),
  category: z.nativeEnum(ProductCategory).optional(),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
  barcode: z.string().optional(),
  isEatable: z.boolean().optional().default(false),
  notes: z.string().optional(),
  isFavorite: z.boolean().optional().default(false)
})
type CreateProductSchema = z.output<typeof createProductSchema>

const createLocationSchema = z.object({
  name: z.string({ required_error: 'Storage location name is required' }).min(2, 'Name must be at least 2 characters').max(128, 'Name must not exceed 128 characters'),
  description: z.string().max(500, 'Description must not exceed 500 characters').optional(),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Color must be a valid hex color (e.g., #FF5733)').optional().or(z.literal('')),
  isFreezer: z.boolean().optional().default(false),
  isSharedWithFamily: z.boolean().optional().default(true)
})
type CreateLocationSchema = z.output<typeof createLocationSchema>

const createShoppingLocationSchema = z.object({
  name: z.string({ required_error: 'Shopping location name is required' }).min(2, 'Name must be at least 2 characters').max(128, 'Name must not exceed 128 characters'),
  description: z.string().max(500, 'Description must not exceed 500 characters').optional(),
  address: z.string().max(128, 'Address must not exceed 128 characters').optional(),
  city: z.string().max(64, 'City must not exceed 64 characters').optional(),
  postalCode: z.string().max(20, 'Postal code must not exceed 20 characters').optional(),
  country: z.string().max(64, 'Country must not exceed 64 characters').optional(),
  website: z.string().url('Must be a valid URL').max(255, 'URL must not exceed 255 characters').optional().or(z.literal('')),
  googleMaps: z.string().url('Must be a valid URL').max(255, 'URL must not exceed 255 characters').optional().or(z.literal('')),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Color must be a valid hex color').optional().or(z.literal('')),
  isSharedWithFamily: z.boolean().optional().default(true)
})
type CreateShoppingLocationSchema = z.output<typeof createShoppingLocationSchema>

const createInventorySchema = z.object({
  quantity: z.number({ required_error: 'Quantity is required' }).min(0.001, 'Quantity must be greater than 0'),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
  expirationAt: z.any().nullable().optional(),
  price: z.number().min(0, 'Price must be positive').optional(),
  currency: z.nativeEnum(Currency).optional(),
  receiptNumber: z.string().max(50, 'Receipt number must not exceed 50 characters').optional(),
  isSharedWithFamily: z.boolean().optional().default(true)
})

const individualItemSchema = z.object({
  quantity: z.number({ required_error: 'Quantity is required' }).min(0.001, 'Quantity must be greater than 0'),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
  expirationAt: z.any().nullable().optional(),
  price: z.number().min(0).optional(),
  currency: z.nativeEnum(Currency).optional(),
  receiptNumber: z.string().max(50).optional()
})

// =========================
// Footer navigation
// =========================
const canGoBack = computed(() =>
  showCreateProduct.value
  || showCreateStorageLocation.value
  || showCreateShoppingLocation.value
  || (currentStep.value === 3 && inventoryHandlingMode.value !== null)
  || currentStep.value > 0
)

const canProceed = computed(() => {
  if (currentStep.value === 0) return !showCreateProduct.value && !!selectedProductId.value
  if (currentStep.value === 1) return !showCreateStorageLocation.value && !!selectedStorageLocationId.value
  if (currentStep.value === 2) return !showCreateShoppingLocation.value
  return true
})

// The Finish button (last step) is NOT gated by canProceed — gate it here.
const finishDisabled = computed(() => {
  if (inventoryHandlingMode.value === null) return true
  if (inventoryHandlingMode.value === 'bulk') return !isBulkFormValid.value
  return individualItems.value.length === 0
})

const finishLabel = computed(() =>
  inventoryHandlingMode.value === 'individual'
    ? t('pages.addProduct.inventory.preview.viewAllButton', { count: individualItems.value.length })
    : t('pages.addProduct.inventory.form.createButton')
)

const goPrevious = () => {
  if (showCreateProduct.value) { showCreateProduct.value = false; return }
  if (showCreateStorageLocation.value) { showCreateStorageLocation.value = false; return }
  if (showCreateShoppingLocation.value) { showCreateShoppingLocation.value = false; return }
  if (currentStep.value === 3 && inventoryHandlingMode.value !== null) { handleBackToModeSelection(); return }
  if (currentStep.value > 0) currentStep.value -= 1
}

const goNext = () => {
  if (!canProceed.value) return
  if (currentStep.value < 3) currentStep.value += 1
}

const finish = () => {
  if (inventoryHandlingMode.value === 'bulk') {
    onCreateBulkInventory()
  } else if (inventoryHandlingMode.value === 'individual') {
    showIndividualItemsPreview()
  }
}

const onOpenChange = (val: boolean) => emit('update:open', val)

// =========================
// Open / reset lifecycle
// =========================
const resetState = () => {
  currentStep.value = 0

  showCreateProduct.value = false
  searchQuery.value = ''
  searchResults.value = []
  isSearching.value = false
  selectedProductId.value = null
  selectedProductUnit.value = undefined
  productFavoriteFilter.value = false
  productEatableFilter.value = false
  productFormData.value = { name: '', brand: '', category: undefined, unit: Unit.Piece, barcode: '', isEatable: false, notes: '', isFavorite: false }
  isQueryingBarcode.value = false
  openFoodFactsProduct.value = null
  isOpenFoodFactsModalOpen.value = false

  showCreateStorageLocation.value = false
  storageLocationSearchQuery.value = ''
  selectedStorageLocationId.value = null
  storageSharedFilter.value = false
  storageFreezerFilter.value = false
  currentStoragePage.value = 1
  locationFormData.value = { name: '', description: '', color: '', isFreezer: false, isSharedWithFamily: true }

  showCreateShoppingLocation.value = false
  shoppingLocationSearchQuery.value = ''
  selectedShoppingLocationId.value = null
  shoppingCityFilter.value = 'all'
  shoppingSharedFilter.value = false
  currentShoppingPage.value = 1
  shoppingLocationFormData.value = { name: '', description: '', color: '', address: '', city: '', postalCode: '', country: '', website: '', googleMaps: '', isSharedWithFamily: true }

  inventoryHandlingMode.value = null
  individualItems.value = []
  isPreviewModalOpen.value = false
  isProgressModalOpen.value = false
  progressItems.value = []
  isCreatingMultiple.value = false
  isCreatingInventory.value = false
  inventoryFormData.value = { quantity: 1, unit: undefined, expirationAt: null, price: undefined, currency: undefined, receiptNumber: undefined, isSharedWithFamily: true }
}

watch(() => props.open, (isOpen) => {
  if (isOpen) resetState()
})

onMounted(async () => {
  try {
    const response = await getSelectValues(SelectValueType.ProductCategory)
    if (response.success && response.data) categoryOptionsRaw.value = response.data
  } catch (error) {
    console.error('Failed to load categories:', error)
  }
})

// =========================
// Watchers
// =========================
watch(() => productFormData.value.barcode, (newValue) => {
  if (newValue) productFormData.value.barcode = newValue.replace(/\D/g, '')
})

watchDebounced(searchQuery, async (newQuery) => {
  if (newQuery.trim() === '') { searchResults.value = []; return }
  isSearching.value = true
  try {
    const response = await getProducts({ searchText: newQuery, pageNumber: 1, pageSize: 20 })
    if (response.success && response.data) {
      searchResults.value = [...response.data.items].sort((a, b) => a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu'))
    }
  } catch (error) {
    console.error('Product search failed:', error)
  } finally {
    isSearching.value = false
  }
}, { debounce: 300 })

// Lazy-load location lists when their step is entered.
watch(currentStep, async (newStep) => {
  if (newStep === 1 && allStorageLocations.value.length === 0) {
    isLoadingStorageLocations.value = true
    try {
      const response = await getStorageLocations({ returnAll: true })
      if (response.success && response.data) allStorageLocations.value = response.data.items
    } catch (error) {
      console.error('Failed to load storage locations:', error)
    } finally {
      isLoadingStorageLocations.value = false
    }
  }
  if (newStep === 2 && allShoppingLocations.value.length === 0) {
    isLoadingShoppingLocations.value = true
    try {
      const response = await getShoppingLocations({ returnAll: true })
      if (response.success && response.data) allShoppingLocations.value = response.data.items
    } catch (error) {
      console.error('Failed to load shopping locations:', error)
    } finally {
      isLoadingShoppingLocations.value = false
    }
  }
})

watch(storageLocationSearchQuery, () => { currentStoragePage.value = 1 })
watch(shoppingLocationSearchQuery, () => { currentShoppingPage.value = 1 })

// Intersection observers for lazy pagination (sentinels mount/unmount with the step).
let storageObserver: IntersectionObserver | null = null
watch(storageSentinelRef, (sentinel) => {
  storageObserver?.disconnect()
  storageObserver = null
  if (sentinel && typeof IntersectionObserver !== 'undefined') {
    storageObserver = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting && paginatedStorageLocations.value.length < filteredStorageLocations.value.length) {
        currentStoragePage.value += 1
      }
    }, { threshold: 0.1 })
    storageObserver.observe(sentinel)
  }
})

let shoppingObserver: IntersectionObserver | null = null
watch(shoppingSentinelRef, (sentinel) => {
  shoppingObserver?.disconnect()
  shoppingObserver = null
  if (sentinel && typeof IntersectionObserver !== 'undefined') {
    shoppingObserver = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting && paginatedShoppingLocations.value.length < filteredShoppingLocations.value.length) {
        currentShoppingPage.value += 1
      }
    }, { threshold: 0.1 })
    shoppingObserver.observe(sentinel)
  }
})

onUnmounted(() => {
  storageObserver?.disconnect()
  shoppingObserver?.disconnect()
})

// =========================
// Step 0 handlers
// =========================
const onStartCreateProduct = () => {
  productFormData.value.name = searchQuery.value.trim()
}

const onProductCardClick = (product: ProductInfo) => {
  const alreadySelected = selectedProductId.value === product.publicId
  selectedProductId.value = alreadySelected ? null : product.publicId
  selectedProductUnit.value = alreadySelected ? undefined : product.unit
}

const onCreateProduct = async (event: FormSubmitEvent<CreateProductSchema>) => {
  isCreating.value = true
  try {
    const productData: CreateProductRequest = {
      name: event.data.name,
      brand: event.data.brand,
      category: event.data.category || null,
      unit: event.data.unit,
      barcode: event.data.barcode?.trim() || null,
      notes: event.data.notes?.trim() || null,
      isEatable: event.data.isEatable,
      isFavorite: event.data.isFavorite
    }
    const response = await createProduct(productData)
    if (response.success && response.data) {
      selectedProductId.value = response.data.publicId
      selectedProductUnit.value = response.data.unit
      showCreateProduct.value = false
      currentStep.value = 1
    }
  } catch (error) {
    console.error('Product creation failed:', error)
  } finally {
    isCreating.value = false
  }
}

const handleBarcodeQuery = async () => {
  if (!productFormData.value.barcode || productFormData.value.barcode.trim() === '') {
    toast.add({ title: t('toast.error'), description: t('pages.addProduct.openFoodFacts.noBarcodeError'), color: 'error' })
    return
  }
  isQueryingBarcode.value = true
  try {
    const response = await getProductByBarcode(productFormData.value.barcode.trim())
    if (response.success && response.data) {
      openFoodFactsProduct.value = response.data
      isImageLoading.value = true
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

const handleImportProduct = () => {
  if (openFoodFactsProduct.value) {
    if (openFoodFactsProduct.value.product_name) productFormData.value.name = openFoodFactsProduct.value.product_name
    if (openFoodFactsProduct.value.brands) productFormData.value.brand = openFoodFactsProduct.value.brands
    productFormData.value.isEatable = true
  }
  handleCancelImport()
}

const handleCancelImport = () => {
  isOpenFoodFactsModalOpen.value = false
  openFoodFactsProduct.value = null
  isImageLoading.value = true
}

const handleBarcodeScanned = (barcode: string) => {
  productFormData.value.barcode = barcode
  nextTick(() => handleBarcodeQuery())
}

const handleSearchBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// =========================
// Step 1 handlers (storage location)
// =========================
const onStartCreateStorageLocation = () => {
  locationFormData.value.name = storageLocationSearchQuery.value.trim()
}

const onStorageLocationCardClick = (location: StorageLocationInfo) => {
  const alreadySelected = selectedStorageLocationId.value === location.publicId
  selectedStorageLocationId.value = alreadySelected ? null : location.publicId
}

const onCreateLocation = async (event: FormSubmitEvent<CreateLocationSchema>) => {
  isCreatingLocation.value = true
  try {
    const locationData: StorageLocationRequest = {
      name: event.data.name,
      description: event.data.description?.trim() || undefined,
      color: event.data.color?.trim() || undefined,
      isFreezer: event.data.isFreezer,
      isSharedWithFamily: event.data.isSharedWithFamily
    }
    const response = await createStorageLocation(locationData)
    if (response.success && response.data) {
      allStorageLocations.value.push(response.data)
      selectedStorageLocationId.value = response.data.publicId
      showCreateStorageLocation.value = false
      currentStep.value = 2
    }
  } catch (error) {
    console.error('Storage location creation failed:', error)
  } finally {
    isCreatingLocation.value = false
  }
}

// =========================
// Step 2 handlers (shopping location)
// =========================
const onStartCreateShoppingLocation = () => {
  shoppingLocationFormData.value.name = shoppingLocationSearchQuery.value.trim()
}

const onShoppingLocationCardClick = (location: ShoppingLocationInfo) => {
  const alreadySelected = selectedShoppingLocationId.value === location.publicId
  selectedShoppingLocationId.value = alreadySelected ? null : location.publicId
}

const onCreateShoppingLocation = async (event: FormSubmitEvent<CreateShoppingLocationSchema>) => {
  isCreatingShoppingLocation.value = true
  try {
    const locationData: ShoppingLocationRequest = {
      name: event.data.name,
      description: event.data.description?.trim() || undefined,
      address: event.data.address?.trim() || undefined,
      city: event.data.city?.trim() || undefined,
      postalCode: event.data.postalCode?.trim() || undefined,
      country: event.data.country?.trim() || undefined,
      website: event.data.website?.trim() || undefined,
      googleMaps: event.data.googleMaps?.trim() || undefined,
      color: event.data.color?.trim() || undefined,
      isSharedWithFamily: event.data.isSharedWithFamily
    }
    const response = await createShoppingLocation(locationData)
    if (response.success && response.data) {
      allShoppingLocations.value.push(response.data)
      selectedShoppingLocationId.value = response.data.publicId
      showCreateShoppingLocation.value = false
      currentStep.value = 3
    }
  } catch (error) {
    console.error('Shopping location creation failed:', error)
  } finally {
    isCreatingShoppingLocation.value = false
  }
}

// =========================
// Step 3 helpers / handlers (inventory)
// =========================
const getStorageLocationName = (): string => {
  if (!selectedStorageLocationId.value) return t('pages.addProduct.inventory.sharedContext.none')
  return allStorageLocations.value.find(l => l.publicId === selectedStorageLocationId.value)?.name
    || t('pages.addProduct.inventory.sharedContext.none')
}

const getShoppingLocationName = (): string => {
  if (!selectedShoppingLocationId.value) return t('pages.addProduct.inventory.sharedContext.none')
  return allShoppingLocations.value.find(l => l.publicId === selectedShoppingLocationId.value)?.name
    || t('pages.addProduct.inventory.sharedContext.none')
}

const formatCalendarDate = (date: any): string =>
  `${date.year}-${String(date.month).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`

const resetInventoryForm = () => {
  inventoryFormData.value = {
    quantity: 1,
    unit: selectedProductUnit.value,
    expirationAt: null,
    price: undefined,
    currency: undefined,
    receiptNumber: undefined,
    isSharedWithFamily: inventoryFormData.value.isSharedWithFamily
  }
}

const handleBackToModeSelection = () => {
  inventoryHandlingMode.value = null
  individualItems.value = []
  resetInventoryForm()
}

const onAddIndividualItem = (_event: FormSubmitEvent<z.output<typeof individualItemSchema>>) => {
  individualItems.value.push({
    id: crypto.randomUUID(),
    quantity: inventoryFormData.value.quantity,
    unit: inventoryFormData.value.unit!,
    expirationAt: inventoryFormData.value.expirationAt ?? null,
    price: inventoryFormData.value.price,
    currency: inventoryFormData.value.currency,
    receiptNumber: inventoryFormData.value.receiptNumber
  })
  const previousUnit = inventoryFormData.value.unit
  resetInventoryForm()
  inventoryFormData.value.unit = previousUnit
}

const removeIndividualItem = (itemId: string) => {
  if (individualItems.value.length <= 1) {
    toast.add({ title: t('toast.error'), description: t('pages.addProduct.inventory.minimumOneItem'), color: 'error' })
    return
  }
  individualItems.value = individualItems.value.filter(item => item.id !== itemId)
}

const convertCalendarDateToISO = (date: any): string | undefined => {
  if (!date) return undefined
  return new Date(date.year, date.month - 1, date.day, 12, 0, 0).toISOString()
}

const buildInventoryRequest = (itemData: IndividualInventoryItem | InventoryFormData): CreateInventoryItemRequest => ({
  productPublicId: selectedProductId.value!,
  storageLocationPublicId: selectedStorageLocationId.value || undefined,
  shoppingLocationPublicId: selectedShoppingLocationId.value || undefined,
  quantity: itemData.quantity,
  expirationAt: convertCalendarDateToISO(itemData.expirationAt || null),
  price: itemData.price && itemData.price > 0 ? itemData.price : undefined,
  currency: itemData.price && itemData.price > 0 ? itemData.currency : undefined,
  receiptNumber: itemData.receiptNumber?.trim() || undefined,
  isSharedWithFamily: inventoryFormData.value.isSharedWithFamily
})

const formatItemDisplay = (item: IndividualInventoryItem | InventoryFormData): string => {
  const unitLabel = t(`enums.unit.${item.unit}`)
  const expiration = item.expirationAt ? formatCalendarDate(item.expirationAt) : t('pages.addProduct.progressModal.noExpiration')
  return `${item.quantity} ${unitLabel} - ${t('common.expirationDate')}: ${expiration}`
}

const showIndividualItemsPreview = () => {
  if (individualItems.value.length === 0) {
    toast.add({ title: t('toast.error'), description: t('pages.addProduct.progressModal.noItemsError'), color: 'error' })
    return
  }
  isPreviewModalOpen.value = true
}

const finishSuccessfully = () => {
  emit('created')
  emit('update:open', false)
}

const onCreateBulkInventory = () => {
  try {
    createInventorySchema.parse(inventoryFormData.value)
  } catch (error) {
    if (error instanceof z.ZodError) {
      toast.add({ title: t('toast.error'), description: error.errors[0]?.message ?? t('toast.error'), color: 'error' })
    }
    return
  }
  onConfirmAndCreateBulkInventory()
}

const handlePreviewConfirm = () => {
  if (inventoryHandlingMode.value === 'bulk') {
    onConfirmAndCreateBulkInventory()
  } else {
    onConfirmAndCreateMultipleInventoryItems()
  }
}

const onConfirmAndCreateBulkInventory = async () => {
  isPreviewModalOpen.value = false
  progressItems.value = [{ id: '1', displayText: formatItemDisplay(inventoryFormData.value), status: 'pending' }]
  isProgressModalOpen.value = true
  isCreatingInventory.value = true
  isCreatingMultiple.value = true

  const current = progressItems.value[0]!
  current.status = 'in-progress'

  try {
    const response = await createInventoryItem(buildInventoryRequest(inventoryFormData.value))
    if (response.success) {
      current.status = 'success'
      setTimeout(finishSuccessfully, 1000)
    } else {
      current.status = 'error'
      current.errorMessage = t('toast.error')
      isCreatingInventory.value = false
      isCreatingMultiple.value = false
    }
  } catch (error) {
    current.status = 'error'
    current.errorMessage = error instanceof Error ? error.message : t('toast.error')
    console.error('Inventory item creation failed:', error)
    isCreatingInventory.value = false
    isCreatingMultiple.value = false
  }
}

const onConfirmAndCreateMultipleInventoryItems = async () => {
  isPreviewModalOpen.value = false
  progressItems.value = individualItems.value.map(item => ({ id: item.id, displayText: formatItemDisplay(item), status: 'pending' as const }))
  isProgressModalOpen.value = true
  isCreatingInventory.value = true
  isCreatingMultiple.value = true

  let failureCount = 0
  for (let i = 0; i < progressItems.value.length; i++) {
    const current = progressItems.value[i]
    const source = individualItems.value[i]
    if (!current || !source) continue
    current.status = 'in-progress'
    try {
      const response = await createInventoryItem(buildInventoryRequest(source))
      if (response.success) {
        current.status = 'success'
      } else {
        current.status = 'error'
        current.errorMessage = t('toast.error')
        failureCount++
      }
    } catch (error) {
      current.status = 'error'
      current.errorMessage = error instanceof Error ? error.message : t('toast.error')
      failureCount++
      console.error('Failed to create inventory item:', error)
    }
    await new Promise(resolve => setTimeout(resolve, 300))
  }

  isCreatingMultiple.value = false
  isCreatingInventory.value = false
  if (failureCount === 0) setTimeout(finishSuccessfully, 1000)
}

const handleProgressModalClose = () => {
  if (!isCreatingMultiple.value && progressItems.value.every(item => item.status !== 'pending' && item.status !== 'in-progress')) {
    isProgressModalOpen.value = false
    finishSuccessfully()
  }
}
</script>
