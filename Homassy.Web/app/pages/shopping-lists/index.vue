<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 border-b border-gray-200 dark:border-gray-800 space-y-3">
      <!-- Title Row -->
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('pages.shoppingLists.title') }}</h1>
        </div>
        <NuxtLink
          :to="{ path: '/shopping-lists/add-product', query: { listId: selectedListId } }"
          :class="{ 'pointer-events-none': !selectedListId }"
        >
          <UButton
            color="primary"
            size="sm"
            trailing-icon="i-lucide-plus"
            :disabled="!selectedListId"
          >
            {{ $t('common.add') }}
          </UButton>
        </NuxtLink>
      </div>

      <!-- Description -->
      <p class="text-gray-600 dark:text-gray-400">
        {{ $t('pages.shoppingLists.description') }}
      </p>

      <!-- SELECT and SEARCH Row (reversed from products) -->
      <div class="flex flex-col sm:flex-row gap-2">
        <!-- LEFT: Shopping List Select with Menu Button -->
        <UFieldGroup size="md" orientation="horizontal" class="flex-1">
          <USelect
            v-model="selectedListId"
            :items="shoppingListOptions"
            :placeholder="$t('pages.shoppingLists.selectPlaceholder')"
            :disabled="isLoadingLists || allShoppingLists.length === 0"
            class="flex-1"
          >
            <template #trailing>
              <div class="flex items-center gap-2">
                <!-- Family Icon -->
                <UIcon
                  v-if="currentListDetails?.isSharedWithFamily"
                  name="i-lucide-users"
                  class="h-4 w-4 text-primary-500"
                />
                <!-- Color Dot -->
                <div
                  v-if="currentListDetails?.color"
                  class="w-3 h-3 rounded-full"
                  :style="{ backgroundColor: currentListDetails.color }"
                />
              </div>
            </template>
          </USelect>
          <!-- Show + button when no list selected -->
          <UButton
            v-if="!selectedListId"
            icon="i-lucide-plus"
            color="primary"
            size="sm"
            @click="openCreateModal"
          />
          <!-- Show menu dropdown when list is selected -->
          <UDropdownMenu v-else :items="listMenuItems" size="md">
            <UButton
              icon="i-lucide-ellipsis-vertical"
              size="sm"
              variant="subtle"
            />
          </UDropdownMenu>
        </UFieldGroup>

        <!-- RIGHT: Search Input with Toggle -->
        <div class="flex-1 flex gap-2">
          <UFieldGroup size="md" orientation="horizontal" class="flex-1">
            <UInput
              v-model="searchQuery"
              :disabled="!isSearchEnabled"
              :placeholder="$t('pages.shoppingLists.searchPlaceholder')"
              class="flex-1"
            >
              <template #trailing>
                <UButton
                  v-if="searchQuery"
                  icon="i-lucide-x"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :disabled="!isSearchEnabled"
                  @click="searchQuery = ''"
                />
              </template>
            </UInput>
            <BarcodeScannerButton
              v-if="showCameraButton"
              :disabled="!isSearchEnabled"
              @scanned="handleBarcodeScanned"
            />
          </UFieldGroup>
          <UButton
            :icon="showPurchased ? 'i-lucide-eye' : 'i-lucide-eye-off'"
            size="md"
            color="neutral"
            variant="outline"
            :disabled="!selectedListId"
            :aria-label="$t('pages.shoppingLists.showPurchasedToggle')"
            @click="showPurchased = !showPurchased"
          />
        </div>
      </div>
    </div>

    <!-- Content Section (with padding for fixed header) -->
    <div class="pt-64 px-4 sm:px-8 lg:px-14 pb-6">
      <!-- Loading State -->
      <div v-if="isLoadingDetails" class="space-y-3">
        <USkeleton class="h-32 w-full" />
        <USkeleton class="h-32 w-full" />
        <USkeleton class="h-32 w-full" />
      </div>

      <!-- Empty: No Lists -->
      <div v-else-if="!isLoadingLists && allShoppingLists.length === 0" class="text-center py-12">
        <UIcon name="i-lucide-shopping-cart" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
        <p class="text-gray-500 dark:text-gray-400">
          {{ $t('pages.shoppingLists.noListsFound') }}
        </p>
      </div>

      <!-- Empty: No Items in List -->
      <div v-else-if="currentListDetails && currentListDetails.items.length === 0" class="text-center py-12">
        <UIcon name="i-lucide-package-open" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
        <p class="text-gray-500 dark:text-gray-400">
          {{ $t('pages.shoppingLists.noItemsInList') }}
        </p>
      </div>

      <!-- Empty: No Search Results -->
      <div v-else-if="filteredItems.length === 0" class="text-center py-12">
        <p class="text-gray-500 dark:text-gray-400">
          {{ $t('pages.shoppingLists.noSearchResults') }}
        </p>
      </div>

      <!-- Items Grid -->
      <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <ShoppingListItemCard
          v-for="item in filteredItems"
          :key="item.publicId"
          :item="item"
          :search-query="searchQuery"
          @refresh="handleItemRefresh"
          @deleted="handleItemRefresh"
        />
      </div>
    </div>

    <!-- Create Shopping List Modal -->
    <UModal :open="isCreateModalOpen" @update:open="(val) => isCreateModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.shoppingLists.createModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.shoppingLists.createModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Name -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.shoppingLists.createModal.nameLabel') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="createForm.name"
              type="text"
              :placeholder="$t('pages.shoppingLists.createModal.namePlaceholder')"
              required
              class="w-full"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.shoppingLists.createModal.descriptionLabel') }}
            </label>
            <UTextarea
              v-model="createForm.description"
              :rows="3"
              :placeholder="$t('pages.shoppingLists.createModal.descriptionPlaceholder')"
              class="w-full"
            />
          </div>

          <!-- Color -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.shoppingLists.createModal.colorLabel') }}
            </label>
            <UInput
              v-model="createForm.color"
              type="color"
              class="w-full"
            />
          </div>

          <!-- Shared with Family -->
          <div class="flex items-center gap-2">
            <UCheckbox
              v-model="createForm.isSharedWithFamily"
              :label="$t('pages.shoppingLists.createModal.isSharedWithFamilyLabel')"
            />
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.shoppingLists.createModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeCreateModal"
          />
          <UButton
            :label="$t('pages.shoppingLists.createModal.confirm')"
            :loading="isCreating"
            @click="handleCreateList"
          />
        </div>
      </template>
    </UModal>

    <!-- Edit Shopping List Modal -->
    <UModal :open="isEditModalOpen" @update:open="(val) => isEditModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.shoppingLists.editModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.shoppingLists.editModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Name -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.shoppingLists.editModal.nameLabel') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="editForm.name"
              type="text"
              :placeholder="$t('pages.shoppingLists.editModal.namePlaceholder')"
              required
              class="w-full"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.shoppingLists.editModal.descriptionLabel') }}
            </label>
            <UTextarea
              v-model="editForm.description"
              :rows="3"
              :placeholder="$t('pages.shoppingLists.editModal.descriptionPlaceholder')"
              class="w-full"
            />
          </div>

          <!-- Color -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.shoppingLists.editModal.colorLabel') }}
            </label>
            <UInput
              v-model="editForm.color"
              type="color"
              class="w-full"
            />
          </div>

          <!-- Shared with Family -->
          <div class="flex items-center gap-2">
            <UCheckbox
              v-model="editForm.isSharedWithFamily"
              :label="$t('pages.shoppingLists.editModal.isSharedWithFamilyLabel')"
            />
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.shoppingLists.editModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeEditModal"
          />
          <UButton
            :label="$t('pages.shoppingLists.editModal.confirm')"
            :loading="isUpdating"
            @click="handleUpdateList"
          />
        </div>
      </template>
    </UModal>

    <!-- Delete Shopping List Modal -->
    <UModal :open="isDeleteModalOpen" @update:open="(val) => isDeleteModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.shoppingLists.deleteModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.shoppingLists.deleteModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Warning -->
          <div class="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
            <p class="text-sm font-medium text-red-600 dark:text-red-400">
              {{ $t('pages.shoppingLists.deleteModal.warning') }}
            </p>
          </div>

          <!-- List Details -->
          <div class="space-y-3">
            <!-- List Name -->
            <div>
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.shoppingLists.deleteModal.listName') }}:
              </span>
              <span class="text-sm ml-2">{{ currentListDetails?.name }}</span>
            </div>

            <!-- Description -->
            <div v-if="currentListDetails?.description">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.shoppingLists.deleteModal.description') }}:
              </span>
              <span class="text-sm ml-2">{{ currentListDetails.description }}</span>
            </div>

            <!-- Item Count -->
            <div class="pt-2 border-t border-gray-200 dark:border-gray-700">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.shoppingLists.deleteModal.itemCount') }}:
              </span>
              <span class="text-sm ml-2">{{ currentListDetails?.items.length || 0 }}</span>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.shoppingLists.deleteModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeDeleteModal"
          />
          <UButton
            :label="$t('pages.shoppingLists.deleteModal.confirm')"
            color="error"
            :loading="isDeleting"
            @click="handleDeleteList"
          />
        </div>
      </template>
    </UModal>

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="handleBarcodeScanned" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import type { SelectValue } from '../../types/selectValue'
import type { DetailedShoppingListInfo, CreateShoppingListRequest, UpdateShoppingListRequest, ShoppingListItemInfo } from '../../types/shoppingList'
import { SelectValueType } from '../../types/enums'
import { useSelectValueApi } from '../../composables/api/useSelectValueApi'
import { useShoppingListApi } from '../../composables/api/useShoppingListApi'
import { normalizeForSearch } from '../../utils/stringUtils'
import { useCameraAvailability } from '../../composables/useCameraAvailability'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { t: $t } = useI18n()
const { getSelectValues } = useSelectValueApi()
const { getShoppingListDetails, createShoppingList, updateShoppingList, deleteShoppingList } = useShoppingListApi()
const { showCameraButton } = useCameraAvailability()

