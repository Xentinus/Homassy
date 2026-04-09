<template>
  <div
    class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 p-4 transition-all duration-200 hover:shadow-lg hover:-translate-y-0.5 card-animate"
    :class="[
      pulseState === 'success' ? 'border-green-400 dark:border-green-500 animate-pulse-success' :
      pulseState === 'error' ? 'border-red-400 dark:border-red-500 animate-pulse-error' :
      automation.isEnabled ? 'border-gray-200 dark:border-gray-700' : 'border-gray-200/60 dark:border-gray-700/60 opacity-70'
    ]"
  >
    <!-- Top Row: Icon + Name + Actions -->
    <div class="flex items-start justify-between gap-3">
      <div class="flex items-center gap-3 min-w-0 flex-1">
        <!-- Action Type Icon Badge -->
        <div
          class="flex-shrink-0 w-10 h-10 rounded-lg flex items-center justify-center"
          :class="actionTypeIconBg"
        >
          <UIcon :name="actionTypeIconName" class="h-5 w-5 text-white" />
        </div>
        <div class="min-w-0 flex-1">
          <h3 class="font-bold text-sm text-gray-900 dark:text-white truncate">{{ automation.productName }}</h3>
          <p v-if="automation.productBrand" class="text-xs text-gray-500 dark:text-gray-400 truncate">{{ automation.productBrand }}</p>
        </div>
      </div>

      <div class="flex items-center gap-1.5 flex-shrink-0">
        <USwitch
          :model-value="automation.isEnabled"
          :disabled="toggling"
          :loading="toggling"
          size="sm"
          @update:model-value="(val: boolean) => emit('toggle', val)"
        />
        <UDropdownMenu :items="menuItems" size="md">
          <UButton
            icon="i-lucide-ellipsis-vertical"
            size="xs"
            variant="ghost"
            color="neutral"
          />
        </UDropdownMenu>
      </div>
    </div>

    <!-- Info Chips -->
    <div class="flex flex-wrap items-center gap-1.5 mt-3">
      <!-- Action Type Badge -->
      <span
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
        :class="actionTypeBadgeClass"
      >
        {{ actionTypeText }}
      </span>

      <!-- Schedule (not for LowStock) -->
      <span
        v-if="automation.actionType !== AutomationActionType.LowStockAddToShoppingList"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400"
      >
        <UIcon name="i-lucide-calendar" class="h-3 w-3" />
        {{ scheduleText }}
      </span>

      <!-- Time (not for LowStock) -->
      <span
        v-if="automation.actionType !== AutomationActionType.LowStockAddToShoppingList"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400"
      >
        <UIcon name="i-lucide-clock" class="h-3 w-3" />
        {{ automation.scheduledTime }}
      </span>

      <!-- Threshold (for LowStock) -->
      <span
        v-if="automation.actionType === AutomationActionType.LowStockAddToShoppingList && automation.thresholdQuantity"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300"
      >
        <UIcon name="i-lucide-arrow-down" class="h-3 w-3" />
        {{ t('profile.automation.belowThreshold', { threshold: automation.thresholdQuantity }) }}
      </span>

      <!-- Triggered / Monitoring status (for LowStock) -->
      <span
        v-if="automation.actionType === AutomationActionType.LowStockAddToShoppingList"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs"
        :class="automation.isTriggered
          ? 'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-300'
          : 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300'"
      >
        <span class="w-1.5 h-1.5 rounded-full" :class="automation.isTriggered ? 'bg-orange-500' : 'bg-emerald-500'" />
        {{ automation.isTriggered ? t('profile.automation.triggered') : t('profile.automation.monitoring') }}
      </span>

      <!-- Shopping List (for AddToShoppingList and LowStock) -->
      <span
        v-if="(automation.actionType === AutomationActionType.AddToShoppingList || automation.actionType === AutomationActionType.LowStockAddToShoppingList) && automation.shoppingListName"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400"
      >
        <UIcon name="i-lucide-list" class="h-3 w-3" />
        {{ automation.shoppingListName }}
      </span>

      <!-- Quantity -->
      <span
        v-if="quantityText"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400"
      >
        <UIcon name="i-lucide-hash" class="h-3 w-3" />
        {{ quantityText }}
      </span>
    </div>

    <!-- Footer: Next execution + Quick actions -->
    <div class="flex items-center justify-between mt-3 pt-2 border-t border-gray-100 dark:border-gray-800">
      <p v-if="automation.actionType === AutomationActionType.LowStockAddToShoppingList" class="text-xs text-gray-500 dark:text-gray-400">
        <UIcon name="i-lucide-scan-eye" class="h-3 w-3 inline" />
        {{ t('profile.automation.eventDriven') }}
      </p>
      <p v-else-if="automation.nextExecutionAt" class="text-xs text-gray-500 dark:text-gray-400">
        <UIcon name="i-lucide-timer" class="h-3 w-3 inline" />
        {{ $t('profile.automation.nextExecution') }}: {{ formatDateTime(automation.nextExecutionAt) }}
      </p>
      <p v-else class="text-xs text-gray-400" />

      <div class="flex items-center gap-1">
        <UButton
          v-if="automation.actionType !== AutomationActionType.LowStockAddToShoppingList"
          size="xs"
          variant="soft"
          icon="i-lucide-play"
          :loading="executing"
          :disabled="executing"
          @click="emit('execute')"
        />
        <UButton
          size="xs"
          variant="ghost"
          color="neutral"
          icon="i-lucide-history"
          @click="emit('history')"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  AutomationActionType,
  ScheduleType,
  DaysOfWeek
} from '~/types/automation'
import type { AutomationResponse } from '~/types/automation'

