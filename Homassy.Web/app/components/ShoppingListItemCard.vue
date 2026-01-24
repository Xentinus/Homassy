<template>
  <div
    class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 p-3 cursor-pointer hover:shadow-lg hover:-translate-y-0.5 transition-all duration-200 flex flex-col overflow-hidden card-animate"
    :class="cardBorderClass"
    @click="handleCardClick"
  >
    <!-- Header: Title and Action Buttons -->
    <div class="flex items-start justify-between gap-2">
      <!-- Item Details -->
      <div class="flex-1 min-w-0 space-y-1">
        <!-- Product Name -->
        <h3 
          class="text-sm font-bold break-words text-gray-900 dark:text-white"
          v-html="highlightText(displayName, searchQuery)"
        />
        <!-- Brand -->
        <p 
          v-if="item.product?.brand" 
          class="text-xs text-gray-500 dark:text-gray-400 break-words font-medium"
          v-html="highlightText(item.product.brand, searchQuery)"
        />
      </div>

      <!-- Action Button & Image Icon -->
      <div class="flex items-center gap-0.5 opacity-100 sm:opacity-0 sm:group-hover:opacity-100 transition-opacity duration-200">
        <!-- Action Menu -->
        <UDropdownMenu :items="dropdownItems" size="md">
          <UButton
            icon="i-lucide-ellipsis-vertical"
            size="xs"
            variant="subtle"
            @click.stop
          />
        </UDropdownMenu>
      </div>
    </div>

    <!-- Values Section (at the bottom) -->
    <div class="mt-auto pt-4 space-y-2.5">
      <!-- Shopping Location with Google Maps -->
      <div v-if="item.shoppingLocation" class="flex items-center gap-2 text-xs">
        <UIcon name="i-lucide-map-pin" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400 flex-shrink-0" />
        <span class="text-gray-700 dark:text-gray-300 font-medium break-words line-clamp-1 flex-1">{{ item.shoppingLocation.name }}</span>
        <a
          v-if="item.shoppingLocation.googleMaps"
          :href="item.shoppingLocation.googleMaps"
          target="_blank"
          rel="noopener noreferrer"
          class="text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 transition-colors flex-shrink-0"
        >
          <UIcon name="i-lucide-external-link" class="h-3 w-3" />
        </a>
      </div>

      <!-- Quantity and Unit -->
      <div class="flex items-center gap-2 text-xs">
        <UIcon name="i-lucide-package-2" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0" />
        <span class="font-bold text-gray-900 dark:text-gray-100">{{ item.quantity }}</span>
        <span class="text-gray-700 dark:text-gray-300">{{ unitLabel }}</span>
      </div>

      <!-- Note (if not empty) -->
      <div v-if="item.note" class="flex items-start gap-2 text-xs">
        <UIcon name="i-lucide-sticky-note" class="h-3.5 w-3.5 text-purple-600 dark:text-purple-400 mt-0.5 flex-shrink-0" />
        <p 
          class="text-gray-600 dark:text-gray-400 italic line-clamp-2"
          v-html="highlightText(item.note, searchQuery)"
        />
      </div>

      <!-- Dates -->
      <div v-if="item.dueAt || item.deadlineAt" class="space-y-1.5">
        <div v-if="item.dueAt" class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-calendar-clock" class="h-3.5 w-3.5 text-orange-600 dark:text-orange-400 flex-shrink-0" />
          <span class="text-gray-600 dark:text-gray-400">{{ formatDate(item.dueAt) }}</span>
        </div>
        <div v-if="item.deadlineAt" class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-calendar-x" class="h-3.5 w-3.5 text-red-600 dark:text-red-400 flex-shrink-0" />
          <span class="text-gray-600 dark:text-gray-400">{{ formatDate(item.deadlineAt) }}</span>
        </div>
      </div>

      <!-- Purchased Badge -->
      <div v-if="item.purchasedAt" class="flex items-center gap-2 px-3 py-1.5 bg-gradient-to-r from-green-50 to-emerald-50 dark:from-green-900/30 dark:to-emerald-900/30 rounded-lg border border-green-300/50 dark:border-green-700/50 shadow-sm">
        <UIcon name="i-lucide-check-circle-2" class="h-4 w-4 text-green-600 dark:text-green-400 flex-shrink-0" />
        <span class="text-xs font-bold text-green-700 dark:text-green-300">{{ $t('common.purchased') }}</span>
      </div>
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
          <!-- Product Image (if available) -->
          <div v-if="item.product?.productPictureBase64" class="flex justify-center">
            <img
              :src="`data:image/jpeg;base64,${item.product.productPictureBase64}`"
              :alt="displayName"
              class="w-32 h-32 object-contain rounded-lg cursor-pointer hover:opacity-90 transition-opacity border border-gray-200 dark:border-gray-700"
              @click="openImageFromModal"
            >
          </div>
          
          <div class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg">
            <UIcon name="i-lucide-package" class="h-5 w-5 text-gray-500 mt-0.5" />
            <div class="flex-1 space-y-2">
              <div>
                <p class="font-medium break-words">{{ displayName }}</p>
                <p v-if="item.product?.brand" class="text-sm text-gray-600 dark:text-gray-400 break-words">{{ item.product.brand }}</p>
              </div>
              
              <!-- Quantity -->
              <div class="flex items-center gap-2 text-sm">
                <UIcon name="i-lucide-package-2" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0" />
                <span class="font-bold text-gray-900 dark:text-gray-100">{{ item.quantity }}</span>
                <span class="text-gray-700 dark:text-gray-300">{{ unitLabel }}</span>
              </div>

              <!-- Shopping Location -->
              <div v-if="item.shoppingLocation" class="flex items-center gap-2 text-sm">
                <UIcon name="i-lucide-map-pin" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400 flex-shrink-0" />
                <span class="text-gray-700 dark:text-gray-300 font-medium">{{ item.shoppingLocation.name }}</span>
              </div>

              <!-- Note -->
              <div v-if="item.note" class="flex items-start gap-2 text-sm">
                <UIcon name="i-lucide-sticky-note" class="h-3.5 w-3.5 text-purple-600 dark:text-purple-400 mt-0.5 flex-shrink-0" />
                <p class="text-gray-600 dark:text-gray-400 italic">{{ item.note }}</p>
              </div>

              <!-- Dates -->
              <div v-if="item.dueAt || item.deadlineAt" class="space-y-1">
                <div v-if="item.dueAt" class="flex items-center gap-2 text-sm">
                  <UIcon name="i-lucide-calendar-clock" class="h-3.5 w-3.5 text-orange-600 dark:text-orange-400 flex-shrink-0" />
                  <span class="text-gray-600 dark:text-gray-400">{{ formatDate(item.dueAt) }}</span>
                </div>
                <div v-if="item.deadlineAt" class="flex items-center gap-2 text-sm">
                  <UIcon name="i-lucide-calendar-x" class="h-3.5 w-3.5 text-red-600 dark:text-red-400 flex-shrink-0" />
                  <span class="text-gray-600 dark:text-gray-400">{{ formatDate(item.deadlineAt) }}</span>
                </div>
              </div>
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
        class="fixed inset-0 z-[100] bg-black/80 flex items-center justify-center p-4 cursor-pointer"
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

