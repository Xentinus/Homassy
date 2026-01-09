<template>
  <UModal
    :open="isOpen"
    @update:open="$emit('update:isOpen', $event)"
    :dismissible="true"
    :ui="{ width: 'sm:max-w-2xl' }"
  >
    <template #title>
      {{ $t('pages.addProduct.previewModal.title', { count: items.length }) }}
    </template>

    <template #description>
      {{ $t('pages.addProduct.previewModal.description') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Shared Context Info -->
        <div class="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
          <h5 class="text-xs font-semibold text-gray-700 dark:text-gray-300 mb-2">
            {{ $t('pages.addProduct.inventory.sharedContext.title') }}
          </h5>
          <div class="flex flex-wrap gap-2 text-sm">
            <UBadge color="primary" variant="soft" size="sm">
              <span class="flex items-center gap-1">
                <UIcon name="i-lucide-map-pin" class="h-3 w-3" />
                {{ storageLocationName }}
              </span>
            </UBadge>
            <UBadge v-if="shoppingLocationName" color="secondary" variant="soft" size="sm">
              <span class="flex items-center gap-1">
                <UIcon name="i-lucide-shopping-cart" class="h-3 w-3" />
                {{ shoppingLocationName }}
              </span>
            </UBadge>
            <UBadge :color="isSharedWithFamily ? 'green' : 'gray'" variant="soft" size="sm">
              <span class="flex items-center gap-1">
                <UIcon :name="isSharedWithFamily ? 'i-lucide-users' : 'i-lucide-user'" class="h-3 w-3" />
                {{ isSharedWithFamily ? $t('common.family') : $t('common.personal') }}
              </span>
            </UBadge>
          </div>
        </div>

        <!-- Items List -->
        <div class="space-y-3 max-h-96 overflow-y-auto">
          <div
            v-for="(item, index) in items"
            :key="item.id"
            class="p-4 border border-gray-300 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-800"
          >
            <div v-if="items.length > 1" class="flex items-start justify-between mb-2">
              <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">
                {{ $t('pages.addProduct.previewModal.itemNumber', { number: index + 1 }) }}
              </span>
            </div>

            <div class="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
              <div>
                <span class="text-gray-600 dark:text-gray-400">{{ $t('common.quantity') }}:</span>
                <span class="ml-2 font-medium">{{ item.quantity }} {{ $t(`enums.unit.${item.unit}`) }}</span>
              </div>

              <div v-if="item.expirationAt">
                <span class="text-gray-600 dark:text-gray-400">{{ $t('common.expirationDate') }}:</span>
                <span class="ml-2 font-medium">{{ formatDate(item.expirationAt) }}</span>
              </div>

              <div v-if="item.price && item.price > 0">
                <span class="text-gray-600 dark:text-gray-400">{{ $t('common.price') }}:</span>
                <span class="ml-2 font-medium">{{ item.price }} {{ item.currency ? $t(`enums.currency.${item.currency}`) : '' }}</span>
              </div>

              <div v-if="item.receiptNumber" class="col-span-2">
                <span class="text-gray-600 dark:text-gray-400">{{ $t('pages.addProduct.inventory.form.receiptNumber') }}:</span>
                <span class="ml-2 font-medium truncate">{{ item.receiptNumber }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Summary -->
        <div class="p-3 bg-primary-50 dark:bg-primary-900/20 rounded-lg">
          <p class="text-sm font-medium text-primary-900 dark:text-primary-100">
            {{ $t('pages.addProduct.previewModal.summary', { count: items.length }) }}
          </p>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="outline"
          @click="$emit('update:isOpen', false)"
        />
        <UButton
          :label="$t('pages.addProduct.previewModal.confirmButton', { count: items.length })"
          color="primary"
          icon="i-lucide-check"
          @click="$emit('confirm')"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
interface PreviewItem {
  id: string
  quantity: number
  unit: any
  expirationAt: any | null
  price?: number
  currency?: any
  receiptNumber?: string
}

defineProps<{
  isOpen: boolean
  items: PreviewItem[]
  storageLocationName: string
  shoppingLocationName?: string
  isSharedWithFamily: boolean
}>()

defineEmits<{
  'update:isOpen': [value: boolean]
  'confirm': []
}>()

const { t } = useI18n()

const formatDate = (date: any): string => {
  if (!date) return ''
  return `${date.year}-${String(date.month).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`
}
</script>
