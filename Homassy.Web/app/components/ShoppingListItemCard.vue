<template>
  <div
    class="relative bg-white dark:bg-gray-800 rounded-lg border p-4 space-y-3 cursor-pointer hover:shadow-md transition-shadow"
    :class="cardBorderClass"
    @click="handleCardClick"
  >
    <div class="flex gap-4">
      <!-- Product Image -->
      <div v-if="item.product?.productPictureBase64" class="flex-shrink-0">
        <img
          :src="`data:image/jpeg;base64,${item.product.productPictureBase64}`"
          :alt="displayName"
          class="w-20 h-20 md:w-24 md:h-24 object-contain rounded-lg cursor-pointer hover:opacity-90 transition-opacity"
          @click.stop="isImageOverlayOpen = true"
        >
      </div>
      <div v-else class="flex-shrink-0 w-20 h-20 md:w-24 md:h-24 bg-gray-100 dark:bg-gray-700 rounded-lg flex items-center justify-center">
        <UIcon name="i-lucide-package" class="h-10 w-10 text-gray-400 dark:text-gray-500" />
      </div>

      <!-- Item Details -->
      <div class="flex-1 min-w-0 space-y-2">
        <!-- Product Name -->
        <div class="break-words">
          <h3 
            class="text-lg font-semibold break-words"
            v-html="highlightText(displayName, searchQuery)"
          />
          <p 
            v-if="item.product?.brand" 
            class="text-sm text-gray-600 dark:text-gray-400 break-words"
            v-html="highlightText(item.product.brand, searchQuery)"
          />
        </div>

        <!-- Shopping Location with Google Maps -->
        <div v-if="item.shoppingLocation" class="flex items-center gap-2 text-sm">
          <UIcon name="i-lucide-map-pin" class="h-4 w-4 text-gray-500" />
          <span class="text-gray-700 dark:text-gray-300">{{ item.shoppingLocation.name }}</span>
          <a
            v-if="item.shoppingLocation.googleMaps"
            :href="item.shoppingLocation.googleMaps"
            target="_blank"
            rel="noopener noreferrer"
            class="text-primary-500 hover:text-primary-600 dark:hover:text-primary-400 transition-colors"
          >
            <UIcon name="i-lucide-external-link" class="h-4 w-4" />
          </a>
        </div>

        <!-- Quantity and Unit -->
        <div class="flex items-center gap-2 text-sm">
          <UIcon name="i-lucide-package-2" class="h-4 w-4 text-gray-500" />
          <span class="font-medium text-gray-900 dark:text-gray-100">
            {{ item.quantity }} {{ unitLabel }}
          </span>
        </div>

        <!-- Note (if not empty) -->
        <div v-if="item.note" class="flex items-start gap-2 text-sm">
          <UIcon name="i-lucide-sticky-note" class="h-4 w-4 text-gray-500 mt-0.5" />
          <p 
            class="text-gray-600 dark:text-gray-400 italic"
            v-html="highlightText(item.note, searchQuery)"
          />
        </div>

        <!-- Dates -->
        <div class="flex flex-wrap gap-3 text-xs">
          <div v-if="item.dueAt" class="flex items-center gap-1 text-gray-600 dark:text-gray-400">
            <UIcon name="i-lucide-calendar-clock" class="h-3 w-3" />
            <span>{{ $t('common.due') }}: {{ formatDate(item.dueAt) }}</span>
          </div>
          <div v-if="item.deadlineAt" class="flex items-center gap-1 text-gray-600 dark:text-gray-400">
            <UIcon name="i-lucide-calendar-x" class="h-3 w-3" />
            <span>{{ $t('common.deadline') }}: {{ formatDate(item.deadlineAt) }}</span>
          </div>
        </div>

        <!-- Purchased Badge -->
        <div v-if="item.purchasedAt" class="inline-flex items-center gap-1 px-2 py-1 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 rounded text-xs font-medium">
          <UIcon name="i-lucide-check-circle" class="h-3 w-3" />
          <span>{{ $t('common.purchased') }}</span>
        </div>
      </div>

      <!-- Action Buttons -->
      <UDropdownMenu :items="dropdownItems" size="md" class="flex-shrink-0 self-start">
        <UButton
          icon="i-lucide-ellipsis-vertical"
          size="sm"
          variant="subtle"
          @click.stop
        />
      </UDropdownMenu>
    </div>

    <!-- Purchase Confirmation Modal -->
    <UModal :open="isPurchaseModalOpen" @update:open="(val) => isPurchaseModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('shoppingList.markAsPurchased') }}
      </template>

      <template #description>
        {{ $t('shoppingList.purchaseConfirmation') }}
      </template>

      <template #body>
        <div class="space-y-3">
          <div class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg">
            <UIcon name="i-lucide-package" class="h-5 w-5 text-gray-500 mt-0.5" />
            <div class="flex-1">
              <p class="font-medium">{{ displayName }}</p>
              <p v-if="item.product?.brand" class="text-sm text-gray-600 dark:text-gray-400">{{ item.product.brand }}</p>
              <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">{{ item.quantity }} {{ unitLabel }}</p>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('common.cancel')"
            variant="subtle"
            @click="isPurchaseModalOpen = false"
          />
          <UButton
            :label="$t('common.confirm')"
            color="success"
            :loading="isQuickPurchasing"
            @click="confirmPurchase"
          />
        </div>
      </template>
    </UModal>

    <!-- Edit Modal -->
    <UModal :open="isEditModalOpen" @update:open="(val) => isEditModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('shoppingList.editItem') }}
      </template>

      <template #description>
        {{ $t('shoppingList.editItemDescription') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Custom Name (only if no product) -->
          <div v-if="!item.product">
            <label class="block text-sm font-medium mb-1">
              {{ $t('shoppingList.customName') }}
            </label>
            <UInput
              v-model="editForm.customName"
              type="text"
              class="w-full"
            />
          </div>

          <!-- Quantity -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.quantity') }}
            </label>
            <UInput
              v-model="editForm.quantity"
              type="number"
              :min="0.001"
              step="0.1"
              class="w-full"
            />
          </div>

          <!-- Unit -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.unit') }}
            </label>
            <USelect
              :model-value="editForm.unit ?? undefined"
              :items="unitOptions"
              class="w-full"
              @update:model-value="(val) => editForm.unit = val ?? null"
            />
          </div>

          <!-- Note -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.note') }}
            </label>
            <UTextarea
              v-model="editForm.note"
              :placeholder="$t('common.note')"
              class="w-full"
            />
          </div>

          <!-- Due Date -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.dueDate') }}
            </label>
            <UInputDate
              v-model="editForm.dueAt"
              :locale="inputDateLocale"
              class="w-full"
            >
              <template #trailing>
                <UPopover>
                  <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" />
                  <template #content>
                    <UCalendar v-model="editForm.dueAt" :locale="inputDateLocale" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </div>

          <!-- Deadline -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('common.deadline') }}
            </label>
            <UInputDate
              v-model="editForm.deadlineAt"
              :locale="inputDateLocale"
              class="w-full"
            >
              <template #trailing>
                <UPopover>
                  <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" />
                  <template #content>
                    <UCalendar v-model="editForm.deadlineAt" :locale="inputDateLocale" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
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
        {{ $t('shoppingList.deleteItem') }}
      </template>

      <template #description>
        {{ $t('shoppingList.deleteItemWarning') }}
      </template>

      <template #body>
        <div class="space-y-3">
          <!-- Item Name -->
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('common.name') }}:
            </span>
            <span class="text-sm ml-2">{{ displayName }}</span>
          </div>

          <!-- Quantity -->
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('common.quantity') }}:
            </span>
            <span class="text-sm ml-2">
              {{ item.quantity }} {{ unitLabel }}
            </span>
          </div>

          <!-- Note if exists -->
          <div v-if="item.note">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('common.note') }}:
            </span>
            <span class="text-sm ml-2">{{ item.note }}</span>
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

    <!-- Restore Purchase Modal -->
    <UModal :open="isRestoreModalOpen" @update:open="(val) => isRestoreModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('shoppingList.restorePurchase') }}
      </template>

      <template #description>
        {{ $t('shoppingList.restorePurchaseWarning') }}
      </template>

      <template #body>
        <div class="p-4 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg">
          <p class="text-sm text-amber-800 dark:text-amber-200">
            {{ $t('shoppingList.restorePurchaseInventoryWarning') }}
          </p>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('common.cancel')"
            color="neutral"
            variant="outline"
            @click="closeRestoreModal"
          />
          <UButton
            :label="$t('common.confirm')"
            color="primary"
            :loading="isRestoring"
            @click="handleRestorePurchase"
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
        v-if="isImageOverlayOpen && item.product?.productPictureBase64"
        class="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4 cursor-pointer"
        @click.stop="isImageOverlayOpen = false"
        @keydown.esc="isImageOverlayOpen = false"
      >
        <img
          :src="`data:image/jpeg;base64,${item.product.productPictureBase64}`"
          :alt="displayName"
          class="max-w-full max-h-full object-contain"
        >
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ShoppingListItemInfo } from '../types/shoppingList'
import { Unit } from '../types/enums'
import type { CalendarDate } from '@internationalized/date'
import { CalendarDate as CalendarDateClass } from '@internationalized/date'