interface Props {
  automation: AutomationResponse
  toggling?: boolean
  executing?: boolean
  pulseState?: 'success' | 'error'
}

const props = withDefaults(defineProps<Props>(), {
  toggling: false,
  executing: false,
  pulseState: undefined
})

const emit = defineEmits<{
  toggle: [enabled: boolean]
  edit: []
  delete: []
  execute: []
  history: []
}>()

const { t } = useI18n()

const isLowStock = computed(() => props.automation.actionType === AutomationActionType.LowStockAddToShoppingList)

// Menu items
const menuItems = computed(() => {
  const editGroup = [
    {
      label: t('common.edit'),
      icon: 'i-lucide-pencil',
      click: () => emit('edit')
    },
    ...(!isLowStock.value ? [{
      label: t('profile.automation.executeNow'),
      icon: 'i-lucide-play',
      click: () => emit('execute')
    }] : []),
    {
      label: t('profile.automation.history'),
      icon: 'i-lucide-history',
      click: () => emit('history')
    }
  ]
  return [editGroup, [
    {
      label: t('common.delete'),
      icon: 'i-lucide-trash-2',
      color: 'error' as const,
      click: () => emit('delete')
    }
  ]]
})

// Action type display
const actionTypeIconName = computed(() => {
  switch (props.automation.actionType) {
    case AutomationActionType.AutoConsume: return 'i-lucide-zap'
    case AutomationActionType.NotifyOnly: return 'i-lucide-bell'
    case AutomationActionType.AddToShoppingList: return 'i-lucide-shopping-cart'
    case AutomationActionType.LowStockAddToShoppingList: return 'i-lucide-triangle-alert'
    default: return 'i-lucide-zap'
  }
})

const actionTypeIconBg = computed(() => {
  switch (props.automation.actionType) {
    case AutomationActionType.AutoConsume: return 'bg-blue-500'
    case AutomationActionType.NotifyOnly: return 'bg-amber-500'
    case AutomationActionType.AddToShoppingList: return 'bg-green-500'
    case AutomationActionType.LowStockAddToShoppingList: return 'bg-red-500'
    default: return 'bg-gray-500'
  }
})

