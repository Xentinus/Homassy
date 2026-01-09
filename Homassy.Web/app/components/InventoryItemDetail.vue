<template>
  <div class="rounded-lg border p-4 transition-all" :class="borderColorClass">
    <!-- Header: Quantity + Actions -->
    <div class="flex items-center justify-between mb-3">
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-package-2" class="h-5 w-5 text-primary-500" />
        <span class="text-lg font-semibold">
          {{ item.currentQuantity }} {{ $t(`enums.unit.${item.unit}`) }}
        </span>
      </div>

      <!-- Action Buttons -->
      <UFieldGroup size="sm" orientation="horizontal">
        <UButton
          :label="$t('pages.products.details.consume.button')"
          icon="i-lucide-utensils"
          size="sm"
          @click="openConsumeModal"
        />
        <UDropdownMenu :items="dropdownItems" size="md">
          <UButton
            icon="i-lucide-ellipsis-vertical"
            size="sm"
            variant="subtle"
          />
        </UDropdownMenu>
      </UFieldGroup>
    </div>

    <!-- Storage Location Section -->
    <div v-if="item.storageLocation" class="mb-3 space-y-1 text-sm">
      <h4 class="font-medium text-gray-700 dark:text-gray-300 flex items-center gap-2">
        <UIcon name="i-lucide-warehouse" class="h-4 w-4" />
        {{ $t('pages.products.details.storageLocation') }}
      </h4>
      <div class="text-gray-600 dark:text-gray-400">
        <div>
          {{ item.storageLocation.name }}
        </div>
      </div>
    </div>

    <!-- Expiration Section -->
    <div v-if="item.expirationAt" class="mb-3 space-y-1 text-sm">
      <h4 class="font-medium text-gray-700 dark:text-gray-300 flex items-center gap-2">
        <UIcon :name="expirationIcon" class="h-4 w-4" />
        {{ $t('pages.products.details.expirationInfo') }}
      </h4>
      <div class="text-gray-600 dark:text-gray-400" :class="expirationTextColorClass">
        <div>
          <span class="font-medium">{{ $t('common.expirationDate') }}:</span>
          {{ formatDate(item.expirationAt) }}
        </div>
      </div>
    </div>

    <!-- Purchase Info Section -->
    <div v-if="item.purchaseInfo" class="mb-3 space-y-1 text-sm">
      <h4 class="font-medium text-gray-700 dark:text-gray-300 flex items-center gap-2">
        <UIcon name="i-lucide-shopping-cart" class="h-4 w-4" />
        {{ $t('pages.products.details.purchaseInfo') }}
      </h4>
      <div class="text-gray-600 dark:text-gray-400">
        <div v-if="item.purchaseInfo.purchasedAt">
          <span class="font-medium">{{ $t('pages.products.details.purchasedAt') }}:</span>
          {{ formatDate(item.purchaseInfo.purchasedAt) }}
        </div>
        <div v-if="item.purchaseInfo.shoppingLocation">
          <span class="font-medium">{{ $t('pages.products.details.shoppingLocation') }}:</span>
          {{ item.purchaseInfo.shoppingLocation.name }}
        </div>
        <div>
          <span class="font-medium">{{ $t('pages.products.details.originalQuantity') }}:</span>
          {{ item.purchaseInfo.originalQuantity }} {{ $t(`enums.unit.${item.unit}`) }}
        </div>
        <div v-if="item.purchaseInfo.price && item.purchaseInfo.currency">
          <span class="font-medium">{{ $t('common.price') }}:</span>
          {{ item.purchaseInfo.price }} {{ $t(`enums.currency.${item.purchaseInfo.currency}`) }}
        </div>
        <div v-if="item.purchaseInfo.price && item.purchaseInfo.currency && item.purchaseInfo.originalQuantity > 0">
          <span class="font-medium">{{ $t('pages.products.details.pricePerUnit') }}:</span>
          {{ formatUnitPrice(item.purchaseInfo.price, item.purchaseInfo.originalQuantity) }} {{ $t(`enums.currency.${item.purchaseInfo.currency}`) }}
        </div>
      </div>
    </div>

    <!-- Consumption History Section -->
    <div v-if="item.purchaseInfo || item.consumptionLogs.length > 0" class="space-y-2">
      <button
        class="flex items-center gap-2 text-sm font-medium text-primary-600 dark:text-primary-400 hover:text-primary-700 dark:hover:text-primary-300 transition-colors"
        @click="isHistoryExpanded = !isHistoryExpanded"
      >
        <UIcon name="i-lucide-history" class="h-4 w-4" />
        <UIcon :name="isHistoryExpanded ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" class="h-4 w-4" />
        {{ isHistoryExpanded ? $t('pages.products.details.hideConsumptionHistory') : $t('pages.products.details.viewConsumptionHistory') }}
      </button>

      <Transition
        enter-active-class="transition-all duration-300 ease-out"
        enter-from-class="max-h-0 opacity-0"
        enter-to-class="max-h-[500px] opacity-100"
        leave-active-class="transition-all duration-300 ease-in"
        leave-from-class="max-h-[500px] opacity-100"
        leave-to-class="max-h-0 opacity-0"
      >
        <div v-show="isHistoryExpanded" class="overflow-hidden">
          <UTimeline
            color="primary"
            size="sm"
            :items="timelineItems"
            :default-value="timelineItems.length - 1"
            :ui="{ description: 'whitespace-pre-line' }"
          />
        </div>
      </Transition>
    </div>

    <!-- Consume Modal -->
    <UModal
      :open="isConsumeModalOpen"
      @update:open="(val) => isConsumeModalOpen = val"
      :dismissible="false"
    >
      <template #title>
        {{ $t('pages.products.details.consume.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.consume.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <div class="space-y-2">
            <label for="consume-quantity" class="block text-sm font-medium">
              {{ $t('pages.products.details.consume.quantityLabel') }}
            </label>
            <UInput
              id="consume-quantity"
              v-model="consumeQuantity"
              type="number"
              :min="0"
              :max="item.currentQuantity"
              :placeholder="$t('pages.products.details.consume.quantityLabel')"
              class="w-full"
            />
            <p class="text-xs text-gray-500 dark:text-gray-400">
              {{ $t('pages.products.details.consume.maxQuantity', { max: item.currentQuantity }) }}
            </p>

            <!-- Stepper Buttons -->
            <div class="flex gap-2">
              <!-- Subtract buttons -->
              <div class="flex gap-1">
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  icon="i-lucide-minus"
                  @click="adjustQuantity(-10)"
                >
                  10
                </UButton>
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  icon="i-lucide-minus"
                  @click="adjustQuantity(-1)"
                >
                  1
                </UButton>
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  icon="i-lucide-minus"
                  @click="adjustQuantity(-0.1)"
                >
                  0.1
                </UButton>
              </div>

              <!-- Add buttons -->
              <div class="flex gap-1">
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  icon="i-lucide-plus"
                  @click="adjustQuantity(0.1)"
                >
                  0.1
                </UButton>
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  icon="i-lucide-plus"
                  @click="adjustQuantity(1)"
                >
                  1
                </UButton>
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  icon="i-lucide-plus"
                  @click="adjustQuantity(10)"
                >
                  10
                </UButton>
              </div>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.products.details.consume.cancel')"
            color="neutral"
            variant="outline"
            @click="closeConsumeModal"
          />
          <UButton
            :label="$t('pages.products.details.consume.confirm')"
            :loading="isConsuming"
            @click="handleConsume"
          />
        </div>
      </template>
    </UModal>

    <!-- Edit Modal -->
    <UModal :open="isEditModalOpen" @update:open="(val) => isEditModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.editModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.editModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Quantity -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.quantityLabel') }}
            </label>
            <UInput
              v-model="editForm.quantity"
              type="number"
              :min="0"
            />
          </div>

          <!-- Unit -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.unitLabel') }}
            </label>
            <USelect
              :model-value="editForm.unit ?? undefined"
              :items="unitOptions"
              class="w-full"
              @update:model-value="(val) => editForm.unit = val ?? null"
            />
          </div>

          <!-- Expiration Date -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.expirationLabel') }}
            </label>
            <UInputDate
              ref="expirationDateInput"
              v-model="editForm.expirationAt"
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
                    <UCalendar v-model="editForm.expirationAt" :locale="inputDateLocale" class="p-2" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </div>

          <!-- Price -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.priceLabel') }}
            </label>
            <UInput
              v-model="editForm.price"
              type="number"
              :min="0"
              step="0.01"
            />
          </div>

          <!-- Currency -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.currencyLabel') }}
            </label>
            <USelect
              :model-value="editForm.currency ?? undefined"
              :items="currencyOptions"
              class="w-full"
              @update:model-value="(val) => editForm.currency = val ?? null"
            />
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.products.details.editModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeEditModal"
          />
          <UButton
            :label="$t('pages.products.details.editModal.confirm')"
            :loading="isUpdating"
            @click="handleUpdate"
          />
        </div>
      </template>
    </UModal>

    <!-- Delete Modal -->
    <UModal :open="isDeleteModalOpen" @update:open="(val) => isDeleteModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.deleteModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.deleteModal.description') }}
      </template>

      <template #body>
        <div class="space-y-3">
          <!-- Product Name -->
          <div v-if="productName">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.products.details.deleteModal.productName') }}:
            </span>
            <span class="text-sm ml-2">{{ productName }}</span>
          </div>

          <!-- Current Quantity -->
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.products.details.deleteModal.currentQuantity') }}:
            </span>
            <span class="text-sm ml-2">
              {{ item.currentQuantity }} {{ $t(`enums.unit.${item.unit}`) }}
            </span>
          </div>

          <!-- Expiration Date -->
          <div v-if="item.expirationAt">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ $t('pages.products.details.deleteModal.expirationDate') }}:
            </span>
            <span class="text-sm ml-2">{{ formatDate(item.expirationAt) }}</span>
          </div>

          <!-- Purchase Information -->
          <div v-if="item.purchaseInfo" class="pt-2 border-t border-gray-200 dark:border-gray-700">
            <div v-if="item.purchaseInfo.purchasedAt" class="mb-2">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteModal.purchaseDate') }}:
              </span>
              <span class="text-sm ml-2">{{ formatDate(item.purchaseInfo.purchasedAt) }}</span>
            </div>

            <div v-if="item.purchaseInfo.originalQuantity">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteModal.originalQuantity') }}:
              </span>
              <span class="text-sm ml-2">
                {{ item.purchaseInfo.originalQuantity }} {{ $t(`enums.unit.${item.unit}`) }}
              </span>
            </div>

            <div v-if="item.purchaseInfo.price && item.purchaseInfo.currency">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.deleteModal.purchasePrice') }}:
              </span>
              <span class="text-sm ml-2">
                {{ item.purchaseInfo.price }} {{ $t(`enums.currency.${item.purchaseInfo.currency}`) }}
              </span>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.products.details.deleteModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeDeleteModal"
          />
          <UButton
            :label="$t('pages.products.details.deleteModal.confirm')"
            color="error"
            :loading="isDeleting"
            @click="handleDelete"
          />
        </div>
      </template>
    </UModal>

    <!-- Move Modal -->
    <UModal :open="isMoveModalOpen" @update:open="(val) => isMoveModalOpen = val" :dismissible="false">
      <template #title>
        {{ $t('pages.products.details.moveModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.moveModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <!-- Current Location (if exists) -->
          <div v-if="item.storageLocation" class="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <div class="text-sm">
              <span class="font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.details.moveModal.currentLocation') }}:
              </span>
              <span class="ml-2 text-gray-900 dark:text-gray-100">
                {{ item.storageLocation.name }}
              </span>
            </div>
          </div>

          <!-- Target Location -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.moveModal.targetLocation') }} <span class="text-red-500">*</span>
            </label>
            <USelect
              v-model="selectedStorageId"
              :items="storageOptions"
              :loading="isLoadingStorages"
              :disabled="isMoving"
              :placeholder="$t('pages.products.details.moveModal.targetLocationPlaceholder')"
              class="w-full"
            />
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            :label="$t('pages.products.details.moveModal.cancel')"
            color="neutral"
            variant="outline"
            @click="closeMoveModal"
          />
          <UButton
            :label="$t('pages.products.details.moveModal.move')"
            :loading="isMoving"
            :disabled="isLoadingStorages || !selectedStorageId"
            @click="handleMove"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { InventoryItemInfo, ConsumeInventoryItemRequest, UpdateInventoryItemRequest, MoveInventoryItemsRequest } from '../types/product'
import { Unit, Currency, SelectValueType } from '../types/enums'
import type { SelectValue } from '../types/selectValue'
import type { CalendarDate } from '@internationalized/date'
import { CalendarDate as CalendarDateClass } from '@internationalized/date'

interface TimelineItem {
  date?: string
  title?: string
  description?: string
  icon?: string
}

interface Props {
  item: InventoryItemInfo
  productName?: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  consumed: []
  updated: []
  deleted: []
}>()

const { t: $t } = useI18n()
const { formatDate } = useDateFormat()
const { inputDateLocale } = useInputDateLocale()
const { consumeInventoryItem, updateInventoryItem, deleteInventoryItem, moveInventoryItems } = useProductsApi()
const { getSelectValues } = useSelectValueApi()
const toast = useToast()

// State
const isHistoryExpanded = ref(false)
const isConsumeModalOpen = ref(false)
const consumeQuantity = ref<number | null>(null)
const isConsuming = ref(false)

// Edit modal state
const isEditModalOpen = ref(false)
const expirationDateInput = ref()
const editForm = ref<{
  quantity: number | null
  unit: Unit | null
  expirationAt: CalendarDate | null
  price: number | null
  currency: Currency | null
}>({
  quantity: null,
  unit: null,
  expirationAt: null,
  price: null,
  currency: null
})
const isUpdating = ref(false)

// Delete modal state
const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

// Move modal state
const isMoveModalOpen = ref(false)
const isMoving = ref(false)
const isLoadingStorages = ref(false)
const selectedStorageId = ref<string | null>(null)
const storageOptions = ref<{ label: string; value: string }[]>([])

// Dropdown menu items
const dropdownItems = computed(() => [
  [
    {
      label: $t('pages.products.details.editInventory'),
      icon: 'i-lucide-pencil',
      onSelect: openEditModal
    },
    {
      label: $t('pages.products.details.moveItem'),
      icon: 'i-lucide-move',
      onSelect: openMoveModal
    },
    {
      label: $t('pages.products.details.deleteItem'),
      icon: 'i-lucide-trash-2',
      color: 'error' as const,
      onSelect: openDeleteModal
    }
  ]
])

// Select options
const unitOptions = computed(() => {
  return Object.entries(Unit)
    .filter(([key]) => isNaN(Number(key))) // Filter out numeric keys
    .map(([_key, value]) => ({
      label: $t(`enums.unit.${value}`),
      value: value as Unit
    }))
})

const currencyOptions = computed(() => [
  { label: $t('enums.currency.135'), value: Currency.Huf },
  { label: $t('enums.currency.105'), value: Currency.Eur },
  { label: $t('enums.currency.279'), value: Currency.Usd }
])

// Computed
const isExpired = computed(() => {
  if (!props.item.expirationAt) return false
  const now = new Date()
  const expirationDate = new Date(props.item.expirationAt)
  return expirationDate < now
})

const isExpiringSoon = computed(() => {
  if (!props.item.expirationAt || isExpired.value) return false
  const now = new Date()
  const twoWeeksFromNow = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000)
  const expirationDate = new Date(props.item.expirationAt)
  return expirationDate >= now && expirationDate <= twoWeeksFromNow
})

const borderColorClass = computed(() => {
  if (isExpired.value) return 'border-red-500 bg-red-50 dark:bg-red-900/20'
  if (isExpiringSoon.value) return 'border-primary-500 bg-primary-50 dark:bg-primary-900/20'
  return 'border-gray-300 dark:border-gray-700 bg-white dark:bg-gray-800'
})

const expirationTextColorClass = computed(() => {
  if (isExpired.value) return 'text-red-600 dark:text-red-400'
  if (isExpiringSoon.value) return 'text-primary-600 dark:text-primary-400'
  return 'text-gray-600 dark:text-gray-400'
})

const expirationIcon = computed(() => {
  if (isExpired.value) return 'i-lucide-alert-circle'
  if (isExpiringSoon.value) return 'i-lucide-clock'
  return 'i-lucide-calendar'
})

const timelineItems = computed<TimelineItem[]>(() => {
  const items: TimelineItem[] = []

  // First item: Purchase (if exists)
  if (props.item.purchaseInfo) {
    items.push({
      date: formatDate(props.item.purchaseInfo.purchasedAt),
      title: $t('pages.products.details.purchaseInfo'),
      description: `${$t('pages.products.details.originalQuantity')}: ${props.item.purchaseInfo.originalQuantity} ${$t(`enums.unit.${props.item.unit}`)}`,
      icon: 'i-lucide-shopping-cart'
    })
  }

  // Then add consumption logs (in chronological order)
  const consumptionItems = props.item.consumptionLogs.map(log => ({
    date: formatDate(log.consumedAt),
    title: log.userName,
    description: `${$t('pages.products.details.consumedQuantity')}: ${log.consumedQuantity} ${$t(`enums.unit.${props.item.unit}`)}`,
    icon: 'i-lucide-utensils'
  }))

  return [...items, ...consumptionItems]
})

// Methods
const formatUnitPrice = (price: number, quantity: number): string => {
  const unitPrice = price / quantity

  // Check if it's a whole number
  if (Number.isInteger(unitPrice)) {
    return unitPrice.toString()
  }

  // Otherwise format with 2 decimal places
  return unitPrice.toFixed(2)
}

const openConsumeModal = () => {
  // Set default value to 1, or current quantity if less than 1
  consumeQuantity.value = Math.min(1, props.item.currentQuantity)
  isConsumeModalOpen.value = true
}

const closeConsumeModal = () => {
  isConsumeModalOpen.value = false
  consumeQuantity.value = null
}

const adjustQuantity = (amount: number) => {
  const currentValue = consumeQuantity.value || 0
  const newValue = currentValue + amount

  // Ensure value stays within bounds (0 to currentQuantity)
  if (newValue < 0) {
    consumeQuantity.value = 0
  } else if (newValue > props.item.currentQuantity) {
    consumeQuantity.value = props.item.currentQuantity
  } else {
    // Round to 1 decimal place to avoid floating point precision issues
    consumeQuantity.value = Math.round(newValue * 10) / 10
  }
}

const handleConsume = async () => {
  // Validate quantity
  if (!consumeQuantity.value || consumeQuantity.value <= 0) {
    toast.add({
      title: $t('toast.error'),
      description: $t('pages.products.details.consume.maxQuantity', { max: props.item.currentQuantity }),
      color: 'error'
    })
    return
  }

  if (consumeQuantity.value > props.item.currentQuantity) {
    toast.add({
      title: $t('toast.error'),
      description: $t('pages.products.details.consume.maxQuantity', { max: props.item.currentQuantity }),
      color: 'error'
    })
    return
  }

  isConsuming.value = true

  try {
    const request: ConsumeInventoryItemRequest = {
      quantity: consumeQuantity.value
    }

    const response = await consumeInventoryItem(props.item.publicId, request)

    if (response.success) {
      closeConsumeModal()
      emit('consumed')
    }
  } catch (error) {
    console.error('Failed to consume inventory item:', error)
  } finally {
    isConsuming.value = false
  }
}

// Edit modal methods
const openEditModal = () => {
  // Pre-fill form with current values
  // Convert ISO string to CalendarDate for expirationAt if it exists
  let expirationDate: CalendarDate | null = null
  if (props.item.expirationAt) {
    const date = new Date(props.item.expirationAt)
    expirationDate = new CalendarDateClass(date.getFullYear(), date.getMonth() + 1, date.getDate())
  }

  editForm.value = {
    quantity: props.item.currentQuantity,
    unit: props.item.unit,
    expirationAt: expirationDate,
    price: props.item.purchaseInfo?.price || null,
    currency: props.item.purchaseInfo?.currency || null
  }
  isEditModalOpen.value = true
}

const closeEditModal = () => {
  isEditModalOpen.value = false
  editForm.value = {
    quantity: null,
    unit: null,
    expirationAt: null,
    price: null,
    currency: null
  }
}

const handleUpdate = async () => {
  isUpdating.value = true

  try {
    // Convert CalendarDate to ISO string for API
    let expirationAtString: string | undefined = undefined
    if (editForm.value.expirationAt) {
      const date = editForm.value.expirationAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      expirationAtString = localDate.toISOString()
    }

    const request: UpdateInventoryItemRequest = {
      quantity: editForm.value.quantity || undefined,
      unit: editForm.value.unit || undefined,
      expirationAt: expirationAtString,
      price: editForm.value.price || undefined,
      currency: editForm.value.currency || undefined
    }

    const response = await updateInventoryItem(props.item.publicId, request)

    if (response.success) {
      closeEditModal()
      emit('updated')
    }
  } catch (error) {
    console.error('Failed to update inventory item:', error)
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
    const response = await deleteInventoryItem(props.item.publicId)

    if (response.success) {
      closeDeleteModal()
      emit('deleted')
    }
  } catch (error) {
    console.error('Failed to delete inventory item:', error)
  } finally {
    isDeleting.value = false
  }
}

// Move modal methods
const openMoveModal = async () => {
  isMoveModalOpen.value = true
  await loadStorageLocations()
}

const closeMoveModal = () => {
  isMoveModalOpen.value = false
  selectedStorageId.value = null
  storageOptions.value = []
}

const loadStorageLocations = async () => {
  isLoadingStorages.value = true
  try {
    const response = await getSelectValues(SelectValueType.StorageLocation)
    if (response.success && response.data) {
      storageOptions.value = response.data.map((s: SelectValue) => ({
        label: s.text,
        value: s.publicId
      }))
    }
  } catch (error) {
    console.error('Failed to load storage locations:', error)
  } finally {
    isLoadingStorages.value = false
  }
}

const handleMove = async () => {
  if (!selectedStorageId.value) {
    return
  }

  isMoving.value = true

  try {
    const request: MoveInventoryItemsRequest = {
      inventoryItemPublicIds: [props.item.publicId],
      storageLocationPublicId: selectedStorageId.value
    }

    const response = await moveInventoryItems(request)

    if (response.success) {
      closeMoveModal()
      emit('updated')
    }
  } catch (error) {
    console.error('Failed to move inventory item:', error)
  } finally {
    isMoving.value = false
  }
}
</script>
