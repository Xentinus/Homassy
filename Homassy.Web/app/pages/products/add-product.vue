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
        :model-value="currentStep"
        :items="stepperItems"
        orientation="horizontal"
        @update:model-value="handleStepChange"
      />
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-48 px-4 sm:px-8 lg:px-14 pb-6">
      <!-- Step 1: Product Selection -->
      <div v-if="currentStep === 0" class="space-y-6">
        <UTabs v-model="activeTab" :items="tabItems">
          <template #search>
            <!-- Tab 1: Search -->
            <div class="space-y-4 py-4">
              <UFieldGroup size="lg" orientation="horizontal" class="w-full">
                <UInput
                  v-model="searchQuery"
                  :placeholder="t('pages.addProduct.search.placeholder')"
                  icon="i-lucide-search"
                  size="lg"
                  class="flex-1"
                />
                <BarcodeScannerButton v-if="showCameraButton" @scanned="handleSearchBarcodeScanned" />
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
                  :search-query="searchQuery"
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
                  <UFieldGroup size="md" orientation="horizontal" class="w-full">
                    <UInput
                      v-model="formData.barcode"
                      :placeholder="t('pages.addProduct.form.barcodePlaceholder')"
                      :disabled="isCreating"
                      inputmode="numeric"
                      pattern="[0-9]*"
                      class="flex-1"
                    />
                    <BarcodeScannerButton
                      v-if="showCameraButton"
                      :disabled="isCreating"
                      @scanned="handleBarcodeScanned"
                    />
                    <UButton
                      :label="t('pages.addProduct.form.barcodeQuery')"
                      icon="i-lucide-barcode"
                      color="primary"
                      size="sm"
                      :loading="isQueryingBarcode"
                      :disabled="isCreating"
                      @click="handleBarcodeQuery"
                    />
                  </UFieldGroup>
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

        <!-- OpenFoodFacts Modal -->
        <UModal
          :open="isOpenFoodFactsModalOpen"
          @update:open="(val) => { if (!val) handleCancelImport() }"
          :dismissible="false"
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
                @click="handleCancelImport"
              />
              <UButton
                :label="t('pages.addProduct.openFoodFacts.import')"
                color="primary"
                @click="handleImportProduct"
              />
            </div>
          </template>
        </UModal>

        <!-- Barcode Scanner Modal -->
        <BarcodeScannerModal :on-barcode-detected="dynamicBarcodeHandler" />
      </div>

      <!-- Step 2: Storage Location Selection -->
      <div v-else-if="currentStep === 1" class="space-y-6">
        <UTabs v-model="activeTab" :items="locationTabItems">
          <template #search>
            <div class="space-y-4 py-4">
              <!-- Search Input -->
              <UInput
                v-model="locationSearchQuery"
                :placeholder="t('pages.addProduct.location.search.placeholder')"
                icon="i-lucide-search"
                size="lg"
                class="w-full"
              />

              <!-- Loading State -->
              <div v-if="isLoadingLocations" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
                <USkeleton class="h-32 w-full" />
              </div>

              <!-- No Results -->
              <div
                v-else-if="filteredLocations.length === 0 && !isLoadingLocations"
                class="text-center py-12 text-gray-500"
              >
                {{ t('pages.addProduct.location.search.noResults') }}
              </div>

              <!-- Results Grid -->
              <div v-else>
                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                  <StorageLocationCard
                    v-for="location in displayedLocations"
                    :key="location.publicId"
                    :location="location"
                    :is-active="selectedLocationCardId === location.publicId"
                    :search-query="locationSearchQuery"
                    @click="onLocationCardClick(location)"
                  />
                </div>

                <!-- Sentinel for intersection observer -->
                <div v-if="hasMoreLocations" ref="locationSentinelRef" class="w-full">
                  <!-- Loading skeletons while loading more -->
                  <div v-if="loadingMoreLocations" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mt-4">
                    <USkeleton class="h-32 w-full" />
                    <USkeleton class="h-32 w-full" />
                    <USkeleton class="h-32 w-full" />
                  </div>
                </div>
              </div>
            </div>
          </template>

          <template #create>
            <div class="py-4">
              <UForm
                :schema="createLocationSchema"
                :state="locationFormData"
                class="space-y-4"
                @submit="onCreateLocation"
              >
                <UFormField :label="t('pages.addProduct.location.form.name')" name="name" required>
                  <UInput
                    v-model="locationFormData.name"
                    :placeholder="t('pages.addProduct.location.form.namePlaceholder')"
                    :disabled="isCreatingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.location.form.description')" name="description">
                  <UTextarea
                    v-model="locationFormData.description"
                    :placeholder="t('pages.addProduct.location.form.descriptionPlaceholder')"
                    :disabled="isCreatingLocation"
                    class="w-full"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.location.form.color')" name="color">
                  <div class="flex items-center gap-3">
                    <UPopover>
                      <UButton
                        color="neutral"
                        variant="outline"
                        :disabled="isCreatingLocation"
                      >
                        <div class="flex items-center gap-2">
                          <div
                            v-if="locationFormData.color"
                            class="w-4 h-4 rounded"
                            :style="{ backgroundColor: locationFormData.color }"
                          />
                          <span>{{ locationFormData.color || t('pages.addProduct.location.form.chooseColor') }}</span>
                        </div>
                      </UButton>
                      <template #content>
                        <UColorPicker v-model="locationFormData.color" />
                      </template>
                    </UPopover>
                    <UButton
                      v-if="locationFormData.color"
                      icon="i-lucide-x"
                      color="neutral"
                      variant="ghost"
                      size="sm"
                      :disabled="isCreatingLocation"
                      @click="locationFormData.color = ''"
                    />
                  </div>
                </UFormField>

                <UFormField :label="t('pages.addProduct.location.form.isFreezer')" name="isFreezer">
                  <UCheckbox
                    v-model="locationFormData.isFreezer"
                    :label="t('pages.addProduct.location.form.isFreezerLabel')"
                    :disabled="isCreatingLocation"
                  />
                </UFormField>

                <UFormField :label="t('pages.addProduct.location.form.isSharedWithFamily')" name="isSharedWithFamily">
                  <UCheckbox
                    v-model="locationFormData.isSharedWithFamily"
                    :label="t('pages.addProduct.location.form.isSharedWithFamilyLabel')"
                    :disabled="isCreatingLocation"
                  />
                </UFormField>

                <UButton
                  type="submit"
                  color="primary"
                  block
                  :loading="isCreatingLocation"
                  icon="i-lucide-plus"
                >
                  {{ t('pages.addProduct.location.form.createButton') }}
                </UButton>
              </UForm>
            </div>
          </template>
        </UTabs>
      </div>

      <!-- Step 3: Shopping Location Selection -->
      <div v-else-if="currentStep === 2" class="space-y-6">
        <!-- Skip Button -->
        <div class="flex justify-end mb-4">
          <UButton
            :label="t('pages.addProduct.shoppingLocation.skipButton')"
            color="neutral"
            variant="outline"
            icon="i-lucide-skip-forward"
            @click="onSkipShoppingLocation"
          />
        </div>

        <UTabs v-model="activeTab" :items="shoppingLocationTabItems">
          <template #search>
            <div class="space-y-4 py-4">
              <!-- Search Input -->
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
                v-else-if="filteredShoppingLocations.length === 0 && !isLoadingShoppingLocations"
                class="text-center py-12 text-gray-500"
              >
                {{ t('pages.addProduct.shoppingLocation.search.noResults') }}
              </div>

              <!-- Results Grid -->
              <div v-else>
                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                  <ShoppingLocationCard
                    v-for="location in displayedShoppingLocations"
                    :key="location.publicId"
                    :location="location"
                    :is-active="selectedShoppingLocationCardId === location.publicId"
                    :search-query="shoppingLocationSearchQuery"
                    @click="onShoppingLocationCardClick(location)"
                  />
                </div>

                <!-- Sentinel for intersection observer -->
                <div v-if="hasMoreShoppingLocations" ref="shoppingLocationSentinelRef" class="w-full">
                  <!-- Loading skeletons while loading more -->
                  <div v-if="loadingMoreShoppingLocations" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mt-4">
                    <USkeleton class="h-32 w-full" />
                    <USkeleton class="h-32 w-full" />
                    <USkeleton class="h-32 w-full" />
                  </div>
                </div>
              </div>
            </div>
          </template>

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

                <UFormField :label="t('pages.addProduct.shoppingLocation.form.isSharedWithFamily')" name="isSharedWithFamily">
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
                  icon="i-lucide-plus"
                >
                  {{ t('pages.addProduct.shoppingLocation.form.createButton') }}
                </UButton>
              </UForm>
            </div>
          </template>
        </UTabs>
      </div>

      <!-- Step 4: Create Inventory Item -->
      <div v-else-if="currentStep === 3" class="space-y-6">
        <!-- Mode Selection (shown first when no mode selected) -->
        <div v-if="inventoryHandlingMode === null" class="space-y-4">
          <h3 class="text-lg font-semibold">
            {{ t('pages.addProduct.inventory.modeSelection.title') }}
          </h3>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            {{ t('pages.addProduct.inventory.modeSelection.description') }}
          </p>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-6">
            <!-- Bulk Mode Card -->
            <button
              @click="inventoryHandlingMode = 'bulk'"
              class="p-6 border-2 border-gray-300 dark:border-gray-700 rounded-lg hover:border-primary-500 dark:hover:border-primary-400 transition-colors text-left"
            >
              <div class="flex items-center gap-3 mb-2">
                <UIcon name="i-lucide-layers" class="h-6 w-6 text-primary-500" />
                <span class="text-lg font-semibold">{{ t('pages.addProduct.inventory.modeSelection.bulk') }}</span>
              </div>
              <p class="text-sm text-gray-600 dark:text-gray-400">
                {{ t('pages.addProduct.inventory.modeSelection.bulkDescription') }}
              </p>
            </button>

            <!-- Individual Mode Card -->
            <button
              @click="inventoryHandlingMode = 'individual'"
              class="p-6 border-2 border-gray-300 dark:border-gray-700 rounded-lg hover:border-primary-500 dark:hover:border-primary-400 transition-colors text-left"
            >
              <div class="flex items-center gap-3 mb-2">
                <UIcon name="i-lucide-grid-2x2" class="h-6 w-6 text-primary-500" />
                <span class="text-lg font-semibold">{{ t('pages.addProduct.inventory.modeSelection.individual') }}</span>
              </div>
              <p class="text-sm text-gray-600 dark:text-gray-400">
                {{ t('pages.addProduct.inventory.modeSelection.individualDescription') }}
              </p>
            </button>
          </div>
        </div>

        <!-- Bulk Mode UI -->
        <div v-else-if="inventoryHandlingMode === 'bulk'" class="space-y-6">
          <!-- Back to Mode Selection -->
          <div class="flex items-center justify-between">
            <h3 class="text-lg font-semibold">
              {{ t('pages.addProduct.inventory.bulk.title') }}
            </h3>
            <UButton
              :label="t('common.back')"
              icon="i-lucide-arrow-left"
              color="neutral"
              variant="ghost"
              size="sm"
              @click="handleBackToModeSelection"
            />
          </div>

          <!-- Inventory Form -->
          <UForm
            :schema="createInventorySchema"
            :state="inventoryFormData"
            class="space-y-4"
          >
            <!-- Quantity (Required) -->
            <UFormField :label="t('pages.addProduct.inventory.form.quantity')" name="quantity" required>
            <UInput
              v-model.number="inventoryFormData.quantity"
              type="number"
              step="0.01"
              min="0.01"
              :placeholder="t('pages.addProduct.inventory.form.quantityPlaceholder')"
              :disabled="isCreatingInventory"
              class="w-full"
            />
          </UFormField>

          <!-- Unit (Required) -->
          <UFormField :label="t('pages.addProduct.inventory.form.unit')" name="unit" required>
            <USelect
              v-model="inventoryFormData.unit"
              :items="unitOptions"
              :placeholder="t('pages.addProduct.inventory.form.unitPlaceholder')"
              :disabled="isCreatingInventory"
              class="w-full"
            />
          </UFormField>

          <!-- Expiration Date (Optional) -->
          <UFormField :label="t('pages.addProduct.inventory.form.expirationAt')" name="expirationAt">
            <UInputDate
              ref="expirationDateInput"
              v-model="inventoryFormData.expirationAt"
              :locale="inputDateLocale"
              :disabled="isCreatingInventory"
              class="w-full"
            >
              <template #trailing>
                <UPopover :reference="expirationDateInput?.inputsRef[0]?.$el">
                  <UButton
                    color="neutral"
                    variant="link"
                    size="sm"
                    icon="i-lucide-calendar"
                    aria-label="Select a date"
                    class="px-0"
                    :disabled="isCreatingInventory"
                  />
                  <template #content>
                    <UCalendar v-model="inventoryFormData.expirationAt" :locale="inputDateLocale" class="p-2" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </UFormField>

          <!-- Price (Optional) -->
          <UFormField :label="t('pages.addProduct.inventory.form.price')" name="price">
            <UInput
              v-model.number="inventoryFormData.price"
              type="number"
              step="0.01"
              min="0"
              :placeholder="t('pages.addProduct.inventory.form.pricePlaceholder')"
              :disabled="isCreatingInventory"
              class="w-full"
            />
          </UFormField>

          <!-- Currency (Optional - shown if price is set) -->
          <UFormField
            v-if="inventoryFormData.price !== undefined && inventoryFormData.price > 0"
            :label="t('pages.addProduct.inventory.form.currency')"
            name="currency"
          >
            <USelect
              v-model="inventoryFormData.currency"
              :items="currencyOptions"
              :placeholder="t('pages.addProduct.inventory.form.currencyPlaceholder')"
              :disabled="isCreatingInventory"
              class="w-full"
            />
          </UFormField>

          <!-- Receipt Number (Optional) -->
          <UFormField :label="t('pages.addProduct.inventory.form.receiptNumber')" name="receiptNumber">
            <UInput
              v-model="inventoryFormData.receiptNumber"
              :placeholder="t('pages.addProduct.inventory.form.receiptNumberPlaceholder')"
              :disabled="isCreatingInventory"
              class="w-full"
            />
          </UFormField>

          <!-- Is Shared with Family (Optional) -->
          <UFormField :label="t('pages.addProduct.inventory.form.isSharedWithFamily')" name="isSharedWithFamily">
            <UCheckbox
              v-model="inventoryFormData.isSharedWithFamily"
              :label="t('pages.addProduct.inventory.form.isSharedWithFamilyLabel')"
              :disabled="isCreatingInventory"
            />
          </UFormField>
        </UForm>

        <!-- Live Preview Card (shows current form state) -->
        <div v-if="isBulkFormValid" class="p-4 border border-primary-500 dark:border-primary-400 rounded-lg bg-primary-50 dark:bg-primary-900/20">
          <h4 class="text-sm font-semibold mb-3 flex items-center gap-2">
            <UIcon name="i-lucide-eye" class="h-4 w-4" />
            {{ t('pages.addProduct.inventory.preview.title') }}
          </h4>
          <div class="space-y-2 text-sm">
            <div class="flex justify-between">
              <span class="text-gray-600 dark:text-gray-400">{{ t('common.quantity') }}:</span>
              <span class="font-medium">
                {{ inventoryFormData.quantity }} {{ inventoryFormData.unit ? t(`enums.unit.${inventoryFormData.unit}`) : '' }}
              </span>
            </div>
            <div v-if="inventoryFormData.expirationAt" class="flex justify-between">
              <span class="text-gray-600 dark:text-gray-400">{{ t('common.expirationDate') }}:</span>
              <span class="font-medium">{{ formatCalendarDate(inventoryFormData.expirationAt) }}</span>
            </div>
            <div v-if="inventoryFormData.price && inventoryFormData.price > 0" class="flex justify-between">
              <span class="text-gray-600 dark:text-gray-400">{{ t('common.price') }}:</span>
              <span class="font-medium">
                {{ inventoryFormData.price }} {{ inventoryFormData.currency ? t(`enums.currency.${inventoryFormData.currency}`) : '' }}
              </span>
            </div>
            <div v-if="inventoryFormData.receiptNumber" class="flex justify-between">
              <span class="text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.form.receiptNumber') }}:</span>
              <span class="font-medium">{{ inventoryFormData.receiptNumber }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-gray-600 dark:text-gray-400">{{ t('pages.addProduct.inventory.form.isSharedWithFamily') }}:</span>
              <UIcon
                :name="inventoryFormData.isSharedWithFamily ? 'i-lucide-check' : 'i-lucide-x'"
                :class="inventoryFormData.isSharedWithFamily ? 'text-green-500' : 'text-gray-400'"
                class="h-5 w-5"
              />
            </div>
          </div>
        </div>

        <!-- Submit Button (not part of UForm, standalone) -->
        <UButton
          color="primary"
          block
          size="lg"
          :disabled="!isBulkFormValid"
          :loading="isCreatingInventory"
          icon="i-lucide-check"
          @click="onCreateBulkInventory"
        >
          {{ t('pages.addProduct.inventory.form.createButton') }}
        </UButton>
      </div>

      <!-- Individual Mode UI -->
      <div v-else-if="inventoryHandlingMode === 'individual'" class="space-y-6">
        <!-- Back to Mode Selection -->
        <div class="flex items-center justify-between">
          <h3 class="text-lg font-semibold">
            {{ t('pages.addProduct.inventory.individual.title') }}
          </h3>
          <UButton
            :label="t('common.back')"
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
            size="sm"
            @click="handleBackToModeSelection"
          />
        </div>

        <!-- Shared Context Info -->
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

          <!-- Shared with Family Checkbox -->
          <UFormField :label="t('pages.addProduct.inventory.form.isSharedWithFamily')" name="isSharedWithFamily">
            <UCheckbox
              v-model="inventoryFormData.isSharedWithFamily"
              :label="t('pages.addProduct.inventory.form.isSharedWithFamilyLabel')"
            />
          </UFormField>
        </div>

        <!-- Add Item Form -->
        <div class="p-6 border-2 border-dashed border-gray-300 dark:border-gray-700 rounded-lg">
          <h4 class="text-md font-semibold mb-4">
            {{ t('pages.addProduct.inventory.addItemForm.title') }}
          </h4>

          <UForm
            :schema="individualItemSchema"
            :state="inventoryFormData"
            class="space-y-4"
            @submit="onAddIndividualItem"
          >
            <!-- Quantity + Unit (side by side) -->
            <div class="grid grid-cols-2 gap-4">
              <UFormField :label="t('pages.addProduct.inventory.form.quantity')" name="quantity" required>
                <UInput
                  v-model.number="inventoryFormData.quantity"
                  type="number"
                  step="0.01"
                  min="0.01"
                  :placeholder="t('pages.addProduct.inventory.form.quantityPlaceholder')"
                  class="w-full"
                />
              </UFormField>

              <UFormField :label="t('pages.addProduct.inventory.form.unit')" name="unit" required>
                <USelect
                  v-model="inventoryFormData.unit"
                  :items="unitOptions"
                  :placeholder="t('pages.addProduct.inventory.form.unitPlaceholder')"
                  class="w-full"
                />
              </UFormField>
            </div>

            <!-- Expiration Date -->
            <UFormField :label="t('pages.addProduct.inventory.form.expirationAt')" name="expirationAt">
              <UInputDate
                ref="expirationDateInput"
                v-model="inventoryFormData.expirationAt"
                :locale="inputDateLocale"
                class="w-full"
              >
                <template #trailing>
                  <UPopover :reference="expirationDateInput?.inputsRef[0]?.$el">
                    <UButton
                      color="neutral"
                      variant="link"
                      size="sm"
                      icon="i-lucide-calendar"
                      aria-label="Select a date"
                      class="px-0"
                    />
                    <template #content>
                      <UCalendar v-model="inventoryFormData.expirationAt" :locale="inputDateLocale" class="p-2" />
                    </template>
                  </UPopover>
                </template>
              </UInputDate>
            </UFormField>

            <!-- Price + Currency -->
            <div class="grid grid-cols-2 gap-4">
              <UFormField :label="t('pages.addProduct.inventory.form.price')" name="price">
                <UInput
                  v-model.number="inventoryFormData.price"
                  type="number"
                  step="0.01"
                  min="0"
                  :placeholder="t('pages.addProduct.inventory.form.pricePlaceholder')"
                  class="w-full"
                />
              </UFormField>

              <UFormField
                v-if="inventoryFormData.price !== undefined && inventoryFormData.price > 0"
                :label="t('pages.addProduct.inventory.form.currency')"
                name="currency"
              >
                <USelect
                  v-model="inventoryFormData.currency"
                  :items="currencyOptions"
                  :placeholder="t('pages.addProduct.inventory.form.currencyPlaceholder')"
                  class="w-full"
                />
              </UFormField>
            </div>

            <!-- Receipt Number -->
            <UFormField :label="t('pages.addProduct.inventory.form.receiptNumber')" name="receiptNumber">
              <UInput
                v-model="inventoryFormData.receiptNumber"
                :placeholder="t('pages.addProduct.inventory.form.receiptNumberPlaceholder')"
                class="w-full"
              />
            </UFormField>

            <!-- Add Button -->
            <UButton
              type="submit"
              color="primary"
              block
              icon="i-lucide-plus"
            >
              {{ t('pages.addProduct.inventory.addItemForm.addButton') }}
            </UButton>
          </UForm>
        </div>

        <!-- Items List -->
        <div v-if="individualItems.length > 0" class="space-y-4">
          <h4 class="text-md font-semibold">
            {{ t('pages.addProduct.inventory.itemsList.title') }} ({{ individualItems.length }})
          </h4>

          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div
              v-for="item in individualItems"
              :key="item.id"
              class="relative p-4 border border-gray-300 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-800"
            >
              <!-- Remove button -->
              <button
                v-if="individualItems.length > 1"
                class="absolute top-2 right-2 text-gray-400 hover:text-red-500 dark:hover:text-red-400 transition-colors"
                @click="removeIndividualItem(item.id)"
              >
                <UIcon name="i-lucide-x" class="h-5 w-5" />
              </button>

              <!-- Item details -->
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

          <!-- Preview All Items Button -->
          <UButton
            color="primary"
            block
            size="lg"
            icon="i-lucide-eye"
            @click="showIndividualItemsPreview"
          >
            {{ t('pages.addProduct.inventory.preview.viewAllButton', { count: individualItems.length }) }}
          </UButton>
        </div>

        <!-- Empty State -->
        <div v-else class="text-center py-12 text-gray-500">
          <UIcon name="i-lucide-package-x" class="h-12 w-12 mx-auto mb-3 text-gray-400" />
          <p>{{ t('pages.addProduct.inventory.itemsList.empty') }}</p>
        </div>
      </div>
    </div>

    <!-- Preview Modal -->
    <InventoryItemsPreviewModal
      v-if="isPreviewModalOpen"
      :is-open="isPreviewModalOpen"
      :items="inventoryHandlingMode === 'bulk' && inventoryFormData.unit
        ? [{
            id: '1',
            quantity: inventoryFormData.quantity,
            unit: inventoryFormData.unit,
            expirationAt: inventoryFormData.expirationAt || null,
            price: inventoryFormData.price,
            currency: inventoryFormData.currency,
            receiptNumber: inventoryFormData.receiptNumber
          }]
        : individualItems"
      :storage-location-name="getStorageLocationName()"
      :shopping-location-name="selectedShoppingLocationId ? getShoppingLocationName() : undefined"
      :is-shared-with-family="inventoryFormData.isSharedWithFamily ?? true"
      @update:is-open="isPreviewModalOpen = $event"
      @confirm="handlePreviewConfirm"
    />

    <!-- Progress Modal -->
    <InventoryCreationProgressModal
      :is-open="isProgressModalOpen"
      :items="progressItems"
      :is-processing="isCreatingMultiple"
      @update:is-open="isProgressModalOpen = $event"
      @close="handleProgressModalClose"
    />
  </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { watchDebounced } from '@vueuse/core'
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { CalendarDate } from '@internationalized/date'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useLocationsApi } from '~/composables/api/useLocationsApi'
import { useOpenFoodFactsApi } from '~/composables/api/useOpenFoodFactsApi'
import type { ProductInfo, CreateProductRequest, CreateInventoryItemRequest } from '~/types/product'
import type { StorageLocationInfo, StorageLocationRequest, ShoppingLocationInfo, ShoppingLocationRequest } from '~/types/location'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'
import { Unit, Currency } from '~/types/enums'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

