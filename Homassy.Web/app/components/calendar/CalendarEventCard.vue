<template>
  <div
    class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-4 py-3 border-l-4"
    :class="isExternal ? '' : borderClass"
    :style="isExternal ? { borderLeftColor: color ?? '#3B82F6' } : {}"
  >
    <div class="flex items-start justify-between gap-2">
      <span class="text-sm font-medium text-gray-900 dark:text-gray-100 leading-snug">
        {{ title }}
      </span>
      <span
        class="shrink-0 text-xs rounded px-1.5 py-0.5 leading-tight"
        :class="isExternal ? 'text-white' : chipClass"
        :style="isExternal ? { backgroundColor: color ?? '#3B82F6' } : {}"
      >
        {{ t(`pages.calendar.eventTypes.${eventTypeKey}`) }}
      </span>
    </div>
    <p v-if="detail" class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
      {{ detail }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { CalendarEventType } from '~/types/calendar'

const props = defineProps<{
  title: string
  eventType: CalendarEventType
  detail: string | null
  color: string | null
}>()

const { t } = useI18n()

const isExternal = computed(() => props.eventType === CalendarEventType.ExternalCalendar)

const borderClass = computed(() => {
  switch (props.eventType) {
    case CalendarEventType.InventoryExpiration: return 'border-l-red-500'
    case CalendarEventType.AutomationExecution: return 'border-l-blue-500'
    case CalendarEventType.ShoppingListDeadline: return 'border-l-amber-500'
    default: return 'border-l-gray-300'
  }
})

const chipClass = computed(() => {
  switch (props.eventType) {
    case CalendarEventType.InventoryExpiration:
      return 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300'
    case CalendarEventType.AutomationExecution:
      return 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300'
    case CalendarEventType.ShoppingListDeadline:
      return 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300'
    default:
      return 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300'
  }
})

const eventTypeKey = computed(() => {
  switch (props.eventType) {
    case CalendarEventType.InventoryExpiration: return 'inventoryExpiration'
    case CalendarEventType.AutomationExecution: return 'automationExecution'
    case CalendarEventType.ShoppingListDeadline: return 'shoppingListDeadline'
    case CalendarEventType.ExternalCalendar: return 'externalCalendar'
    default: return 'inventoryExpiration'
  }
})
</script>
