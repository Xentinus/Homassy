<template>
  <section class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 border-gray-200 dark:border-gray-700 p-5 shadow-sm">
    <div class="flex flex-col md:flex-row gap-6 items-center md:items-start">
      <!-- Image -->
      <div class="flex-shrink-0">
        <button
          v-if="product.productPictureBase64"
          type="button"
          class="w-24 h-24 md:w-32 md:h-32 rounded-xl overflow-hidden border border-gray-200 dark:border-gray-700 bg-white/80 dark:bg-gray-900/40 cursor-pointer hover:opacity-90 transition-opacity"
          :aria-label="product.name"
          @click="emit('image-click')"
          @keydown.enter.prevent="emit('image-click')"
          @keydown.space.prevent="emit('image-click')"
        >
          <img
            :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
            :alt="product.name"
            class="w-full h-full object-contain"
          >
        </button>

        <div
          v-else
          class="w-24 h-24 md:w-32 md:h-32 bg-gray-100 dark:bg-gray-700 rounded-xl border border-gray-200 dark:border-gray-700 flex flex-col items-center justify-center"
        >
          <UIcon name="i-lucide-package" class="h-12 w-12 text-gray-400 dark:text-gray-500" />
          <span class="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {{ t('pages.products.details.noImage') }}
          </span>
        </div>
      </div>

      <!-- Details -->
      <div class="flex-1 space-y-3 text-center md:text-left">
        <div>
          <h2 class="text-2xl font-bold break-words text-gray-900 dark:text-white">{{ product.name }}</h2>
          <p v-if="product.brand" class="text-lg text-gray-600 dark:text-gray-400 break-words">{{ product.brand }}</p>

          <!-- Status Icons -->
          <div class="flex items-center gap-2 mt-2 justify-center md:justify-start">
            <UIcon
              v-if="product.isEatable"
              name="i-lucide-utensils"
              class="h-5 w-5 text-amber-600 dark:text-amber-400"
            />
            <button type="button" class="inline-flex" @click="emit('toggle-favorite')">
              <UIcon
                :name="product.isFavorite ? 'i-lucide-heart' : 'i-lucide-heart-plus'"
                class="h-5 w-5 text-pink-600 dark:text-pink-400 hover:opacity-80 transition-opacity"
              />
            </button>
          </div>
        </div>

        <div class="flex flex-wrap gap-3 text-sm justify-center md:justify-start">
          <div v-if="product.category" class="flex items-center gap-2 px-2 py-1 rounded-md border bg-white/70 dark:bg-gray-900/40 border-gray-200 dark:border-gray-700">
            <UIcon name="i-lucide-tag" class="text-gray-500" />
            <span>{{ formatProductCategory(product.category) }}</span>
          </div>
          <div v-if="product.barcode" class="hidden md:flex items-center gap-2 px-2 py-1 rounded-md border bg-white/70 dark:bg-gray-900/40 border-gray-200 dark:border-gray-700">
            <UIcon name="i-lucide-barcode" class="text-gray-500" />
            <span class="font-mono">{{ product.barcode }}</span>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { DetailedProductInfo } from '~/types/product'
import { useEnumLabel } from '~/composables/useEnumLabel'

interface Props {
  product: DetailedProductInfo
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'image-click': []
  'toggle-favorite': []
}>()

const { t } = useI18n()
const { formatProductCategory } = useEnumLabel()
</script>