// Local type for form data with CalendarDate
interface InventoryFormData {
  productPublicId: string
  isSharedWithFamily?: boolean
  storageLocationPublicId?: string
  quantity: number
  unit?: Unit
  expirationAt?: any | null
  price?: number
  currency?: Currency
  shoppingLocationPublicId?: string
  receiptNumber?: string
}

const { getProducts, createProduct, createInventoryItem } = useProductsApi()
const { getStorageLocations, createStorageLocation, getShoppingLocations, createShoppingLocation } = useLocationsApi()
const { getProductByBarcode } = useOpenFoodFactsApi()
const { t } = useI18n()
const { inputDateLocale } = useInputDateLocale()
const toast = useToast()
const { showCameraButton } = useCameraAvailability()

// Stepper state
const currentStep = ref(0)
const stepperItems = computed(() => [
  { label: t('pages.addProduct.stepLabels.step1') },
  { label: t('pages.addProduct.stepLabels.step2') },
  { label: t('pages.addProduct.stepLabels.step3') },
  { label: t('pages.addProduct.stepLabels.step4') }
])

// Selected product
const selectedProductId = ref<string | null>(null)

// Selected storage location
const selectedStorageLocationId = ref<string | null>(null)

// Selected shopping location
const selectedShoppingLocationId = ref<string | null>(null)

