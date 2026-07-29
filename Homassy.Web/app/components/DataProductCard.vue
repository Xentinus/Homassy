<template>
  <div class="relative rounded-2xl overflow-hidden" style="touch-action: pan-y" data-no-pull-refresh>
    <!-- Swipe action layer (edit only — products are global and cannot be deleted) -->
    <div
      v-show="swipe.isSwiping.value && swipe.direction.value === 'right'"
      aria-hidden="true"
      class="absolute inset-0 rounded-2xl flex items-center px-4 bg-primary-500 dark:bg-primary-600"
    >
      <UIcon name="i-lucide-pencil" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'right' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
    </div>

    <!-- Card surface (no image — data opens on click, like the inventory card) -->
    <div
      ref="cardEl"
      class="relative h-full bg-default rounded-2xl border-2 p-3 cursor-pointer shadow-sm hover:shadow-lg transition-shadow duration-200 flex flex-col overflow-hidden select-none"
      :class="cardBorderClass"
      :style="swipe.cardStyle.value"
      @click="handleCardClick"
    >
      <!-- Header -->
      <div class="min-w-0 space-y-1">
        <div class="flex items-start gap-2">
          <h3 class="text-sm font-bold break-words text-highlighted flex-1" v-html="highlightText(product.name, searchQuery)" />
          <div class="flex gap-1 flex-shrink-0 pt-0.5">
            <UIcon v-if="product.isEatable" name="i-lucide-utensils" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" :title="$t('common.eatable')" />
            <UIcon v-if="product.isFavorite" name="i-lucide-heart" class="h-3.5 w-3.5 text-pink-600 dark:text-pink-400" :title="$t('common.favorite')" />
          </div>
        </div>
        <p v-if="product.brand" class="text-xs text-muted break-words font-medium line-clamp-1" v-html="highlightText(product.brand, searchQuery)" />
      </div>

      <!-- Attributes (pinned bottom) -->
      <div class="mt-auto pt-4 space-y-2">
        <div v-if="product.category" class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-tag" class="h-3.5 w-3.5 text-primary-500 flex-shrink-0" />
          <span class="text-toned truncate">{{ formatProductCategory(product.category) }}</span>
        </div>
        <div class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-ruler" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400 flex-shrink-0" />
          <span class="text-toned">{{ $t(`enums.unit.${product.unit}`) }}</span>
        </div>
        <div v-if="product.barcode" class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-barcode" class="h-3.5 w-3.5 text-gray-400 dark:text-gray-500 flex-shrink-0" />
          <span class="text-toned font-mono break-all" v-html="highlightText(product.barcode, searchQuery)" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ProductInfo } from '~/types/product'
import { useEnumLabel } from '~/composables/useEnumLabel'

const props = withDefaults(defineProps<{
  product: ProductInfo
  isActive?: boolean
  searchQuery?: string
}>(), {
  isActive: false,
  searchQuery: ''
})

const emit = defineEmits<{
  select: [publicId: string]
  edit: [product: ProductInfo]
}>()

const { formatProductCategory } = useEnumLabel()
const { highlightText } = useSearchHighlight()

const cardEl = ref<HTMLElement | null>(null)
// Only edit (right). Products are global master data — deleting one would remove it for every
// family, so removal happens per family through the inventory items instead.
const swipe = useSwipeActions(cardEl, {
  onSwipeRight: () => emit('edit', props.product)
})

const cardBorderClass = computed(() => {
  if (props.isActive) return 'border-primary-400 dark:border-primary-500'
  if (props.product.isFavorite) return 'border-pink-400 dark:border-pink-500'
  return 'border-gray-200 dark:border-gray-700'
})

function handleCardClick(event: MouseEvent) {
  if (swipe.suppressClick.value) return
  const target = event.target as HTMLElement
  if (target.closest('a, button')) return
  emit('select', props.product.publicId)
}
</script>
