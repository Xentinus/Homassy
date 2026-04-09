<template>
  <div
    class="group relative bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border-2 p-4 transition-all duration-200 hover:shadow-lg hover:-translate-y-0.5 cursor-pointer"
    :class="[
      automation.isEnabled ? 'border-gray-200 dark:border-gray-700' : 'border-gray-200/60 dark:border-gray-700/60 opacity-70'
    ]"
    @click="handleCardClick"
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
    </div>

    <!-- Status indicator -->
    <div class="flex items-center gap-1.5 mt-2">
      <span class="w-2 h-2 rounded-full" :class="automation.isEnabled ? 'bg-green-500' : 'bg-gray-400'" />
      <span class="text-xs" :class="automation.isEnabled ? 'text-green-600 dark:text-green-400' : 'text-gray-500 dark:text-gray-400'">
        {{ automation.isEnabled ? t('profile.automation.enabled') : t('profile.automation.disabled') }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { AutomationActionType } from '~/types/automation'
import type { AutomationResponse } from '~/types/automation'

interface Props {
  automation: AutomationResponse
}

const props = defineProps<Props>()

const { t } = useI18n()

function handleCardClick() {
  navigateTo(`/profile/automation/${props.automation.publicId}`)
}

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

</script>