const actionTypeBadgeClass = computed(() => {
  switch (props.automation.actionType) {
    case AutomationActionType.AutoConsume:
      return 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
    case AutomationActionType.NotifyOnly:
      return 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300'
    case AutomationActionType.AddToShoppingList:
      return 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300'
    case AutomationActionType.LowStockAddToShoppingList:
      return 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300'
    default:
      return 'bg-gray-100 dark:bg-gray-800'
  }
})

const actionTypeText = computed(() => {
  switch (props.automation.actionType) {
    case AutomationActionType.AutoConsume: return t('profile.automation.autoConsume')
    case AutomationActionType.NotifyOnly: return t('profile.automation.notifyOnly')
    case AutomationActionType.AddToShoppingList: return t('profile.automation.addToShoppingList')
    case AutomationActionType.LowStockAddToShoppingList: return t('profile.automation.lowStockAddToShoppingList')
    default: return ''
  }
})

// Schedule display
const scheduleText = computed(() => {
  if (props.automation.scheduleType === ScheduleType.Interval) {
    return t('profile.automation.everyNDays', { n: props.automation.intervalDays })
  }
  if (props.automation.scheduledDaysOfWeek && props.automation.scheduledDaysOfWeek !== 0) {
    const dayNames = getSelectedDayNames(props.automation.scheduledDaysOfWeek)
    const flags = props.automation.scheduledDaysOfWeek
    const isSingleDay = flags !== 0 && (flags & (flags - 1)) === 0
    if (isSingleDay) {
      return t('profile.automation.everyWeekday', { day: dayNames })
    }
    return t('profile.automation.everyWeekdays', { days: dayNames })
  }
  if (props.automation.scheduledDayOfMonth) {
    return t('profile.automation.everyMonthDay', { day: props.automation.scheduledDayOfMonth })
  }
  return ''
})

function getSelectedDayNames(flags: number): string {
  const dayMap = [
    { flag: DaysOfWeek.Monday, key: 'monday' },
    { flag: DaysOfWeek.Tuesday, key: 'tuesday' },
    { flag: DaysOfWeek.Wednesday, key: 'wednesday' },
    { flag: DaysOfWeek.Thursday, key: 'thursday' },
    { flag: DaysOfWeek.Friday, key: 'friday' },
    { flag: DaysOfWeek.Saturday, key: 'saturday' },
    { flag: DaysOfWeek.Sunday, key: 'sunday' }
  ]
  return dayMap
    .filter(d => (flags & d.flag) !== 0)
    .map(d => t(`profile.automation.daysShort.${d.key}`))
    .join(', ')
}

// Quantity display
const quantityText = computed(() => {
  if (props.automation.consumeQuantity) {
    const unit = props.automation.consumeUnit !== undefined ? t(`enums.unit.${props.automation.consumeUnit}`) : ''
    return `${props.automation.consumeQuantity} ${unit}`.trim()
  }
  if (props.automation.addQuantity) {
    const unit = props.automation.addUnit !== undefined ? t(`enums.unit.${props.automation.addUnit}`) : ''
    return `${props.automation.addQuantity} ${unit}`.trim()
  }
  return ''
})

function formatDateTime(dateStr: string): string {
  return new Date(dateStr).toLocaleString()
}
</script>

<style scoped>
.animate-pulse-success {
  animation: pulse-success 0.5s ease-in-out 3;
}
.animate-pulse-error {
  animation: pulse-error 0.5s ease-in-out 3;
}

@keyframes pulse-success {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(34, 197, 94, 0);
  }
  50% {
    box-shadow: 0 0 16px 4px rgba(34, 197, 94, 0.35);
  }
}

@keyframes pulse-error {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(239, 68, 68, 0);
  }
  50% {
    box-shadow: 0 0 16px 4px rgba(239, 68, 68, 0.35);
  }
}
</style>
