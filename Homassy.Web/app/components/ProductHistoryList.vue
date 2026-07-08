<template>
  <section class="space-y-3">
    <!-- Navbar-style header: icon + title + count -->
    <div class="flex items-center gap-3 border-b border-gray-200 dark:border-gray-800 pb-3">
      <UIcon name="i-lucide-history" class="h-6 w-6 text-primary-500 shrink-0" />
      <h2 class="text-xl font-semibold leading-tight">
        {{ $t('pages.products.details.historyHeader') }}
      </h2>
      <UBadge color="primary" variant="soft" size="sm">{{ items.length }}</UBadge>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="space-y-2">
      <USkeleton v-for="i in 3" :key="i" class="h-16 w-full rounded-xl" />
    </div>

    <!-- Empty -->
    <div v-else-if="items.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
      {{ $t('pages.products.details.noHistory') }}
    </div>

    <!-- Timeline (newest first) -->
    <div v-else class="space-y-2">
      <div
        v-for="event in items"
        :key="event.eventId"
        class="rounded-xl border-2 border-gray-200 dark:border-gray-700 bg-default p-3 flex items-start gap-3"
      >
        <UIcon :name="iconFor(event.type)" class="h-5 w-5 shrink-0 mt-0.5" :class="colorFor(event.type)" />

        <div class="min-w-0 flex-1">
          <div class="flex items-center justify-between gap-2">
            <span class="font-semibold text-highlighted text-sm">{{ labelFor(event.type) }}</span>
            <span class="text-xs text-muted shrink-0">{{ formatDate(event.date) }}</span>
          </div>

          <div class="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted">
            <span v-if="event.quantity != null" class="inline-flex items-center gap-1">
              <UIcon name="i-lucide-package-2" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
              {{ event.quantity }}<template v-if="event.unit != null"> {{ $t(`enums.unit.${event.unit}`) }}</template>
            </span>
            <span v-if="event.price != null && event.currency != null" class="inline-flex items-center gap-1">
              <UIcon name="i-lucide-tag" class="h-3.5 w-3.5" />
              {{ event.price }} {{ $t(`enums.currency.${event.currency}`) }}
            </span>
            <span v-if="event.location" class="inline-flex items-center gap-1">
              <UIcon name="i-lucide-map-pin" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400" />
              {{ event.location.name }}
            </span>
            <span v-if="event.userName" class="inline-flex items-center gap-1">
              <UIcon name="i-lucide-user" class="h-3.5 w-3.5" />
              {{ event.userName }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { ProductHistoryEventInfo } from '../types/product'
import { ProductHistoryEventType } from '../types/product'

withDefaults(defineProps<{
  items?: ProductHistoryEventInfo[]
  loading?: boolean
}>(), {
  items: () => [],
  loading: false
})

const { t: $t } = useI18n()
const { formatDate } = useDateFormat()

const iconFor = (type: ProductHistoryEventType): string => {
  switch (type) {
    case ProductHistoryEventType.Purchased: return 'i-lucide-shopping-cart'
    case ProductHistoryEventType.Added: return 'i-lucide-plus-circle'
    case ProductHistoryEventType.Consumed: return 'i-lucide-utensils'
    case ProductHistoryEventType.Updated: return 'i-lucide-pencil'
    case ProductHistoryEventType.Deleted: return 'i-lucide-trash-2'
    default: return 'i-lucide-circle'
  }
}

const colorFor = (type: ProductHistoryEventType): string => {
  switch (type) {
    case ProductHistoryEventType.Purchased: return 'text-green-600 dark:text-green-400'
    case ProductHistoryEventType.Added: return 'text-primary-600 dark:text-primary-400'
    case ProductHistoryEventType.Consumed: return 'text-amber-600 dark:text-amber-400'
    case ProductHistoryEventType.Updated: return 'text-gray-500 dark:text-gray-400'
    case ProductHistoryEventType.Deleted: return 'text-red-600 dark:text-red-400'
    default: return 'text-gray-500'
  }
}

const labelFor = (type: ProductHistoryEventType): string => {
  switch (type) {
    case ProductHistoryEventType.Purchased: return $t('pages.products.details.history.purchased')
    case ProductHistoryEventType.Added: return $t('pages.products.details.history.added')
    case ProductHistoryEventType.Consumed: return $t('pages.products.details.history.consumed')
    case ProductHistoryEventType.Updated: return $t('pages.products.details.history.updated')
    case ProductHistoryEventType.Deleted: return $t('pages.products.details.history.deleted')
    default: return ''
  }
}
</script>