// Tab state
const activeTab = ref(0)
const tabItems = computed(() => [
  { label: t('pages.addProduct.tabs.search'), value: 0, slot: 'search' },
  { label: t('pages.addProduct.tabs.create'), value: 1, slot: 'create' }
])

const locationTabItems = computed(() => [
  { label: t('pages.addProduct.location.tabs.search'), value: 0, slot: 'search' },
  { label: t('pages.addProduct.location.tabs.create'), value: 1, slot: 'create' }
])

const shoppingLocationTabItems = computed(() => [
  { label: t('pages.addProduct.shoppingLocation.tabs.search'), value: 0, slot: 'search' },
  { label: t('pages.addProduct.shoppingLocation.tabs.create'), value: 1, slot: 'create' }
])

// Search state
const searchQuery = ref('')
const searchResults = ref<ProductInfo[]>([])
const isSearching = ref(false)
const selectedCardId = ref<string | null>(null)

// OpenFoodFacts state
const isOpenFoodFactsModalOpen = ref(false)
const isQueryingBarcode = ref(false)
const openFoodFactsProduct = ref<OpenFoodFactsProduct | null>(null)
const isImageLoading = ref(true)

// Step 2: Location search state
const allLocations = ref<StorageLocationInfo[]>([])
const locationSearchQuery = ref('')
const isLoadingLocations = ref(false)
const selectedLocationCardId = ref<string | null>(null)
const currentLocationPage = ref(1)
const locationPageSize = 20
const loadingMoreLocations = ref(false)
const locationSentinelRef = ref<HTMLElement | null>(null)

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