// LocalStorage key for last selected shopping list
const LAST_SELECTED_LIST_KEY = 'lastSelectedShoppingListId'
const SHOW_PURCHASED_KEY = 'shoppingListsShowPurchased'

// State
const allShoppingLists = ref<SelectValue[]>([])
const selectedListId = ref<string | null>(null)
const currentListDetails = ref<DetailedShoppingListInfo | null>(null)
const searchQuery = ref('')
const showPurchased = ref(false)
const isLoadingLists = ref(false)
const isLoadingDetails = ref(false)

// Create modal state
const isCreateModalOpen = ref(false)
const createForm = ref<CreateShoppingListRequest>({
  name: '',
  description: undefined,
  color: undefined,
  isSharedWithFamily: undefined
})
const isCreating = ref(false)

// Edit modal state
const isEditModalOpen = ref(false)
const editForm = ref<UpdateShoppingListRequest>({
  name: undefined,
  description: undefined,
  color: undefined,
  isSharedWithFamily: undefined
})
const isUpdating = ref(false)

// Delete modal state
const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

// Helper function to get target date for an item
const getTargetDate = (item: ShoppingListItemInfo): Date | null => {
  const deadlineDate = item.deadlineAt ? new Date(item.deadlineAt) : null
  const dueDate = item.dueAt ? new Date(item.dueAt) : null

  // Use the earlier of deadline or due date
  return deadlineDate && dueDate
    ? (deadlineDate < dueDate ? deadlineDate : dueDate)
    : (deadlineDate || dueDate)
}

