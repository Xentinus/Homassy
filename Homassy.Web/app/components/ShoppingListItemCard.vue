<template>
  <div
    class="relative bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-4 space-y-3"
    :class="{ 'border-green-500 dark:border-green-600': item.purchasedAt }"
  >
    <div class="flex gap-4">
      <!-- Product Image -->
      <div v-if="item.product?.productPictureBase64" class="flex-shrink-0">
        <img
          :src="`data:image/jpeg;base64,${item.product.productPictureBase64}`"
          :alt="displayName"
          class="w-20 h-20 md:w-24 md:h-24 object-contain rounded-lg cursor-pointer hover:opacity-90 transition-opacity"
          @click="isImageOverlayOpen = true"
        >
      </div>
      <div v-else class="flex-shrink-0 w-20 h-20 md:w-24 md:h-24 bg-gray-100 dark:bg-gray-700 rounded-lg flex items-center justify-center">
        <UIcon name="i-lucide-package" class="h-10 w-10 text-gray-400 dark:text-gray-500" />
      </div>

      <!-- Item Details -->
      <div class="flex-1 space-y-2">
        <!-- Product Name -->
        <div>
          <h3 class="text-lg font-semibold">{{ displayName }}</h3>
          <p v-if="item.product?.brand" class="text-sm text-gray-600 dark:text-gray-400">
            {{ item.product.brand }}
          </p>
        </div>

        <!-- Shopping Location with Google Maps -->
        <div v-if="item.shoppingLocation" class="flex items-center gap-2 text-sm">
          <UIcon name="i-lucide-map-pin" class="h-4 w-4 text-gray-500" />
          <span class="text-gray-700 dark:text-gray-300">{{ item.shoppingLocation.name }}</span>
          <a
            v-if="item.shoppingLocation.googleMaps"
            :href="item.shoppingLocation.googleMaps"
            target="_blank"
            rel="noopener noreferrer"
            class="text-primary-500 hover:text-primary-600 dark:hover:text-primary-400 transition-colors"
          >
            <UIcon name="i-lucide-external-link" class="h-4 w-4" />
          </a>
        </div>

        <!-- Quantity and Unit -->
        <div class="flex items-center gap-2 text-sm">
          <UIcon name="i-lucide-package-2" class="h-4 w-4 text-gray-500" />
          <span class="font-medium text-gray-900 dark:text-gray-100">
            {{ item.quantity }} {{ item.unit }}
          </span>
        </div>

        <!-- Note (if not empty) -->
        <div v-if="item.note" class="flex items-start gap-2 text-sm">
          <UIcon name="i-lucide-sticky-note" class="h-4 w-4 text-gray-500 mt-0.5" />
          <p class="text-gray-600 dark:text-gray-400 italic">{{ item.note }}</p>
        </div>

        <!-- Dates -->
        <div class="flex flex-wrap gap-3 text-xs">
          <div v-if="item.dueAt" class="flex items-center gap-1 text-gray-600 dark:text-gray-400">
            <UIcon name="i-lucide-calendar-clock" class="h-3 w-3" />
            <span>Due: {{ formatDate(item.dueAt) }}</span>
          </div>
          <div v-if="item.deadlineAt" class="flex items-center gap-1 text-gray-600 dark:text-gray-400">
            <UIcon name="i-lucide-calendar-x" class="h-3 w-3" />
            <span>Deadline: {{ formatDate(item.deadlineAt) }}</span>
          </div>
        </div>

        <!-- Purchased Badge -->
        <div v-if="item.purchasedAt" class="inline-flex items-center gap-1 px-2 py-1 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 rounded text-xs font-medium">
          <UIcon name="i-lucide-check-circle" class="h-3 w-3" />
          <span>Purchased</span>
        </div>
      </div>
    </div>

    <!-- Image Overlay -->
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-200 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="isImageOverlayOpen && item.product?.productPictureBase64"
        class="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4 cursor-pointer"
        @click="isImageOverlayOpen = false"
        @keydown.esc="isImageOverlayOpen = false"
      >
        <img
          :src="`data:image/jpeg;base64,${item.product.productPictureBase64}`"
          :alt="displayName"
          class="max-w-full max-h-full object-contain"
        >
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ShoppingListItemInfo } from '../types/shoppingList'

interface Props {
  item: ShoppingListItemInfo
}

const props = defineProps<Props>()

// State
const isImageOverlayOpen = ref(false)

// Computed
const displayName = computed(() => {
  return props.item.product?.name || props.item.customName || 'Unnamed Item'
})

// Methods
const formatDate = (dateString: string): string => {
  return new Date(dateString).toLocaleDateString()
}
</script>