// Step 2: Create location form state
const isCreatingLocation = ref(false)
const locationFormData = ref<StorageLocationRequest>({
  name: '',
  description: '',
  color: '',
  isFreezer: false,
  isSharedWithFamily: true
})

// Step 3: Shopping location search state
const allShoppingLocations = ref<ShoppingLocationInfo[]>([])
const shoppingLocationSearchQuery = ref('')
const isLoadingShoppingLocations = ref(false)
const selectedShoppingLocationCardId = ref<string | null>(null)
const currentShoppingLocationPage = ref(1)
const shoppingLocationPageSize = 20
const loadingMoreShoppingLocations = ref(false)
const shoppingLocationSentinelRef = ref<HTMLElement | null>(null)

// Step 3: Create shopping location form state
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

// Step 4: Create inventory item form state
const isCreatingInventory = ref(false)
const expirationDateInput = ref()
const inventoryFormData = ref<InventoryFormData>({
  productPublicId: '',
  quantity: 1,
  unit: undefined,
  expirationAt: null,
  price: undefined,
  currency: undefined,
  shoppingLocationPublicId: undefined,
  receiptNumber: undefined,
  isSharedWithFamily: true
})

// Inventory handling mode state (Step 3)
const inventoryHandlingMode = ref<'bulk' | 'individual' | null>(null)