// Helper function to get display name for an item
const getDisplayName = (item: ShoppingListItemInfo): string => {
  return item.product?.name || item.customName || 'Unnamed Item'
}

// Computed
const shoppingListOptions = computed(() => {
  return allShoppingLists.value.map(list => ({
    label: list.text,
    value: list.publicId
  }))
})

const listMenuItems = computed(() => [
  [
    {
      label: $t('pages.shoppingLists.menu.add'),
      icon: 'i-lucide-plus',
      onSelect: openCreateModal
    }
  ],
  [
    {
      label: $t('pages.shoppingLists.menu.edit'),
      icon: 'i-lucide-pencil',
      onSelect: openEditModal
    },
    {
      label: $t('pages.shoppingLists.menu.delete'),
      icon: 'i-lucide-trash-2',
      onSelect: openDeleteModal
    }
  ]
])

const isSearchEnabled = computed(() => {
  return currentListDetails.value && currentListDetails.value.items.length > 0
})

const filteredItems = computed(() => {
  if (!currentListDetails.value) return []

  let items = currentListDetails.value.items

  // Filter purchased items
  if (!showPurchased.value) {
    items = items.filter(item => !item.purchasedAt)
  }

  // Search filter
  if (searchQuery.value.trim()) {
    const normalized = normalizeForSearch(searchQuery.value)
    items = items.filter(item => {
      const productName = item.product?.name || ''
      const productBrand = item.product?.brand || ''
      const customName = item.customName || ''
      const note = item.note || ''

      return normalizeForSearch(productName).includes(normalized) ||
             normalizeForSearch(productBrand).includes(normalized) ||
             normalizeForSearch(customName).includes(normalized) ||
             normalizeForSearch(note).includes(normalized)
    })
  }

  // Sort items by urgency and then alphabetically
  const now = new Date()
  const twoWeeksFromNow = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000)

  // Categorize items
  const overdueItems: ShoppingListItemInfo[] = []
  const dueSoonItems: ShoppingListItemInfo[] = []
  const otherItems: ShoppingListItemInfo[] = []

  items.forEach(item => {
    const targetDate = getTargetDate(item)

    if (targetDate && targetDate < now) {
      overdueItems.push(item)
    } else if (targetDate && targetDate <= twoWeeksFromNow) {
      dueSoonItems.push(item)
    } else {
      otherItems.push(item)
    }
  })

  // Sort each category alphabetically
  const sortAlphabetically = (a: ShoppingListItemInfo, b: ShoppingListItemInfo) => {
    const nameA = getDisplayName(a).toLowerCase()
    const nameB = getDisplayName(b).toLowerCase()
    return nameA.localeCompare(nameB, 'hu')
  }

  overdueItems.sort(sortAlphabetically)
  dueSoonItems.sort(sortAlphabetically)
  otherItems.sort(sortAlphabetically)

  // Concatenate in priority order
  return [...overdueItems, ...dueSoonItems, ...otherItems]
})

