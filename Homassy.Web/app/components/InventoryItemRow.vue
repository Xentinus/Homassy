<template>
  <div
    class="relative rounded-xl overflow-hidden slideInUp"
    style="touch-action: pan-y"
    data-no-pull-refresh
  >
    <!-- Swipe action layer (revealed behind the row while dragging) -->
    <div
      v-show="swipe.isSwiping.value"
      aria-hidden="true"
      class="absolute inset-0 rounded-xl flex items-center justify-between px-4"
      :class="swipe.direction.value === 'left' ? 'bg-error-500 dark:bg-error-600' : 'bg-primary-500 dark:bg-primary-600'"
    >
      <UIcon
        name="i-lucide-pencil"
        class="h-5 w-5 text-white transition-transform duration-150"
        :class="[
          swipe.direction.value === 'right' ? 'opacity-100' : 'opacity-0',
          swipe.progress.value >= 1 ? 'scale-125' : ''
        ]"
      />
      <UIcon
        name="i-lucide-trash-2"
        class="h-5 w-5 text-white transition-transform duration-150"
        :class="[
          swipe.direction.value === 'left' ? 'opacity-100' : 'opacity-0',
          swipe.progress.value >= 1 ? 'scale-125' : ''
        ]"
      />
    </div>

    <!-- Row surface (translates during swipe) -->
    <div
      ref="rowEl"
      class="relative rounded-xl border-2 p-4 cursor-pointer shadow-sm hover:shadow-lg transition-shadow duration-200 select-none"
      :class="[backgroundClass, borderClass]"
      :style="swipe.cardStyle.value"
      @click="handleRowClick"
    >
      <!-- Header: quantity -->
      <div class="flex items-center gap-3 mb-3">
        <UIcon :name="quantityIcon" class="h-6 w-6 shrink-0" :class="quantityIconColor" />
        <span class="text-lg font-semibold text-highlighted">
          {{ item.currentQuantity }} {{ $t(`enums.unit.${item.unit}`) }}
        </span>
      </div>

      <!-- Storage Location -->
      <div v-if="item.storageLocation" class="mb-2 flex items-center gap-2 text-sm">
        <UIcon name="i-lucide-warehouse" class="h-4 w-4 text-primary-600 dark:text-primary-400 shrink-0" />
        <span class="text-toned">{{ item.storageLocation.name }}</span>
      </div>

      <!-- Expiration -->
      <div v-if="item.expirationAt" class="mb-2 flex items-center gap-2 text-sm" :class="expirationTextColorClass">
        <UIcon :name="expirationIcon" class="h-4 w-4 shrink-0" />
        <span><span class="font-medium">{{ $t('common.expirationDate') }}:</span> {{ formatDate(item.expirationAt) }}</span>
      </div>

      <!-- Purchase Info -->
      <div v-if="item.purchaseInfo" class="mb-2 space-y-0.5 text-sm text-toned">
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-shopping-cart" class="h-4 w-4 text-pink-600 dark:text-pink-400 shrink-0" />
          <span class="font-medium text-highlighted">{{ $t('pages.products.details.purchaseInfo') }}</span>
        </div>
        <div class="pl-6 space-y-0.5">
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

    </div>

    <!-- Consume Modal -->
    <UModal
      :open="isConsumeModalOpen"
      :dismissible="false"
      @update:open="(val) => isConsumeModalOpen = val"
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

            <div class="flex gap-2">
              <div class="flex gap-1">
                <UButton size="xs" color="primary" variant="soft" icon="i-lucide-minus" @click="adjustQuantity(-10)">10</UButton>
                <UButton size="xs" color="primary" variant="soft" icon="i-lucide-minus" @click="adjustQuantity(-1)">1</UButton>
                <UButton size="xs" color="primary" variant="soft" icon="i-lucide-minus" @click="adjustQuantity(-0.1)">0.1</UButton>
              </div>
              <div class="flex gap-1">
                <UButton size="xs" color="primary" variant="soft" icon="i-lucide-plus" @click="adjustQuantity(0.1)">0.1</UButton>
                <UButton size="xs" color="primary" variant="soft" icon="i-lucide-plus" @click="adjustQuantity(1)">1</UButton>
                <UButton size="xs" color="primary" variant="soft" icon="i-lucide-plus" @click="adjustQuantity(10)">10</UButton>
              </div>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton :label="$t('pages.products.details.consume.cancel')" color="neutral" variant="outline" @click="closeConsumeModal" />
          <UButton :label="$t('pages.products.details.consume.confirm')" :loading="isConsuming" @click="handleConsume" />
        </div>
      </template>
    </UModal>

    <!-- Edit Modal -->
    <UModal :open="isEditModalOpen" :dismissible="false" @update:open="(val) => isEditModalOpen = val">
      <template #title>
        {{ $t('pages.products.details.editModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.editModal.description') }}
      </template>

      <template #body>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.quantityLabel') }}
            </label>
            <UInput v-model="editForm.quantity" type="number" :min="0" class="w-full">
              <template #trailing>
                <span class="text-sm text-gray-500 dark:text-gray-400">{{ $t(`enums.unit.${item.unit}`) }}</span>
              </template>
            </UInput>
          </div>

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
                  <UButton color="neutral" variant="link" size="sm" icon="i-lucide-calendar" aria-label="Select a date" class="px-0" />
                  <template #content>
                    <UCalendar v-model="editForm.expirationAt" :locale="inputDateLocale" class="p-2" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </div>

          <div>
            <label class="block text-sm font-medium mb-1">
              {{ $t('pages.products.details.editModal.priceLabel') }}
            </label>
            <UInput v-model="editForm.price" type="number" :min="0" step="0.01" />
          </div>

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
          <UButton :label="$t('pages.products.details.editModal.cancel')" color="neutral" variant="outline" @click="closeEditModal" />
          <UButton :label="$t('pages.products.details.editModal.confirm')" :loading="isUpdating" @click="handleUpdate" />
        </div>
      </template>
    </UModal>

    <!-- Delete Modal -->
    <UModal :open="isDeleteModalOpen" :dismissible="false" @update:open="(val) => isDeleteModalOpen = val">
      <template #title>
        {{ $t('pages.products.details.deleteModal.title') }}
      </template>

      <template #description>
        {{ $t('pages.products.details.deleteModal.description') }}
      </template>

      <template #body>
        <div class="space-y-3">
          <div v-if="productName">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('pages.products.details.deleteModal.productName') }}:</span>
            <span class="text-sm ml-2">{{ productName }}</span>
          </div>
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('pages.products.details.deleteModal.currentQuantity') }}:</span>
            <span class="text-sm ml-2">{{ item.currentQuantity }} {{ $t(`enums.unit.${item.unit}`) }}</span>
          </div>
          <div v-if="item.expirationAt">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('pages.products.details.deleteModal.expirationDate') }}:</span>
            <span class="text-sm ml-2">{{ formatDate(item.expirationAt) }}</span>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton :label="$t('pages.products.details.deleteModal.cancel')" color="neutral" variant="outline" @click="closeDeleteModal" />
          <UButton :label="$t('pages.products.details.deleteModal.confirm')" color="error" :loading="isDeleting" @click="handleDelete" />
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { InventoryItemInfo, ConsumeInventoryItemRequest, UpdateInventoryItemRequest } from '../types/product'
import { Currency } from '../types/enums'
import type { CalendarDate } from '@internationalized/date'
import { CalendarDate as CalendarDateClass } from '@internationalized/date'

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
const { consumeInventoryItem, updateInventoryItem, deleteInventoryItem } = useProductsApi()
const { isExpired: checkIsExpired, isExpiringSoon: checkIsExpiringSoon } = useExpirationCheck()
const toast = useToast()