// Individual items state
interface IndividualInventoryItem {
  id: string // temporary UUID for frontend tracking
  quantity: number
  unit: Unit
  expirationAt: any | null
  price: number | undefined
  currency: Currency | undefined
  receiptNumber: string | undefined
}

const individualItems = ref<IndividualInventoryItem[]>([])

// Progress modal state
const isProgressModalOpen = ref(false)
const isPreviewModalOpen = ref(false)
interface ProgressItem {
  id: string
  displayText: string
  status: 'pending' | 'in-progress' | 'success' | 'error'
  errorMessage?: string
}
const progressItems = ref<ProgressItem[]>([])
const isCreatingMultiple = ref(false)

// Unit options for dropdown
const unitOptions = computed(() => {
  return Object.entries(Unit)
    .filter(([key]) => isNaN(Number(key))) // Filter out numeric keys
    .map(([_key, value]) => ({
      label: t(`enums.unit.${value}`),
      value: value as Unit
    }))
})

// Currency options for dropdown
const currencyOptions = computed(() => {
  return [
    { label: t('enums.currency.135'), value: Currency.Huf },
    { label: t('enums.currency.105'), value: Currency.Eur },
    { label: t('enums.currency.279'), value: Currency.Usd }
  ]
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

// Zod schema for storage location
const createLocationSchema = z.object({
  name: z.string({ required_error: 'Storage location name is required' })
    .min(2, 'Name must be at least 2 characters')
    .max(128, 'Name must not exceed 128 characters'),
  description: z.string()
    .max(500, 'Description must not exceed 500 characters')
    .optional(),
  color: z.string()
    .regex(/^#[0-9A-Fa-f]{6}$/, 'Color must be a valid hex color (e.g., #FF5733)')
    .optional()
    .or(z.literal('')),
  isFreezer: z.boolean().optional().default(false),
  isSharedWithFamily: z.boolean().optional().default(true)
})

type CreateLocationSchema = z.output<typeof createLocationSchema>

// Zod schema for shopping location
const createShoppingLocationSchema = z.object({
  name: z.string({ required_error: 'Shopping location name is required' })
    .min(2, 'Name must be at least 2 characters')
    .max(128, 'Name must not exceed 128 characters'),
  description: z.string()
    .max(500, 'Description must not exceed 500 characters')
    .optional(),
  address: z.string()
    .max(128, 'Address must not exceed 128 characters')
    .optional(),
  city: z.string()
    .max(64, 'City must not exceed 64 characters')
    .optional(),
  postalCode: z.string()
    .max(20, 'Postal code must not exceed 20 characters')
    .optional(),
  country: z.string()
    .max(64, 'Country must not exceed 64 characters')
    .optional(),
  website: z.string()
    .url('Must be a valid URL')
    .max(255, 'URL must not exceed 255 characters')
    .optional()
    .or(z.literal('')),
  googleMaps: z.string()
    .url('Must be a valid URL')
    .max(255, 'URL must not exceed 255 characters')
    .optional()
    .or(z.literal('')),
  color: z.string()
    .regex(/^#[0-9A-Fa-f]{6}$/, 'Color must be a valid hex color')
    .optional()
    .or(z.literal('')),
  isSharedWithFamily: z.boolean().optional().default(true)
})

type CreateShoppingLocationSchema = z.output<typeof createShoppingLocationSchema>

// Zod schema for inventory item
const createInventorySchema = z.object({
  quantity: z.number({ required_error: 'Quantity is required' })
    .min(0.001, 'Quantity must be greater than 0'),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
  expirationAt: z.any().nullable().optional(),
  price: z.number()
    .min(0, 'Price must be positive')
    .optional(),
  currency: z.nativeEnum(Currency).optional(),
  receiptNumber: z.string()
    .max(50, 'Receipt number must not exceed 50 characters')
    .optional(),
  isSharedWithFamily: z.boolean().optional().default(true)
})

type CreateInventorySchema = z.output<typeof createInventorySchema>

// Zod schema for individual item (no isSharedWithFamily field)
const individualItemSchema = z.object({
  quantity: z.number({ required_error: 'Quantity is required' })
    .min(0.001, 'Quantity must be greater than 0'),
  unit: z.nativeEnum(Unit, { required_error: 'Unit is required' }),
  expirationAt: z.any().nullable().optional(),
  price: z.number().min(0).optional(),
  currency: z.nativeEnum(Currency).optional(),
  receiptNumber: z.string().max(50).optional()
})

// Computed: Check if bulk form has minimum valid data
const isBulkFormValid = computed(() => {
  return inventoryFormData.value.quantity > 0 && inventoryFormData.value.unit !== undefined
})

// Helper: Format CalendarDate for display
const formatCalendarDate = (date: CalendarDate): string => {
  return `${date.year}-${String(date.month).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`
}

// Ensure barcode contains only numeric characters
watch(() => formData.value.barcode, (newValue) => {
  if (newValue) {
    formData.value.barcode = newValue.replace(/\D/g, '')
  }
})

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
        // Sort products alphabetically by name
        const sortedItems = response.data.items.sort((a, b) => {
          return a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu')
        })
        searchResults.value = sortedItems
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

// Computed - client-side filtering for locations
const filteredLocations = computed(() => {
  let result = allLocations.value

  // Text search filter
  if (locationSearchQuery.value.trim()) {
    const searchTerm = locationSearchQuery.value.toLowerCase()
    result = result.filter(location =>
      location.name.toLowerCase().includes(searchTerm) ||
      (location.description && location.description.toLowerCase().includes(searchTerm))
    )
  }

  // Sort alphabetically by name
  result = result.sort((a, b) => {
    return a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu')
  })

  return result
})

// Paginated locations for display (lazy loading)
const displayedLocations = computed(() => {
  const startIndex = 0
  const endIndex = currentLocationPage.value * locationPageSize
  return filteredLocations.value.slice(startIndex, endIndex)
})

const hasMoreLocations = computed(() => {
  return displayedLocations.value.length < filteredLocations.value.length
})

// Load all locations
const loadLocations = async () => {
  isLoadingLocations.value = true
  try {
    const response = await getStorageLocations({ returnAll: true })
    if (response.success && response.data) {
      allLocations.value = response.data.items
    }
  } finally {
    isLoadingLocations.value = false
  }
}

// Intersection Observer for lazy loading locations
let locationObserver: IntersectionObserver | null = null

const setupLocationObserver = () => {
  if (locationSentinelRef.value && !locationObserver) {
    locationObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting && hasMoreLocations.value && !loadingMoreLocations.value) {
            loadingMoreLocations.value = true
            currentLocationPage.value++
            setTimeout(() => {
              loadingMoreLocations.value = false
            }, 100)
          }
        })
      },
      { threshold: 0.5 }
    )
    locationObserver.observe(locationSentinelRef.value)
  }
}

