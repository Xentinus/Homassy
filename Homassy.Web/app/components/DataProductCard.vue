<template>
  <div class="relative rounded-2xl overflow-hidden card-animate" style="touch-action: pan-y" data-no-pull-refresh>
    <!-- Swipe action layer -->
    <div
      v-show="swipe.isSwiping.value"
      aria-hidden="true"
      class="absolute inset-0 rounded-2xl flex items-center justify-between px-4"
      :class="swipe.direction.value === 'left' ? 'bg-error-500 dark:bg-error-600' : 'bg-primary-500 dark:bg-primary-600'"
    >
      <UIcon name="i-lucide-pencil" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'right' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
      <UIcon name="i-lucide-trash-2" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'left' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
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

    <!-- Delete confirmation -->
    <AppDrawer :open="isDeleteModalOpen" :title="$t('pages.products.details.deleteProduct')" icon="i-lucide-trash-2" fit="content" @update:open="(v) => { isDeleteModalOpen = v }">
      <p class="text-sm text-muted">{{ $t('pages.products.details.deleteProductModal.warning') }}</p>
      <div class="space-y-2">
        <div>
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('common.name') }}:</span>
          <span class="text-sm ml-2">{{ product.name }}</span>
        </div>
        <div v-if="product.brand">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('pages.addProduct.form.brand') }}:</span>
          <span class="text-sm ml-2">{{ product.brand }}</span>
        </div>
      </div>
      <template #footer>
        <UButton :label="$t('common.cancel')" color="neutral" variant="outline" @click="() => { isDeleteModalOpen = false }" />
        <UButton :label="$t('common.delete')" color="error" :loading="isDeleting" @click="handleDelete" />
      </template>
    </AppDrawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ProductInfo } from '~/types/product'
import { useProductsApi } from '~/composables/api/useProductsApi'
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
  deleted: [publicId: string]
}>()

const { t } = useI18n()
const toast = useToast()
const { deleteProduct } = useProductsApi()
const { formatProductCategory } = useEnumLabel()
const { highlightText } = useSearchHighlight()

const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

const cardEl = ref<HTMLElement | null>(null)
const swipe = useSwipeActions(cardEl, {
  onSwipeLeft: () => { isDeleteModalOpen.value = true },
  onSwipeRight: () => emit('edit', props.product),
  disabled: () => isDeleteModalOpen.value || isDeleting.value
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

async function handleDelete() {
  isDeleting.value = true
  try {
    await deleteProduct(props.product.publicId)
    isDeleteModalOpen.value = false
    emit('deleted', props.product.publicId)
  } catch (error) {
    console.error('Failed to delete product:', error)
    toast.add({ title: t('common.error'), description: t('pages.products.details.deleteProductModal.deleteFailed'), color: 'error' })
  } finally {
    isDeleting.value = false
  }
}
</script>

<style scoped>
@keyframes slideInUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}
.card-animate {
  animation: slideInUp 0.4s ease-out;
}
</style>
