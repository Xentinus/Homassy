<template>
  <section class="rounded-2xl border-2 border-gray-200 dark:border-gray-700 bg-default shadow-sm overflow-hidden">
    <!-- Navbar-style header: thumbnail + name/brand + favorite + actions menu -->
    <div class="flex items-center gap-3 border-b border-gray-200 dark:border-gray-800 p-4">
      <!-- Thumbnail / placeholder -->
      <button
        v-if="product.productPictureBase64"
        type="button"
        class="w-14 h-14 rounded-xl overflow-hidden border border-gray-200 dark:border-gray-700 bg-white/80 dark:bg-gray-900/40 shrink-0 cursor-pointer hover:opacity-90 transition-opacity"
        :aria-label="product.name"
        @click="emit('image-click')"
      >
        <img
          :src="`data:image/jpeg;base64,${product.productPictureBase64}`"
          :alt="product.name"
          class="w-full h-full object-contain"
        >
      </button>
      <div
        v-else
        class="w-14 h-14 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-100 dark:bg-gray-800 flex items-center justify-center shrink-0"
      >
        <UIcon name="i-lucide-package" class="h-7 w-7 text-gray-400 dark:text-gray-500" />
      </div>

      <!-- Name + brand -->
      <div class="min-w-0 flex-1">
        <h1 class="text-lg sm:text-xl font-semibold leading-tight break-words text-highlighted">
          {{ product.name }}
        </h1>
        <p v-if="product.brand" class="text-sm text-muted break-words">{{ product.brand }}</p>
      </div>

      <!-- Favorite toggle -->
      <UButton
        :icon="product.isFavorite ? 'i-lucide-heart' : 'i-lucide-heart-plus'"
        color="neutral"
        variant="ghost"
        :aria-label="$t('pages.products.filters.favorites')"
        class="shrink-0 text-pink-600 dark:text-pink-400"
        @click="emit('toggle-favorite')"
      />
    </div>

    <!-- Body: attribute chips -->
    <div class="flex flex-wrap gap-2 p-4 text-sm">
      <div
        class="flex items-center gap-2 px-2.5 py-1 rounded-md border border-gray-200 dark:border-gray-700 bg-white/70 dark:bg-gray-900/40"
      >
        <UIcon name="i-lucide-scale" class="h-4 w-4 text-gray-500" />
        <span>{{ $t(`enums.unit.${product.unit}`) }}</span>
      </div>
      <div
        v-if="product.isEatable"
        class="flex items-center gap-2 px-2.5 py-1 rounded-md border border-gray-200 dark:border-gray-700 bg-white/70 dark:bg-gray-900/40"
      >
        <UIcon name="i-lucide-utensils" class="h-4 w-4 text-amber-600 dark:text-amber-400" />
        <span>{{ $t('pages.products.details.editProductModal.isEatableLabel') }}</span>
      </div>
      <div
        v-if="product.category"
        class="flex items-center gap-2 px-2.5 py-1 rounded-md border border-gray-200 dark:border-gray-700 bg-white/70 dark:bg-gray-900/40"
      >
        <UIcon name="i-lucide-tag" class="h-4 w-4 text-gray-500" />
        <span>{{ formatProductCategory(product.category) }}</span>
      </div>
      <div
        v-if="product.barcode"
        class="flex items-center gap-2 px-2.5 py-1 rounded-md border border-gray-200 dark:border-gray-700 bg-white/70 dark:bg-gray-900/40"
      >
        <UIcon name="i-lucide-barcode" class="h-4 w-4 text-gray-500" />
        <span class="font-mono">{{ product.barcode }}</span>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { DetailedProductInfo } from '~/types/product'

interface Props {
  product: DetailedProductInfo
}

defineProps<Props>()

const emit = defineEmits<{
  'image-click': []
  'toggle-favorite': []
}>()

const { t: $t } = useI18n()
const { formatProductCategory } = useEnumLabel()
</script>