const cleanupLocationObserver = () => {
  if (locationObserver) {
    locationObserver.disconnect()
    locationObserver = null
  }
}

// Watch for when we enter Step 2 to load locations
watch(currentStep, async (newStep) => {
  // Always reset to search tab (tab0) when entering any step
  activeTab.value = 0

  if (newStep === 1 && allLocations.value.length === 0) {
    await loadLocations()
    await nextTick()
    setupLocationObserver()
  } else if (newStep !== 1) {
    cleanupLocationObserver()
  }

  // Step 3: Load shopping locations
  if (newStep === 2 && allShoppingLocations.value.length === 0) {
    await loadShoppingLocations()
    await nextTick()
    setupShoppingLocationObserver()
  } else if (newStep !== 2) {
    cleanupShoppingLocationObserver()
  }
})

// Reset tab to search when component mounts
onMounted(() => {
  activeTab.value = 0
})

// Reset pagination when search changes
watch(locationSearchQuery, () => {
  currentLocationPage.value = 1
})

// Computed - client-side filtering for shopping locations
const filteredShoppingLocations = computed(() => {
  let result = allShoppingLocations.value

  // Text search filter
  if (shoppingLocationSearchQuery.value.trim()) {
    const searchTerm = shoppingLocationSearchQuery.value.toLowerCase()
    result = result.filter(location =>
      location.name.toLowerCase().includes(searchTerm) ||
      (location.address && location.address.toLowerCase().includes(searchTerm)) ||
      (location.city && location.city.toLowerCase().includes(searchTerm)) ||
      (location.description && location.description.toLowerCase().includes(searchTerm))
    )
  }

  // Sort alphabetically by name
  result = result.sort((a, b) => {
    return a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu')
  })

  return result
})

// Paginated shopping locations for display (lazy loading)
const displayedShoppingLocations = computed(() => {
  const startIndex = 0
  const endIndex = currentShoppingLocationPage.value * shoppingLocationPageSize
  return filteredShoppingLocations.value.slice(startIndex, endIndex)
})

const hasMoreShoppingLocations = computed(() => {
  return displayedShoppingLocations.value.length < filteredShoppingLocations.value.length
})

// Load all shopping locations
const loadShoppingLocations = async () => {
  isLoadingShoppingLocations.value = true
  try {
    const response = await getShoppingLocations({ returnAll: true })
    if (response.success && response.data) {
      allShoppingLocations.value = response.data.items
    }
  } finally {
    isLoadingShoppingLocations.value = false
  }
}

// Intersection Observer for lazy loading shopping locations
let shoppingLocationObserver: IntersectionObserver | null = null

const setupShoppingLocationObserver = () => {
  if (shoppingLocationSentinelRef.value && !shoppingLocationObserver) {
    shoppingLocationObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting && hasMoreShoppingLocations.value && !loadingMoreShoppingLocations.value) {
            loadingMoreShoppingLocations.value = true
            currentShoppingLocationPage.value++
            setTimeout(() => {
              loadingMoreShoppingLocations.value = false
            }, 100)
          }
        })
      },
      { threshold: 0.5 }
    )
    shoppingLocationObserver.observe(shoppingLocationSentinelRef.value)
  }
}

const cleanupShoppingLocationObserver = () => {
  if (shoppingLocationObserver) {
    shoppingLocationObserver.disconnect()
    shoppingLocationObserver = null
  }
}

// Reset shopping location pagination when search changes
watch(shoppingLocationSearchQuery, () => {
  currentShoppingLocationPage.value = 1
})

