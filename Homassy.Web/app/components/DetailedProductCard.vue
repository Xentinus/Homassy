<template>
  <div
    role="button"
    tabindex="0"
    class="cursor-pointer transition-colors"
    :class="[
      'p-3 rounded-lg border-2 bg-white dark:bg-gray-800 hover:shadow-sm transition-shadow hover:bg-gray-50 dark:hover:bg-gray-700',
      hasExpiredItems
        ? 'border-red-500 dark:border-red-400'
        : hasExpiringSoonItems
          ? 'border-primary-500 dark:border-primary-400'
          : 'border-neutral-300 dark:border-neutral-700'
    ]"
    @click="navigateToProduct"
    @keydown.enter="navigateToProduct"
    @keydown.space.prevent="navigateToProduct"
  >
    <!-- Header: Name, Brand, Barcode -->
    <div class="space-y-1 mb-2">
      <div class="flex items-start justify-between gap-2">
        <h3 class="font-semibold text-sm text-gray-900 dark:text-white line-clamp-1">
          {{ product.name }}
        </h3>
        <div class="flex items-center gap-1.5 flex-shrink-0 mt-0.5">
          <UIcon
            v-if="product.isEatable"
            name="i-lucide-utensils"
            class="h-4 w-4 text-primary-500"
          />
          <UIcon
            v-if="product.isFavorite"
            name="i-lucide-heart"
            class="h-4 w-4 text-primary-500"
          />
          <UIcon
            v-if="hasExpiredItems"
            name="i-lucide-alert-triangle"
            class="h-4 w-4 text-red-500 dark:text-red-400"
          />
          <UIcon
            v-if="!hasExpiredItems && hasExpiringSoonItems"
            name="i-lucide-alert-triangle"
            class="h-4 w-4 text-primary-500"
          />
        </div>
      </div>
      
      <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
        <span>{{ product.brand || '-' }}</span>
        <span v-if="product.barcode" class="flex items-center gap-1">
          <span>•</span>
          <UIcon name="i-lucide-barcode" class="h-3 w-3" />
          <span class="font-mono">{{ product.barcode }}</span>
        </span>
      </div>
    </div>

    <!-- Inventory Items -->
    <div class="space-y-1.5">
      <p v-if="product.inventoryItems.length === 0" class="text-xs text-gray-400 italic text-center py-1">
        {{ $t('common.noData') }}
      </p>

      <div
        v-for="item in product.inventoryItems"
        :key="item.publicId"
        :class="[
          'text-xs p-2 rounded space-y-1',
          item.expirationAt && isExpired(item.expirationAt)
            ? 'bg-red-50 dark:bg-red-900/20'
            : 'bg-gray-50 dark:bg-gray-700/50'
        ]"
      >
        <!-- Quantity -->
        <div class="flex items-center justify-between">
          <span class="text-gray-600 dark:text-gray-400">{{ $t('common.quantity') }}</span>
          <span class="font-semibold text-gray-900 dark:text-white">
            {{ item.currentQuantity }} {{ getUnitLabel(item.unit) }}
          </span>
        </div>

        <!-- Expiration -->
        <div v-if="item.expirationAt" class="flex items-center justify-between">
          <span class="text-gray-600 dark:text-gray-400">{{ $t('common.expirationDate') }}</span>
          <div class="flex items-center gap-1">
            <UIcon 
              v-if="isExpired(item.expirationAt)"
              name="i-lucide-alert-circle" 
              class="h-3 w-3 text-red-600 dark:text-red-400" 
            />
            <span
              :class="[
                'font-medium',
                isExpired(item.expirationAt)
                  ? 'text-red-600 dark:text-red-400'
                  : 'text-gray-900 dark:text-white'
              ]"
            >
              {{ formatDate(item.expirationAt) }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { DetailedProductInfo } from '../types/product'
import type { Unit } from '../types/enums'

interface Props {
  product: DetailedProductInfo
}

const props = defineProps<Props>()

const { t } = useI18n()

const hasExpiredItems = computed(() => {
  return props.product.inventoryItems.some(item =>
    item.expirationAt && isExpired(item.expirationAt)
  )
})

const hasExpiringSoonItems = computed(() => {
  const now = new Date()
  const twoWeeksFromNow = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000)

  return props.product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      const expirationDate = new Date(item.expirationAt)
      // Within next 2 weeks but NOT yet expired
      return expirationDate >= now && expirationDate <= twoWeeksFromNow
    } catch {
      return false
    }
  })
})

const getUnitLabel = (unit: Unit): string => {
  const unitKey = `enums.unit.${unit}`
  return t(unitKey)
}

const formatDate = (dateString: string): string => {
  try {
    const date = new Date(dateString)
    return new Intl.DateTimeFormat(navigator.language, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }).format(date)
  } catch {
    return dateString
  }
}

const isExpired = (expirationAt: string): boolean => {
  try {
    const expirationDate = new Date(expirationAt)
    return expirationDate < new Date()
  } catch {
    return false
  }
}

const navigateToProduct = () => {
  navigateTo(`/products/${props.product.publicId}`)
}
</script>