// Methods
const loadShoppingLists = async () => {
  isLoadingLists.value = true
  try {
    const response = await getSelectValues(SelectValueType.ShoppingList)

    if (response.success && response.data) {
      allShoppingLists.value = response.data
      
      // Try to restore last selected list from localStorage
      const lastSelectedId = localStorage.getItem(LAST_SELECTED_LIST_KEY)
      
      if (lastSelectedId && allShoppingLists.value.some(list => list.publicId === lastSelectedId)) {
        // If last selected list exists in the current list, select it
        selectedListId.value = lastSelectedId
      } else if (allShoppingLists.value.length > 0 && allShoppingLists.value[0]) {
        // Otherwise, auto-select first list if available
        selectedListId.value = allShoppingLists.value[0].publicId
      }
    }
  } catch (error) {
    console.error('Failed to load shopping lists:', error)
  } finally {
    isLoadingLists.value = false
  }
}

const loadListDetails = async (publicId: string) => {
  isLoadingDetails.value = true
  searchQuery.value = '' // Reset search when switching lists

  try {
    const response = await getShoppingListDetails(publicId)

    if (response.success && response.data) {
      currentListDetails.value = response.data
    }
  } catch (error) {
    console.error('Failed to load list details:', error)
  } finally {
    isLoadingDetails.value = false
  }
}