const { t, locale } = useI18n()
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

// Status checks for dates
const hasExpiredDate = computed(() => {
  const deadlineDate = props.item.deadlineAt ? new Date(props.item.deadlineAt) : null
  const dueDate = props.item.dueAt ? new Date(props.item.dueAt) : null
  const targetDate = deadlineDate && dueDate
    ? (deadlineDate < dueDate ? deadlineDate : dueDate)
    : (deadlineDate || dueDate)
  
  return targetDate ? checkIsExpired(targetDate) : false
})

const hasExpiringDate = computed(() => {
  const deadlineDate = props.item.deadlineAt ? new Date(props.item.deadlineAt) : null
  const dueDate = props.item.dueAt ? new Date(props.item.dueAt) : null
  const targetDate = deadlineDate && dueDate
    ? (deadlineDate < dueDate ? deadlineDate : dueDate)
    : (deadlineDate || dueDate)
  
  return targetDate ? checkIsExpiringWithinTwoWeeks(targetDate) : false
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
    return 'border-green-400 dark:border-green-500'
  }

  // Check if has expired or expiring dates
  if (hasExpiredDate.value) {
    return 'border-red-400 dark:border-red-500'
  }

  if (hasExpiringDate.value) {
    return 'border-primary-400 dark:border-primary-500'
  }

  // Default border
  return 'border-gray-200 dark:border-gray-700'
})

// Methods
const formatDate = (dateString: string): string => {
  const localeCode = locale.value === 'hu' ? 'hu-HU' : 'en-US'
  return new Date(dateString).toLocaleDateString(localeCode, { 
    year: 'numeric', 
    month: '2-digit', 
    day: '2-digit' 
  })
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

// Open image from modal - close modal first, then open overlay
const openImageFromModal = () => {
  isPurchaseModalOpen.value = false
  // Use setTimeout to ensure modal closes before opening overlay
  setTimeout(() => {
    isImageOverlayOpen.value = true
  }, 100)
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