interface Props {
  item: ShoppingListItemInfo
  searchQuery?: string
}

const props = withDefaults(defineProps<Props>(), {
  searchQuery: ''
})

const emit = defineEmits<{
  refresh: []
  deleted: []
}>()

const { t } = useI18n()
const { inputDateLocale } = useInputDateLocale()
const { quickPurchaseShoppingListItem, restorePurchaseShoppingListItem, updateShoppingListItem, deleteShoppingListItem } = useShoppingListApi()
const { isExpired: checkIsExpired, isExpiringWithinTwoWeeks: checkIsExpiringWithinTwoWeeks } = useExpirationCheck()
const toast = useToast()

// State
const isImageOverlayOpen = ref(false)
const isPurchaseModalOpen = ref(false)
const isQuickPurchasing = ref(false)
const isRestoring = ref(false)
const isEditModalOpen = ref(false)
const isDeleteModalOpen = ref(false)
const isRestoreModalOpen = ref(false)
const isUpdating = ref(false)
const isDeleting = ref(false)

// Edit form state
const editForm = ref<{
  customName: string | null
  quantity: number | null
  unit: number | null
  note: string | null
  dueAt: CalendarDate | null
  deadlineAt: CalendarDate | null
}>({
  customName: null,
  quantity: null,
  unit: null,
  note: null,
  dueAt: null,
  deadlineAt: null
})