// Stepper navigation handler - prevent forward navigation
const handleStepChange = (newStep: number) => {
  // Only allow backward navigation or staying on current step
  if (newStep <= currentStep.value) {
    currentStep.value = newStep
  }
  // Forward navigation is blocked - users must complete each step
}

// Handlers
const onProductCardClick = (product: ProductInfo) => {
  selectedCardId.value = product.publicId
  selectedProductId.value = product.publicId
  currentStep.value = 1
}

// OpenFoodFacts barcode query handler
const handleBarcodeQuery = async () => {
  // Validate barcode exists
  if (!formData.value.barcode || formData.value.barcode.trim() === '') {
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.openFoodFacts.noBarcodeError'),
      color: 'error'
    })
    return
  }

  isQueryingBarcode.value = true
  try {
    const response = await getProductByBarcode(formData.value.barcode.trim())
    
    // Only open modal if request was successful and has data
    if (response.success && response.data) {
      openFoodFactsProduct.value = response.data
      isImageLoading.value = true
      isOpenFoodFactsModalOpen.value = true
    } else {
      // Show error toast
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
    // Import product name and brand
    if (openFoodFactsProduct.value.product_name) {
      formData.value.name = openFoodFactsProduct.value.product_name
    }
    if (openFoodFactsProduct.value.brands) {
      formData.value.brand = openFoodFactsProduct.value.brands
    }
  }
  handleCancelImport()
}

const handleCancelImport = () => {
  isOpenFoodFactsModalOpen.value = false
  openFoodFactsProduct.value = null
  isImageLoading.value = true
}

// Barcode scanner handler for Create tab
const handleBarcodeScanned = (barcode: string) => {
  formData.value.barcode = barcode
  // Auto-trigger OpenFoodFacts query
  nextTick(() => {
    handleBarcodeQuery()
  })
}

// Barcode scanner handler for Search tab
const handleSearchBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Dynamic barcode handler - routes to correct handler based on active tab
const dynamicBarcodeHandler = (barcode: string) => {
  if (activeTab.value === 0) {
    // Search tab - just set search query
    handleSearchBarcodeScanned(barcode)
  } else {
    // Create tab - set barcode and trigger OpenFoodFacts query
    handleBarcodeScanned(barcode)
  }
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

// Location selection handler
const onLocationCardClick = (location: StorageLocationInfo) => {
  selectedLocationCardId.value = location.publicId
  selectedStorageLocationId.value = location.publicId
  currentStep.value = 2 // Advance to step 3
}

// Location creation handler
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
      selectedStorageLocationId.value = response.data.publicId
      currentStep.value = 2 // Advance to step 3
    }
  } catch (error) {
    console.error('Location creation failed:', error)
  } finally {
    isCreatingLocation.value = false
  }
}

// Shopping location selection handler
const onShoppingLocationCardClick = (location: ShoppingLocationInfo) => {
  selectedShoppingLocationCardId.value = location.publicId
  selectedShoppingLocationId.value = location.publicId
  currentStep.value = 3 // Advance to step 4
}

// Skip shopping location handler
const onSkipShoppingLocation = () => {
  selectedShoppingLocationCardId.value = null
  selectedShoppingLocationId.value = null
  currentStep.value = 3 // Advance to step 4
}

// Shopping location creation handler
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
      selectedShoppingLocationId.value = response.data.publicId
      currentStep.value = 3 // Advance to step 4
    }
  } catch (error) {
    console.error('Shopping location creation failed:', error)
  } finally {
    isCreatingShoppingLocation.value = false
  }
}

// Get storage location name for display
const getStorageLocationName = (): string => {
  if (!selectedStorageLocationId.value) return t('pages.addProduct.inventory.sharedContext.none')
  const location = allLocations.value.find(l => l.publicId === selectedStorageLocationId.value)
  return location?.name || t('pages.addProduct.inventory.sharedContext.none')
}

// Get shopping location name for display
const getShoppingLocationName = (): string => {
  if (!selectedShoppingLocationId.value) return t('pages.addProduct.inventory.sharedContext.none')
  const location = allShoppingLocations.value.find(l => l.publicId === selectedShoppingLocationId.value)
  return location?.name || t('pages.addProduct.inventory.sharedContext.none')
}

// Handle back to mode selection
const handleBackToModeSelection = () => {
  inventoryHandlingMode.value = null
  individualItems.value = []
  resetInventoryForm()
}

// Reset inventory form to defaults
const resetInventoryForm = () => {
  inventoryFormData.value = {
    productPublicId: '',
    quantity: 1,
    unit: undefined,
    expirationAt: null,
    price: undefined,
    currency: undefined,
    receiptNumber: undefined,
    isSharedWithFamily: true
  }
}

// Add individual item to list
const onAddIndividualItem = (event: FormSubmitEvent<any>) => {
  // Create new item with unique ID
  const newItem: IndividualInventoryItem = {
    id: crypto.randomUUID(),
    quantity: inventoryFormData.value.quantity,
    unit: inventoryFormData.value.unit!,
    expirationAt: inventoryFormData.value.expirationAt,
    price: inventoryFormData.value.price,
    currency: inventoryFormData.value.currency,
    receiptNumber: inventoryFormData.value.receiptNumber
  }

  individualItems.value.push(newItem)

  // Reset form for next item (keep unit for convenience)
  const previousUnit = inventoryFormData.value.unit
  resetInventoryForm()
  inventoryFormData.value.unit = previousUnit

  toast.add({
    title: t('toast.success'),
    description: t('pages.addProduct.inventory.itemAdded'),
    color: 'success'
  })
}

// Remove individual item from list
const removeIndividualItem = (itemId: string) => {
  if (individualItems.value.length <= 1) {
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.inventory.minimumOneItem'),
      color: 'error'
    })
    return
  }

  individualItems.value = individualItems.value.filter(item => item.id !== itemId)
}

// Convert CalendarDate to ISO string helper
const convertCalendarDateToISO = (date: CalendarDate | null): string | undefined => {
  if (!date) return undefined
  const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
  return localDate.toISOString()
}

// Build API request from item data
const buildInventoryRequest = (itemData: IndividualInventoryItem | InventoryFormData): CreateInventoryItemRequest => {
  return {
    productPublicId: selectedProductId.value!,
    storageLocationPublicId: selectedStorageLocationId.value || undefined,
    shoppingLocationPublicId: selectedShoppingLocationId.value || undefined,
    quantity: itemData.quantity,
    unit: itemData.unit!,
    expirationAt: convertCalendarDateToISO(itemData.expirationAt || null),
    price: itemData.price && itemData.price > 0 ? itemData.price : undefined,
    currency: itemData.price && itemData.price > 0 ? itemData.currency : undefined,
    receiptNumber: itemData.receiptNumber?.trim() || undefined,
    isSharedWithFamily: inventoryFormData.value.isSharedWithFamily
  }
}

