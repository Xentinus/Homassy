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

    <!-- Card surface -->
    <div
      ref="cardEl"
      class="relative h-full bg-default rounded-2xl border-2 p-3 cursor-pointer shadow-sm hover:shadow-lg transition-shadow duration-200 flex flex-col overflow-hidden select-none"
      :class="[cardBorderClass, automation.isEnabled ? '' : 'opacity-70']"
      :style="swipe.cardStyle.value"
      @click="handleCardClick"
    >
      <!-- Header: action badge + product -->
      <div class="flex items-center gap-3 min-w-0">
        <div class="flex-shrink-0 w-10 h-10 rounded-xl flex items-center justify-center" :class="actionTypeIconBg">
          <UIcon :name="actionTypeIconName" class="h-5 w-5 text-white" />
        </div>
        <div class="min-w-0 flex-1">
          <h3 class="text-sm font-bold text-highlighted truncate">{{ automation.productName }}</h3>
          <p v-if="automation.productBrand" class="text-xs text-muted truncate">{{ automation.productBrand }}</p>
        </div>
        <UIcon v-if="automation.isTriggered" name="i-lucide-flame" class="h-4 w-4 text-orange-500 flex-shrink-0" :title="$t('profile.automation.triggered')" />
      </div>

      <!-- Details (pinned bottom) -->
      <div class="mt-auto pt-4 space-y-2">
        <div class="flex items-center gap-2 text-xs">
          <UIcon :name="actionTypeIconName" class="h-3.5 w-3.5 text-primary-500 flex-shrink-0" />
          <span class="text-toned truncate">{{ actionTypeLabel }}</span>
        </div>
        <div v-if="scheduleSummary" class="flex items-center gap-2 text-xs">
          <UIcon name="i-lucide-clock" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400 flex-shrink-0" />
          <span class="text-toned truncate">{{ scheduleSummary }}</span>
        </div>
        <div class="flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full flex-shrink-0" :class="automation.isEnabled ? 'bg-green-500' : 'bg-gray-400'" />
          <span class="text-xs" :class="automation.isEnabled ? 'text-green-600 dark:text-green-400' : 'text-muted'">
            {{ automation.isEnabled ? $t('profile.automation.enabled') : $t('profile.automation.disabled') }}
          </span>
        </div>
      </div>
    </div>

    <!-- Delete confirmation -->
    <AppDrawer :open="isDeleteModalOpen" :title="$t('profile.automation.deleteAutomation')" icon="i-lucide-trash-2" fit="content" @update:open="(v) => { isDeleteModalOpen = v }">
      <p class="text-sm text-muted">{{ $t('profile.automation.deleteWarning') }}</p>
      <div>
        <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('common.name') }}:</span>
        <span class="text-sm ml-2">{{ automation.productName }}</span>
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
import { AutomationActionType, ScheduleType } from '~/types/automation'
import type { AutomationResponse } from '~/types/automation'
import { useAutomationApi } from '~/composables/api/useAutomationApi'

const props = defineProps<{
  automation: AutomationResponse
}>()

const emit = defineEmits<{
  deleted: [publicId: string]
}>()

const { t } = useI18n()
const toast = useToast()
const { deleteAutomation } = useAutomationApi()

const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

const cardEl = ref<HTMLElement | null>(null)
const swipe = useSwipeActions(cardEl, {
  onSwipeLeft: () => { isDeleteModalOpen.value = true },
  onSwipeRight: () => goToDetail(),
  disabled: () => isDeleteModalOpen.value || isDeleting.value
})

const cardBorderClass = computed(() => 'border-gray-200 dark:border-gray-700')

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

const actionTypeLabel = computed(() => {
  switch (props.automation.actionType) {
    case AutomationActionType.AutoConsume: return t('profile.automation.autoConsume')
    case AutomationActionType.NotifyOnly: return t('profile.automation.notifyOnly')
    case AutomationActionType.AddToShoppingList: return t('profile.automation.addToShoppingList')
    case AutomationActionType.LowStockAddToShoppingList: return t('profile.automation.lowStockAddToShoppingList')
    default: return ''
  }
})

// Low-stock rules are event-driven (no schedule). Others show their schedule type + time.
const scheduleSummary = computed(() => {
  if (props.automation.actionType === AutomationActionType.LowStockAddToShoppingList) return ''
  const typeLabel = props.automation.scheduleType === ScheduleType.Interval
    ? t('profile.automation.interval')
    : t('profile.automation.fixedDate')
  const time = props.automation.scheduledTime ? props.automation.scheduledTime.slice(0, 5) : ''
  return time ? `${typeLabel} · ${time}` : typeLabel
})

function goToDetail() {
  navigateTo(`/profile/automation/${props.automation.publicId}`)
}

function handleCardClick(event: MouseEvent) {
  if (swipe.suppressClick.value) return
  const target = event.target as HTMLElement
  if (target.closest('a, button')) return
  goToDetail()
}

async function handleDelete() {
  isDeleting.value = true
  try {
    await deleteAutomation(props.automation.publicId)
    isDeleteModalOpen.value = false
    emit('deleted', props.automation.publicId)
  } catch (error) {
    console.error('Failed to delete automation:', error)
    toast.add({ title: t('common.error'), description: t('profile.automation.deleteFailed'), color: 'error' })
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
