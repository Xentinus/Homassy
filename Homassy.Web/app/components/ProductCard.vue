<template>
  <div
    class="p-4 rounded-lg border-2 transition-all duration-200 cursor-pointer hover:shadow-md"
    :class="[
      isActive 
        ? 'border-primary-500 bg-primary-50 dark:bg-primary-900/10' 
        : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'
    ]"
    @click="emit('click')"
  >
    <!-- Product Info -->
    <div class="space-y-2">
      <!-- Name -->
      <h3 class="font-semibold text-sm line-clamp-2 text-gray-900 dark:text-white">
        {{ product.name }}
      </h3>

      <!-- Brand -->
      <p class="text-xs text-gray-600 dark:text-gray-400">
        {{ product.brand || '-' }}
      </p>

      <!-- Barcode -->
      <div class="flex items-center gap-1 text-xs text-gray-500 dark:text-gray-500">
        <UIcon name="i-lucide-barcode" class="h-3 w-3" />
        <span class="font-mono">{{ product.barcode || '-' }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ProductInfo } from '../types/product'

interface Props {
  product: ProductInfo
  isActive?: boolean
}

withDefaults(defineProps<Props>(), {
  isActive: false
})

const emit = defineEmits<{
  click: []
}>()
</script>
