<template>
  <div
    role="button"
    tabindex="0"
    class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 p-3 cursor-pointer hover:shadow-lg hover:-translate-y-0.5 transition-all duration-200 flex flex-col overflow-hidden card-animate"
    :class="cardBorderClass"
    @click="navigateToProduct"
    @keydown.enter="navigateToProduct"
    @keydown.space.prevent="navigateToProduct"
  >
    <!-- Header: Name, Brand, Barcode, Icons -->
    <div class="space-y-1 mb-2">
      <!-- Product Name -->
      <h3 
        class="font-bold text-sm text-gray-900 dark:text-white break-words"
        v-html="highlightText(product.name, searchQuery)"
      />
      
      <!-- Brand -->
      <div class="text-xs text-gray-500 dark:text-gray-400 break-words font-medium">
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

    <!-- Stock Summary Icons - Bottom Right -->
    <div class="mt-auto">
      <p v-if="product.inventoryItems.length === 0" class="text-xs text-gray-400 italic text-center py-1">
        {{ $t('common.noData') }}
      </p>

      <div v-else class="flex items-center gap-1.5" :class="{ 'justify-end': visibleBadgeCount < 3 }">
        <!-- Normal Icon + Count -->
        <div v-if="normalItems.length > 0" class="flex items-center gap-1 px-1.5 py-1 rounded-md border bg-gray-50 dark:bg-gray-700/50 border-gray-200 dark:border-gray-600 shadow-sm">
          <UIcon name="i-lucide-check-circle" class="h-3.5 w-3.5 text-gray-600 dark:text-gray-400 flex-shrink-0" />
          <span class="text-xs font-semibold text-gray-700 dark:text-gray-300">{{ normalItems.length }}</span>
        </div>

        <!-- Expiring Soon Icon + Count -->
        <div v-if="expiringSoonItems.length > 0" class="flex items-center gap-1 px-1.5 py-1 rounded-md border bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-800 shadow-sm">
          <UIcon name="i-lucide-clock" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0" />
          <span class="text-xs font-semibold text-amber-700 dark:text-amber-300">{{ expiringSoonItems.length }}</span>
        </div>

        <!-- Expired Icon + Count -->
        <div v-if="expiredItems.length > 0" class="flex items-center gap-1 px-1.5 py-1 rounded-md border bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800 shadow-sm">
          <UIcon name="i-lucide-alert-circle" class="h-3.5 w-3.5 text-red-600 dark:text-red-400 flex-shrink-0" />
          <span class="text-xs font-semibold text-red-700 dark:text-red-300">{{ expiredItems.length }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { DetailedProductInfo } from '../types/product'
import type { Unit } from '../types/enums'

interface Props {
  product: DetailedProductInfo
  searchQuery?: string
}

const props = withDefaults(defineProps<Props>(), {
  searchQuery: ''
})

const { t } = useI18n()
const { isExpired: checkIsExpired, isExpiringSoon: checkIsExpiringSoon } = useExpirationCheck()

// Dynamic border classes based on status
const cardBorderClass = computed(() => {
  if (hasExpiredItems.value) {
    return 'border-red-400 dark:border-red-500'
  }
  if (hasExpiringSoonItems.value) {
    return 'border-amber-400 dark:border-amber-500'
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

// Helper to check if an item is expiring soon
const isExpiringSoon = (expirationAt: string): boolean => {
  try {
    return checkIsExpiringSoon(expirationAt)
  } catch {
    return false
  }
}

// Group inventory items by category
const expiredItems = computed(() => {
  return props.product.inventoryItems.filter(item =>
    item.expirationAt && isExpired(item.expirationAt)
  )
})

const expiringSoonItems = computed(() => {
  return props.product.inventoryItems.filter(item =>
    item.expirationAt && isExpiringSoon(item.expirationAt) && !isExpired(item.expirationAt)
  )
})

const normalItems = computed(() => {
  return props.product.inventoryItems.filter(item =>
    !item.expirationAt || (!isExpired(item.expirationAt) && !isExpiringSoon(item.expirationAt))
  )
})

// Count visible badges
const visibleBadgeCount = computed(() => {
  let count = 0
  if (expiredItems.value.length > 0) count++
  if (expiringSoonItems.value.length > 0) count++
  if (normalItems.value.length > 0) count++
  return count
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
    return checkIsExpired(expirationAt)
  } catch {
    return false
  }
}

const router = useRouter()

const navigateToProduct = () => {
  router.push(`/products/${props.product.publicId}`)
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