// Format display text for progress items
const formatItemDisplay = (item: IndividualInventoryItem | InventoryFormData): string => {
  const qty = item.quantity
  const unitLabel = t(`enums.unit.${item.unit}`)
  const expiration = item.expirationAt
    ? formatCalendarDate(item.expirationAt)
    : t('pages.addProduct.progressModal.noExpiration')

  return `${qty} ${unitLabel} - ${t('common.expirationDate')}: ${expiration}`
}

// Show preview modal for individual items
const showIndividualItemsPreview = () => {
  if (individualItems.value.length === 0) {
    toast.add({
      title: t('toast.error'),
      description: t('pages.addProduct.progressModal.noItemsError'),
      color: 'error'
    })
    return
  }

  isPreviewModalOpen.value = true
}

// Bulk mode: Create directly without preview modal
const onCreateBulkInventory = async () => {
  // Validate form first
  try {
    createInventorySchema.parse(inventoryFormData.value)
  } catch (error) {
    if (error instanceof z.ZodError) {
      toast.add({
        title: t('toast.error'),
        description: error.errors[0].message,
        color: 'error'
      })
    }
    return
  }

  // Create directly without preview modal
  onConfirmAndCreateBulkInventory()
}

// Preview modal confirm handler (routes to correct create function)
const handlePreviewConfirm = () => {
  if (inventoryHandlingMode.value === 'bulk') {
    onConfirmAndCreateBulkInventory()
  } else {
    onConfirmAndCreateMultipleInventoryItems()
  }
}

// Confirm and create bulk inventory (called from preview modal)
const onConfirmAndCreateBulkInventory = async () => {
  // Close preview modal
  isPreviewModalOpen.value = false

  // Prepare single item for progress modal
  progressItems.value = [{
    id: '1',
    displayText: formatItemDisplay(inventoryFormData.value),
    status: 'pending' as const
  }]

  isProgressModalOpen.value = true
  isCreatingInventory.value = true
  isCreatingMultiple.value = true

  progressItems.value[0].status = 'in-progress'

  try {
    const inventoryData = buildInventoryRequest(inventoryFormData.value)
    const response = await createInventoryItem(inventoryData)

    if (response.success) {
      progressItems.value[0].status = 'success'

      // Auto-close and redirect after 1 second
      setTimeout(() => {
        window.location.href = '/products'
      }, 1000)
    } else {
      progressItems.value[0].status = 'error'
      progressItems.value[0].errorMessage = response.message || t('toast.error')
      isCreatingInventory.value = false
      isCreatingMultiple.value = false
    }
  } catch (error) {
    progressItems.value[0].status = 'error'
    progressItems.value[0].errorMessage = error instanceof Error ? error.message : t('toast.error')
    console.error('Inventory item creation failed:', error)
    isCreatingInventory.value = false
    isCreatingMultiple.value = false
  }
}

// Confirm and create multiple inventory items (called from preview modal)
const onConfirmAndCreateMultipleInventoryItems = async () => {
  // Close preview modal
  isPreviewModalOpen.value = false

  // Prepare progress items
  progressItems.value = individualItems.value.map(item => ({
    id: item.id,
    displayText: formatItemDisplay(item),
    status: 'pending' as const
  }))

  isProgressModalOpen.value = true
  isCreatingInventory.value = true
  isCreatingMultiple.value = true

  let successCount = 0
  let failureCount = 0

  // Sequential processing
  for (let i = 0; i < progressItems.value.length; i++) {
    progressItems.value[i].status = 'in-progress'

    try {
      const itemToCreate = buildInventoryRequest(individualItems.value[i])
      const response = await createInventoryItem(itemToCreate)

      if (response.success) {
        progressItems.value[i].status = 'success'
        successCount++
      } else {
        progressItems.value[i].status = 'error'
        progressItems.value[i].errorMessage = response.message || t('toast.error')
        failureCount++
      }
    } catch (error) {
      progressItems.value[i].status = 'error'
      progressItems.value[i].errorMessage = error instanceof Error ? error.message : t('toast.error')
      failureCount++
      console.error('Failed to create inventory item:', error)
    }

    // Small delay between requests for UX
    await new Promise(resolve => setTimeout(resolve, 300))
  }

  isCreatingMultiple.value = false
  isCreatingInventory.value = false

  // Auto-close and redirect if all succeeded
  if (failureCount === 0) {
    setTimeout(() => {
      window.location.href = '/products'
    }, 1000)
  }
}

// Progress modal close handler
const handleProgressModalClose = () => {
  if (!isCreatingMultiple.value && progressItems.value.every(item => item.status !== 'pending' && item.status !== 'in-progress')) {
    window.location.href = '/products'
  }
}

// Watch for step changes to reset mode
watch(currentStep, (newStep) => {
  if (newStep !== 3) {
    inventoryHandlingMode.value = null
    individualItems.value = []
  }
})

// Inventory item creation handler (legacy - keeping for backward compatibility if needed)
const onCreateInventory = async (event: FormSubmitEvent<CreateInventorySchema>) => {
  isCreatingInventory.value = true
  try {
    // Convert CalendarDate to ISO string at 12:00:00 local time
    let expirationAtString: string | undefined = undefined
    if (event.data.expirationAt) {
      const date = event.data.expirationAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      expirationAtString = localDate.toISOString()
    }

    const inventoryData: CreateInventoryItemRequest = {
      productPublicId: selectedProductId.value!,
      storageLocationPublicId: selectedStorageLocationId.value || undefined,
      shoppingLocationPublicId: selectedShoppingLocationId.value || undefined,
      quantity: event.data.quantity,
      unit: event.data.unit,
      expirationAt: expirationAtString,
      price: event.data.price,
      currency: event.data.currency,
      receiptNumber: event.data.receiptNumber?.trim() || undefined,
      isSharedWithFamily: event.data.isSharedWithFamily
    }

    const response = await createInventoryItem(inventoryData)
    if (response.success && response.data) {
      // Success - redirect to products page
      // Keep button disabled until navigation completes
      window.location.href = '/products'
    } else {
      // Only re-enable button if there was an error
      isCreatingInventory.value = false
    }
  } catch (error) {
    console.error('Inventory item creation failed:', error)
    // Only re-enable button on error
    isCreatingInventory.value = false
  }
}
</script>
