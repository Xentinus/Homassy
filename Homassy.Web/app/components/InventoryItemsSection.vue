<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-package-2" class="h-5 w-5 text-primary-600 dark:text-primary-400" />
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          {{ $t('pages.products.details.inventoryHeader') }}
        </h3>
      </div>
      <UBadge color="primary" variant="soft" size="sm">
        {{ items.length }}
      </UBadge>
    </div>

    <div v-if="items.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
      {{ $t('pages.products.details.noInventoryItems') }}
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
      <InventoryItemDetail
        v-for="item in items"
        :key="item.publicId"
        :item="item"
        :product-name="productName"
        @consumed="emit('refresh')"
        @updated="emit('refresh')"
        @deleted="emit('refresh')"
      />
    </div>
  </section>
</template>

<script setup lang="ts">
import type { InventoryItemInfo } from '~/types/product'

withDefaults(defineProps<{
  items?: InventoryItemInfo[]
  productName?: string
}>(), {
  items: () => [],
  productName: ''
})

const emit = defineEmits<{
  refresh: []
}>()

const { t: $t } = useI18n()
</script>
