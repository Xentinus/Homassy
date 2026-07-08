<template>
  <div
    role="button"
    tabindex="0"
    class="group relative bg-default rounded-2xl border-2 p-3 cursor-pointer shadow-sm hover:shadow-lg transition-shadow duration-200 flex flex-col overflow-hidden card-animate"
    :class="cardBorderClass"
    @click="selectProduct"
    @keydown.enter="selectProduct"
    @keydown.space.prevent="selectProduct"
  >
    <!-- Header: Name, Brand, Barcode, Icons -->
    <div class="space-y-1 mb-2">
      <!-- Product Name -->
      <h3
        class="text-sm font-bold break-words text-highlighted"
        v-html="highlightText(product.name, searchQuery)"
      />

      <!-- Brand -->
      <div class="text-xs text-muted break-words font-medium">
        <span v-html="highlightText(product.brand || '-', searchQuery)" />
      </div>

      <!-- Barcode -->
      <div v-if="product.barcode" class="flex items-center gap-1 text-xs text-gray-500 dark:text-gray-400">
        <UIcon name="i-lucide-barcode" class="h-3 w-3 text-gray-400 dark:text-gray-500 flex-shrink-0" />
        <span class="font-mono break-all" v-html="highlightText(product.barcode, searchQuery)" />
      </div>

      <!-- Icons -->
      <div class="flex items-center gap-1.5 pt-1">
        <UIcon
          v-if="product.isEatable"
          name="i-lucide-utensils"
          class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0"
        />
        <UIcon
          v-if="product.isFavorite"
          name="i-lucide-heart"
          class="h-3.5 w-3.5 text-pink-600 dark:text-pink-400 flex-shrink-0"
        />
      </div>
    </div>

    <!-- Stock Count by Unit -->
    <div class="mt-auto">
      <p v-if="product.inventoryItems.length === 0" class="text-xs text-muted italic text-center py-1">
        {{ $t('common.noData') }}
      </p>
      <div v-else class="flex flex-col gap-1">
        <div v-for="entry in stockByUnit" :key="entry.unit" class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-package-2" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0" />
          <span class="font-bold text-highlighted">{{ entry.quantity }}</span>
          <span class="text-toned">{{ entry.unitLabel }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { InventoryGridProductInfo } from '../types/product'

interface Props {
  product: InventoryGridProductInfo
  searchQuery?: string
}

const props = withDefaults(defineProps<Props>(), {
  searchQuery: ''
})

const emit = defineEmits<{
  select: [publicId: string]
}>()

const { t } = useI18n()
const { isExpired: checkIsExpired, isExpiringSoon: checkIsExpiringSoon } = useExpirationCheck()

// Aggregate inventory quantities by unit
const stockByUnit = computed(() => {
  const map = new Map<number, number>()
  for (const item of props.product.inventoryItems) {
    const unit = item.unit as number
    map.set(unit, (map.get(unit) ?? 0) + item.currentQuantity)
  }
  return Array.from(map.entries()).map(([unit, quantity]) => ({
    unit,
    quantity: Number.isInteger(quantity) ? quantity : parseFloat(quantity.toFixed(3)),
    unitLabel: t(`enums.unit.${unit}`)
  }))
})

// Dynamic border classes based on status
const cardBorderClass = computed(() => {
  if (hasExpiredItems.value) {
    return 'border-red-400 dark:border-red-500'
  }
  if (hasExpiringSoonItems.value) {
    return 'border-primary-400 dark:border-primary-500'
  }
  return 'border-gray-200 dark:border-gray-700'
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

const hasExpiredItems = computed(() => {
  return props.product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      return checkIsExpired(item.expirationAt)
    } catch {
      return false
    }
  })
})

const hasExpiringSoonItems = computed(() => {
  return props.product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      return checkIsExpiringSoon(item.expirationAt)
    } catch {
      return false
    }
  })
})

const selectProduct = () => {
  emit('select', props.product.publicId)
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
