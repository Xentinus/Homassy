<template>
  <div class="p-4 rounded-lg border-2 border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 space-y-3">
    <!-- Product Header -->
    <div>
      <h3 class="font-semibold text-sm text-gray-900 dark:text-white">
        {{ product.name }}
      </h3>
      <p class="text-xs text-gray-600 dark:text-gray-400">
        {{ product.brand || '-' }}
      </p>
    </div>

    <!-- Inventory Items -->
    <div class="space-y-2 pt-2 border-t border-gray-200 dark:border-gray-700">
      <p v-if="product.inventoryItems.length === 0" class="text-xs text-gray-500 dark:text-gray-500 italic">
        {{ $t('common.noData') }}
      </p>

      <div
        v-for="item in product.inventoryItems"
        :key="item.publicId"
        class="text-xs space-y-1 p-2 rounded bg-gray-50 dark:bg-gray-700/50"
      >
        <!-- Current Quantity & Unit -->
        <div class="flex items-center justify-between">
          <span class="text-gray-600 dark:text-gray-400">{{ $t('common.quantity') }}:</span>
          <span class="font-medium text-gray-900 dark:text-white">
            {{ item.currentQuantity }} {{ getUnitLabel(item.unit) }}
          </span>
        </div>

        <!-- Expiration Date -->
        <div v-if="item.expirationAt" class="flex items-center justify-between">
          <span class="text-gray-600 dark:text-gray-400">{{ $t('common.expirationDate') }}:</span>
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

        <!-- Expired Badge -->
        <div v-if="item.expirationAt && isExpired(item.expirationAt)" class="flex items-center gap-1">
          <UIcon name="i-lucide-alert-circle" class="h-3 w-3 text-red-600 dark:text-red-400" />
          <span class="text-red-600 dark:text-red-400">{{ $t('common.expired') }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { DetailedProductInfo } from '../types/product'
import type { Unit } from '../types/enums'

interface Props {
  product: DetailedProductInfo
}

defineProps<Props>()

const { t } = useI18n()

const getUnitLabel = (unit: Unit): string => {
  const unitKey = `enums.unit.${unit}`
  return t(unitKey, unit)
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
</script>
