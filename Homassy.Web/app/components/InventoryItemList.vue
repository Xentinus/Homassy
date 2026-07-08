<template>
  <section class="space-y-3">
    <!-- Navbar-style header: icon + title + count + settings (⚙) action -->
    <div class="flex items-center gap-3 border-b border-gray-200 dark:border-gray-800 pb-3">
      <UIcon name="i-lucide-package-2" class="h-6 w-6 text-primary-500 shrink-0" />
      <h2 class="text-xl font-semibold leading-tight">
        {{ $t('pages.products.details.inventoryHeader') }}
      </h2>
      <UBadge color="primary" variant="soft" size="sm">{{ items.length }}</UBadge>
      <UButton
        class="ml-auto"
        icon="i-lucide-settings"
        color="primary"
        variant="soft"
        :aria-label="$t('pages.products.details.settingsAriaLabel')"
        :disabled="items.length === 0"
        @click="operationsOpen = true"
      />
    </div>

    <!-- Empty state -->
    <div v-if="items.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
      {{ $t('pages.products.details.noInventoryItems') }}
    </div>

    <!-- Full-width rows -->
    <div v-else class="space-y-2">
      <InventoryItemRow
        v-for="item in items"
        :key="item.publicId"
        :item="item"
        :product-name="productName"
        @consumed="emit('refresh')"
        @updated="emit('refresh')"
        @deleted="emit('refresh')"
      />
    </div>

    <!-- Felosztás / Készlet mozgatás operations (⚙) -->
    <InventoryOperationsDrawer
      v-model:open="operationsOpen"
      :items="items"
      @changed="emit('refresh')"
    />
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { InventoryItemInfo } from '../types/product'

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

const operationsOpen = ref(false)
</script>