// State
const isConsumeModalOpen = ref(false)
const consumeQuantity = ref<number | null>(null)
const isConsuming = ref(false)

const isEditModalOpen = ref(false)
const expirationDateInput = ref()
const editForm = ref<{
  quantity: number | null
  expirationAt: CalendarDate | null
  price: number | null
  currency: Currency | null
}>({
  quantity: null,
  expirationAt: null,
  price: null,
  currency: null
})
const isUpdating = ref(false)

const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

// Swipe gestures (left = delete, right = edit); tap = consume.
const rowEl = ref<HTMLElement | null>(null)

const anyModalOpen = computed(() =>
  isConsumeModalOpen.value || isEditModalOpen.value || isDeleteModalOpen.value
)
const anyPending = computed(() =>
  isConsuming.value || isUpdating.value || isDeleting.value
)

const swipe = useSwipeActions(rowEl, {
  onSwipeLeft: () => openDeleteModal(),
  onSwipeRight: () => openEditModal(),
  disabled: () => anyModalOpen.value || anyPending.value
})

// Select options
const currencyOptions = computed(() => [
  { label: $t('enums.currency.135'), value: Currency.Huf },
  { label: $t('enums.currency.105'), value: Currency.Eur },
  { label: $t('enums.currency.279'), value: Currency.Usd }
])

