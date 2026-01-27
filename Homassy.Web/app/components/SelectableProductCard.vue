<template>
  <div
    class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 p-3 cursor-pointer hover:shadow-lg hover:-translate-y-0.5 transition-all duration-200 flex flex-col overflow-hidden card-animate"
    :class="[cardBorderClass, isSelected && 'ring-2 ring-offset-2 ring-primary-500 dark:ring-offset-gray-900']"
    @click="handleCardClick"
  >
    <!-- Selection Indicator (Top Right) -->
    <div class="absolute top-2 right-2 z-10">
      <div 
        class="w-5 h-5 rounded-full border-2 border-primary-500 flex items-center justify-center bg-white dark:bg-gray-800 transition-all duration-200"
        :class="isSelected && 'bg-primary-500'"
      >
        <UIcon v-if="isSelected" name="i-lucide-check" class="w-3 h-3 text-white" />
      </div>
    </div>

    <!-- Product Image -->
    <div class="aspect-square bg-gray-100 dark:bg-gray-800 rounded-md mb-2 overflow-hidden flex items-center justify-center relative">
      <img
        v-if="product.productPictureBase64"
        :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
        :alt="product.name"
        class="w-full h-full object-contain transition-opacity"
      />
      <UIcon v-else name="i-lucide-package" class="h-12 w-12 text-gray-400" />
    </div>
    
    <!-- Product Info -->
    <div class="flex-1 flex flex-col">
      <div class="flex items-start gap-2 mb-1">
        <h3 
          class="text-sm font-bold break-words flex-1 text-gray-900 dark:text-white"
          v-html="highlightText(product.name, searchQuery)"
        />
        <div class="flex gap-1 flex-shrink-0">
          <UIcon
            v-if="product.isEatable"
            name="i-lucide-utensils"
            class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0"
            :title="$t('common.eatable')"
          />
          <UIcon
            v-if="product.isFavorite"
            name="i-lucide-heart"
            class="h-3.5 w-3.5 text-pink-600 dark:text-pink-400 flex-shrink-0"
            :title="$t('common.favorite')"
          />
        </div>
      </div>
      
      <p 
        v-if="product.brand" 
        class="text-xs text-gray-500 dark:text-gray-400 break-words font-medium line-clamp-1 mb-1"
        v-html="highlightText(product.brand, searchQuery)"
      />
      
      <p v-if="product.category" class="text-xs text-gray-500 dark:text-gray-500 line-clamp-1 mb-1">
        {{ formatProductCategory(product.category) }}
      </p>
      
      <p 
        v-if="product.barcode" 
        class="text-xs text-gray-500 dark:text-gray-500 font-mono line-clamp-1"
        v-html="highlightText(product.barcode, searchQuery)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ProductInfo } from '~/types/product'
import { useEnumLabel } from '~/composables/useEnumLabel'

interface Props {
  product: ProductInfo
  searchQuery?: string
  isSelected?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  searchQuery: '',
  isSelected: false
})

const emit = defineEmits<{
  select: [product: ProductInfo]
}>()

const { formatProductCategory } = useEnumLabel()

// Dynamic border classes based on state
const cardBorderClass = computed(() => {
  if (props.product.isFavorite) {
    return 'border-pink-400 dark:border-pink-500'
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

const handleCardClick = () => {
  emit('select', props.product)
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