// Computed
const displayName = computed(() => {
  return props.item.product?.name || props.item.customName || 'Unnamed Item'
})

const unitLabel = computed(() => {
  if (props.item.unit === null || props.item.unit === undefined) return ''
  return t(`enums.unit.${props.item.unit}`)
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

// Unit options for edit form
const unitOptions = computed(() => {
  return Object.entries(Unit)
    .filter(([key]) => isNaN(Number(key)))
    .map(([_key, value]) => ({
      label: t(`enums.unit.${value}`),
      value: value as number
    }))
})

const cardBorderClass = computed(() => {
  // If purchased, show green border
  if (props.item.purchasedAt) {
    return 'border-green-500 dark:border-green-600'
  }

  // Check deadline date
  const deadlineDate = props.item.deadlineAt ? new Date(props.item.deadlineAt) : null
  const dueDate = props.item.dueAt ? new Date(props.item.dueAt) : null

  // Use the earlier of deadline or due date
  const targetDate = deadlineDate && dueDate
    ? (deadlineDate < dueDate ? deadlineDate : dueDate)
    : (deadlineDate || dueDate)

  if (!targetDate) {
    return 'border-gray-200 dark:border-gray-700'
  }

  // If date has passed, show danger (red)
  if (checkIsExpired(targetDate)) {
    return 'border-red-500 dark:border-red-600'
  }

  // If within 2 weeks, show primary (blue)
  if (checkIsExpiringWithinTwoWeeks(targetDate)) {
    return 'border-primary-500 dark:border-primary-600'
  }

  // Default border
  return 'border-gray-200 dark:border-gray-700'
})

// Methods
const formatDate = (dateString: string): string => {
  return new Date(dateString).toLocaleDateString()
}

// Card click handler
const handleCardClick = (event: MouseEvent) => {
  // Ignore clicks on interactive elements
  const target = event.target as HTMLElement
  if (target.closest('a, button, img')) return
  
  // If already purchased, open restore modal
  if (props.item.purchasedAt) {
    isRestoreModalOpen.value = true
  } else {
    // If not purchased, open purchase modal
    isPurchaseModalOpen.value = true
  }
}

// Confirm purchase from modal
const confirmPurchase = async () => {
  await handleQuickPurchase()
  isPurchaseModalOpen.value = false
}

const handleQuickPurchase = async () => {
  isQuickPurchasing.value = true
  try {
    const response = await quickPurchaseShoppingListItem(props.item.publicId)
    if (response.success) {
      emit('refresh')
    }
  } catch (error) {
    console.error('Failed to quick purchase item:', error)
  } finally {
    isQuickPurchasing.value = false
  }
}

const handleRestorePurchase = async () => {
  isRestoring.value = true
  try {
    const response = await restorePurchaseShoppingListItem(props.item.publicId)
    if (response.success) {
      closeRestoreModal()
      emit('refresh')
    }
  } catch (error) {
    console.error('Failed to restore purchase:', error)
  } finally {
    isRestoring.value = false
  }
}

// Edit modal methods
const openEditModal = () => {
  // Convert ISO strings to CalendarDate
  let dueDate: CalendarDate | null = null
  if (props.item.dueAt) {
    const date = new Date(props.item.dueAt)
    dueDate = new CalendarDateClass(date.getFullYear(), date.getMonth() + 1, date.getDate())
  }

  let deadlineDate: CalendarDate | null = null
  if (props.item.deadlineAt) {
    const date = new Date(props.item.deadlineAt)
    deadlineDate = new CalendarDateClass(date.getFullYear(), date.getMonth() + 1, date.getDate())
  }

  editForm.value = {
    customName: props.item.customName || null,
    quantity: props.item.quantity,
    unit: props.item.unit,
    note: props.item.note || null,
    dueAt: dueDate,
    deadlineAt: deadlineDate
  }
  isEditModalOpen.value = true
}

const closeEditModal = () => {
  isEditModalOpen.value = false
}

const handleUpdate = async () => {
  isUpdating.value = true
  try {
    // Convert CalendarDate to ISO string for API
    let dueAtString: string | undefined = undefined
    if (editForm.value.dueAt) {
      const date = editForm.value.dueAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      dueAtString = localDate.toISOString()
    }

    let deadlineAtString: string | undefined = undefined
    if (editForm.value.deadlineAt) {
      const date = editForm.value.deadlineAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      deadlineAtString = localDate.toISOString()
    }

    const updateData = {
      quantity: editForm.value.quantity ?? undefined,
      unit: editForm.value.unit ?? undefined,
      note: editForm.value.note || undefined,
      dueAt: dueAtString,
      deadlineAt: deadlineAtString,
      customName: !props.item.product ? (editForm.value.customName || undefined) : undefined
    }

    const response = await updateShoppingListItem(props.item.publicId, updateData)
    if (response.success) {
      closeEditModal()
      emit('refresh')
    }
  } catch (error) {
    console.error('Failed to update shopping list item:', error)
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

const handleDelete = async () => {
  isDeleting.value = true
  try {
    const response = await deleteShoppingListItem(props.item.publicId)
    if (response.success) {
      closeDeleteModal()
      emit('deleted')
    }
  } catch (error) {
    console.error('Failed to delete shopping list item:', error)
  } finally {
    isDeleting.value = false
  }
}

// Restore modal methods
const openRestoreModal = () => {
  isRestoreModalOpen.value = true
}

const closeRestoreModal = () => {
  isRestoreModalOpen.value = false
}
</script>