// Create modal methods
const openCreateModal = () => {
  createForm.value = {
    name: '',
    description: undefined,
    color: undefined,
    isSharedWithFamily: undefined
  }
  isCreateModalOpen.value = true
}

const closeCreateModal = () => {
  isCreateModalOpen.value = false
  createForm.value = {
    name: '',
    description: undefined,
    color: undefined,
    isSharedWithFamily: undefined
  }
}

const handleCreateList = async () => {
  if (!createForm.value.name || createForm.value.name.trim() === '') {
    return
  }

  isCreating.value = true

  try {
    const response = await createShoppingList(createForm.value)

    if (response.success && response.data) {
      closeCreateModal()
      // Reload shopping lists and select the new one
      await loadShoppingLists()
      selectedListId.value = response.data.publicId
    }
  } catch (error) {
    console.error('Failed to create shopping list:', error)
  } finally {
    isCreating.value = false
  }
}

// Edit modal methods
const openEditModal = () => {
  if (!currentListDetails.value) return

  editForm.value = {
    name: currentListDetails.value.name,
    description: currentListDetails.value.description || undefined,
    color: currentListDetails.value.color || undefined,
    isSharedWithFamily: currentListDetails.value.isSharedWithFamily
  }
  isEditModalOpen.value = true
}

const closeEditModal = () => {
  isEditModalOpen.value = false
  editForm.value = {
    name: undefined,
    description: undefined,
    color: undefined,
    isSharedWithFamily: undefined
  }
}

const handleUpdateList = async () => {
  if (!selectedListId.value) return

  isUpdating.value = true

  try {
    const response = await updateShoppingList(selectedListId.value, editForm.value)

    if (response.success) {
      closeEditModal()
      // Reload shopping lists and details
      await loadShoppingLists()
      await loadListDetails(selectedListId.value)
    }
  } catch (error) {
    console.error('Failed to update shopping list:', error)
  } finally {
    isUpdating.value = false
  }
}

// Delete modal methods
const openDeleteModal = () => {
  isDeleteModalOpen.value = true
}

const closeDeleteModal = () => {
  isDeleteModalOpen.value = false
}

const handleDeleteList = async () => {
  if (!selectedListId.value) return

  isDeleting.value = true

  try {
    const response = await deleteShoppingList(selectedListId.value)

    if (response.success) {
      closeDeleteModal()
      selectedListId.value = null
      currentListDetails.value = null
      // Reload shopping lists
      await loadShoppingLists()
    }
  } catch (error) {
    console.error('Failed to delete shopping list:', error)
  } finally {
    isDeleting.value = false
  }
}

// Handle item refresh (when item is updated, deleted, purchased, or restored)
const handleItemRefresh = async () => {
  if (selectedListId.value) {
    await loadListDetails(selectedListId.value)
  }
}

// Barcode scanner handler
const handleBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Watchers
watch(selectedListId, (newId) => {
  if (newId) {
    loadListDetails(newId)
    // Save to localStorage
    localStorage.setItem(LAST_SELECTED_LIST_KEY, newId)
  } else {
    currentListDetails.value = null
    // Clear from localStorage if no list is selected
    localStorage.removeItem(LAST_SELECTED_LIST_KEY)
  }
})

watch(showPurchased, (newValue) => {
  // Save to localStorage
  localStorage.setItem(SHOW_PURCHASED_KEY, String(newValue))
  
  // Reload list details
  if (selectedListId.value) {
    loadListDetails(selectedListId.value)
  }
})

// Lifecycle
onMounted(() => {
  // Restore showPurchased state from localStorage
  const savedShowPurchased = localStorage.getItem(SHOW_PURCHASED_KEY)
  if (savedShowPurchased !== null) {
    showPurchased.value = savedShowPurchased === 'true'
  }
  
  loadShoppingLists()
})
</script>
