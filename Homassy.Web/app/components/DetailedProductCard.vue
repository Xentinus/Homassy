<template>
  <div
    role="button"
    tabindex="0"
    class="cursor-pointer transition-colors relative flex flex-col"
    :class="[
      'p-2 md:p-4 rounded-lg border-2 bg-white dark:bg-gray-800 hover:shadow-sm transition-shadow hover:bg-gray-50/50 dark:hover:bg-gray-700/50',
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
    <!-- Header: Name, Brand, Barcode, Icons -->
    <div class="space-y-1 mb-2">
      <!-- Product Name -->
      <h3 
        class="font-semibold text-sm text-gray-900 dark:text-white break-words"
        v-html="highlightText(product.name, searchQuery)"
      />
      
      <!-- Brand -->
      <div class="text-xs text-gray-500 dark:text-gray-400 break-words">
        <span v-html="highlightText(product.brand || '-', searchQuery)" />
      </div>

      <!-- Barcode -->
      <div v-if="product.barcode" class="flex items-center gap-1 text-xs text-gray-500 dark:text-gray-400">
        <UIcon name="i-lucide-barcode" class="h-3 w-3 flex-shrink-0" />
        <span class="font-mono break-all" v-html="highlightText(product.barcode, searchQuery)" />
      </div>

      <!-- Icons -->
      <div class="flex items-center gap-1.5 pt-1">
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
      </div>
    </div>

    <!-- Stock Summary Icons - Bottom Right -->
    <div class="mt-auto">
      <p v-if="product.inventoryItems.length === 0" class="text-xs text-gray-400 italic text-center py-1">
        {{ $t('common.noData') }}
      </p>

      <div v-else class="flex items-center gap-1.5" :class="{ 'justify-end': visibleBadgeCount < 3 }">
        <!-- Normal Icon + Count -->
        <div v-if="normalItems.length > 0" class="flex items-center gap-1 px-1.5 py-1 rounded-md border bg-gray-50 dark:bg-gray-700/50 border-gray-200 dark:border-gray-600">
          <UIcon name="i-lucide-check-circle" class="h-3.5 w-3.5 text-gray-500 dark:text-gray-400 flex-shrink-0" />
          <span class="text-xs font-semibold text-gray-700 dark:text-gray-300">{{ normalItems.length }}</span>
        </div>

        <!-- Expiring Soon Icon + Count -->
        <div v-if="expiringSoonItems.length > 0" class="flex items-center gap-1 px-1.5 py-1 rounded-md border bg-primary-50 dark:bg-primary-900/20 border-primary-200 dark:border-primary-800">
          <UIcon name="i-lucide-clock" class="h-3.5 w-3.5 text-primary-500 flex-shrink-0" />
          <span class="text-xs font-semibold text-primary-700 dark:text-primary-300">{{ expiringSoonItems.length }}</span>
        </div>

        <!-- Expired Icon + Count -->
        <div v-if="expiredItems.length > 0" class="flex items-center gap-1 px-1.5 py-1 rounded-md border bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800">
          <UIcon name="i-lucide-alert-circle" class="h-3.5 w-3.5 text-red-500 dark:text-red-400 flex-shrink-0" />
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
  const today = new Date()
  today.setHours(0, 0, 0, 0) // Reset to start of day
  
  return props.product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      const expirationDate = new Date(item.expirationAt)
      expirationDate.setHours(0, 0, 0, 0) // Reset to start of day
      return expirationDate < today
    } catch {
      return false
    }
  })
})

const hasExpiringSoonItems = computed(() => {
  const today = new Date()
  today.setHours(0, 0, 0, 0) // Reset to start of day
  
  const twoWeeksFromNow = new Date(today)
  twoWeeksFromNow.setDate(today.getDate() + 14)

  return props.product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      const expirationDate = new Date(item.expirationAt)
      expirationDate.setHours(0, 0, 0, 0) // Reset to start of day
      // Within next 2 weeks but NOT yet expired
      return expirationDate >= today && expirationDate <= twoWeeksFromNow
    } catch {
      return false
    }
  })
})

// Helper to check if an item is expiring soon
const isExpiringSoon = (expirationAt: string): boolean => {
  try {
    const today = new Date()
    today.setHours(0, 0, 0, 0) // Reset to start of day
    
    const twoWeeksFromNow = new Date(today)
    twoWeeksFromNow.setDate(today.getDate() + 14)
    
    const expirationDate = new Date(expirationAt)
    expirationDate.setHours(0, 0, 0, 0) // Reset to start of day
    
    return expirationDate >= today && expirationDate <= twoWeeksFromNow
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
    const today = new Date()
    today.setHours(0, 0, 0, 0) // Reset to start of day
    
    const expirationDate = new Date(expirationAt)
    expirationDate.setHours(0, 0, 0, 0) // Reset to start of day
    
    return expirationDate < today
  } catch {
    return false
  }
}

const router = useRouter()

const navigateToProduct = () => {
  router.push(`/products/${props.product.publicId}`)
}
</script>