// Computed status / styling
const isExpired = computed(() => checkIsExpired(props.item.expirationAt))
const isExpiringSoon = computed(() => checkIsExpiringSoon(props.item.expirationAt))

const backgroundClass = computed(() => {
  if (isExpired.value)
    return 'bg-gradient-to-br from-red-50 to-red-100/50 dark:from-red-900/30 dark:to-red-800/20'
  if (isExpiringSoon.value)
    return 'bg-gradient-to-br from-amber-50 to-amber-100/50 dark:from-amber-900/30 dark:to-amber-800/20'
  return 'bg-default'
})

const borderClass = computed(() => {
  if (isExpired.value) return 'border-red-400 dark:border-red-500'
  if (isExpiringSoon.value) return 'border-primary-400 dark:border-primary-500'
  return 'border-gray-200 dark:border-gray-700'
})

const quantityIcon = computed(() => {
  if (isExpired.value) return 'i-lucide-alert-circle'
  if (isExpiringSoon.value) return 'i-lucide-clock'
  return 'i-lucide-package-2'
})

const quantityIconColor = computed(() => {
  if (isExpired.value) return 'text-red-600 dark:text-red-400'
  if (isExpiringSoon.value) return 'text-amber-600 dark:text-amber-400'
  return 'text-primary-600 dark:text-primary-400'
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

// Methods
const formatUnitPrice = (price: number, quantity: number): string => {
  const unitPrice = price / quantity
  if (Number.isInteger(unitPrice)) return unitPrice.toString()
  return unitPrice.toFixed(2)
}

// Tap the row → consume. Ignore the synthetic click after a swipe and clicks on interactive children.
const handleRowClick = (event: MouseEvent) => {
  if (swipe.suppressClick.value) return
  const target = event.target as HTMLElement
  if (target.closest('a, button')) return
  openConsumeModal()
}

const openConsumeModal = () => {
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
  if (newValue < 0) {
    consumeQuantity.value = 0
  } else if (newValue > props.item.currentQuantity) {
    consumeQuantity.value = props.item.currentQuantity
  } else {
    consumeQuantity.value = Math.round(newValue * 10) / 10
  }
}

const handleConsume = async () => {
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
    const request: ConsumeInventoryItemRequest = { quantity: consumeQuantity.value }
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

const openEditModal = () => {
  let expirationDate: CalendarDate | null = null
  if (props.item.expirationAt) {
    const date = new Date(props.item.expirationAt)
    expirationDate = new CalendarDateClass(date.getFullYear(), date.getMonth() + 1, date.getDate())
  }

  editForm.value = {
    quantity: props.item.currentQuantity,
    expirationAt: expirationDate,
    price: props.item.purchaseInfo?.price || null,
    currency: props.item.purchaseInfo?.currency || null
  }
  isEditModalOpen.value = true
}

const closeEditModal = () => {
  isEditModalOpen.value = false
  editForm.value = { quantity: null, expirationAt: null, price: null, currency: null }
}

const handleUpdate = async () => {
  isUpdating.value = true
  try {
    let expirationAtString: string | undefined = undefined
    if (editForm.value.expirationAt) {
      const date = editForm.value.expirationAt
      const localDate = new Date(date.year, date.month - 1, date.day, 12, 0, 0)
      expirationAtString = localDate.toISOString()
    }

    const request: UpdateInventoryItemRequest = {
      quantity: editForm.value.quantity || undefined,
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

.slideInUp {
  animation: slideInUp 0.4s ease-out;
}
</style>
