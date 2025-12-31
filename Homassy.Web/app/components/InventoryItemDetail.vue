<template>
  <div class="rounded-lg border p-4 transition-all" :class="borderColorClass">
    <!-- Header: Quantity + Unit -->
    <div class="flex items-center justify-between mb-3">
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-package-2" class="h-5 w-5 text-primary-500" />
        <span class="text-lg font-semibold">
          {{ item.currentQuantity }} {{ $t(`enums.unit.${item.unit}`) }}
        </span>
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
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { InventoryItemInfo } from '../types/product'

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

const { t: $t } = useI18n()
const { formatDate } = useDateFormat()

// State
const isHistoryExpanded = ref(false)

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
</script>
