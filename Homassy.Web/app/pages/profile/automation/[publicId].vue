<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 border-b border-gray-200 dark:border-gray-800">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3 min-w-0 flex-1">
          <NuxtLink to="/profile/automation">
            <UButton
              icon="i-lucide-arrow-left"
              color="neutral"
              variant="ghost"
            />
          </NuxtLink>
        </div>
        <div v-if="automation" class="flex items-center gap-2 flex-shrink-0">
          <USwitch
            :model-value="automation.isEnabled"
            :disabled="isToggling"
            :loading="isToggling"
            size="sm"
            @update:model-value="toggleEnabled"
          />
          <UButton
            icon="i-lucide-pencil"
            color="neutral"
            variant="ghost"
            size="sm"
            @click="openEditModal"
          />
          <UButton
            icon="i-lucide-trash-2"
            color="error"
            variant="ghost"
            size="sm"
            @click="confirmDelete"
          />
        </div>
      </div>
    </div>

    <!-- Content -->
    <div class="pt-28 px-4 sm:px-8 lg:px-14 pb-6">
      <!-- Loading State -->
      <div v-if="isLoading" class="space-y-6">
        <USkeleton class="h-64 w-full rounded-xl" />
        <USkeleton class="h-48 w-full rounded-xl" />
      </div>

      <!-- Error State -->
      <div v-else-if="error" class="text-center py-12">
        <UIcon name="i-lucide-alert-circle" class="h-16 w-16 mx-auto text-red-500 mb-4" />
        <p class="text-lg text-gray-600 dark:text-gray-400">
          {{ $t('profile.automation.executeErrors.AUTOMATION-0001') }}
        </p>
      </div>

      <!-- Automation Details -->
      <div v-else-if="automation" class="space-y-6">
        <!-- Details Card -->
        <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-5 space-y-4">
          <!-- Product Info -->
          <div class="flex items-center gap-3">
            <div
              class="flex-shrink-0 w-12 h-12 rounded-xl flex items-center justify-center"
              :class="actionTypeIconBg"
            >
              <UIcon :name="actionTypeIconName" class="h-6 w-6 text-white" />
            </div>
            <div class="min-w-0 flex-1">
              <h2 class="text-lg font-bold text-gray-900 dark:text-white truncate">{{ automation.productName }}</h2>
              <p v-if="automation.productBrand" class="text-sm text-gray-500 dark:text-gray-400">{{ automation.productBrand }}</p>
            </div>
          </div>

          <div class="border-t border-gray-100 dark:border-gray-700" />

          <!-- Action Type -->
          <div class="flex items-center gap-2">
            <span
              class="inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-medium"
              :class="actionTypeBadgeClass"
            >
              {{ actionTypeText }}
            </span>
          </div>

          <!-- Schedule Details (not for LowStock) -->
          <div v-if="!isLowStock" class="space-y-2">
            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <UIcon name="i-lucide-calendar" class="h-4 w-4" />
              <span>{{ scheduleText }}</span>
            </div>
            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <UIcon name="i-lucide-clock" class="h-4 w-4" />
              <span>{{ automation.scheduledTime }}</span>
            </div>
            <div v-if="automation.nextExecutionAt" class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <UIcon name="i-lucide-timer" class="h-4 w-4" />
              <span>{{ t('profile.automation.nextExecution') }}: {{ formatDateTime(automation.nextExecutionAt) }}</span>
            </div>
          </div>

          <!-- LowStock Details -->
          <div v-if="isLowStock" class="space-y-2">
            <div v-if="automation.thresholdQuantity" class="flex items-center gap-2 text-sm">
              <UIcon name="i-lucide-arrow-down" class="h-4 w-4 text-red-500" />
              <span class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.belowThreshold', { threshold: automation.thresholdQuantity }) }}</span>
            </div>
            <div class="flex items-center gap-2 text-sm">
              <span
                class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs"
                :class="automation.isTriggered
                  ? 'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-300'
                  : 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300'"
              >
                <span class="w-1.5 h-1.5 rounded-full" :class="automation.isTriggered ? 'bg-orange-500' : 'bg-emerald-500'" />
                {{ automation.isTriggered ? t('profile.automation.triggered') : t('profile.automation.monitoring') }}
              </span>
            </div>
            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <UIcon name="i-lucide-scan-eye" class="h-4 w-4" />
              <span>{{ t('profile.automation.eventDriven') }}</span>
            </div>
          </div>

          <!-- Shopping List -->
          <div v-if="automation.shoppingListName && (automation.actionType === AutomationActionType.AddToShoppingList || isLowStock)" class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
            <UIcon name="i-lucide-list" class="h-4 w-4" />
            <span>{{ automation.shoppingListName }}</span>
          </div>

          <!-- Quantity -->
          <div v-if="quantityText" class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
            <UIcon name="i-lucide-hash" class="h-4 w-4" />
            <span>{{ quantityText }}</span>
          </div>

          <!-- Last Executed -->
          <div v-if="automation.lastExecutedAt" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
            <UIcon name="i-lucide-check-circle" class="h-4 w-4" />
            <span>{{ t('profile.automation.lastExecuted') }}: {{ formatDateTime(automation.lastExecutedAt) }}</span>
          </div>

          <!-- Execute Button -->
          <div v-if="!isLowStock" class="pt-2">
            <UButton
              :label="t('profile.automation.executeNow')"
              icon="i-lucide-play"
              :loading="isExecuting"
              :disabled="isExecuting"
              size="sm"
              :class="[
                pulseState === 'success' ? 'animate-pulse-success' :
                pulseState === 'error' ? 'animate-pulse-error' : ''
              ]"
              @click="handleExecute"
            />
          </div>
        </div>

        <!-- Execution History Section -->
        <div class="space-y-3">
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
            {{ t('profile.automation.executionHistory') }}
          </h3>

          <!-- History Loading -->
          <div v-if="isLoadingHistory" class="space-y-3">
            <USkeleton v-for="i in 3" :key="i" class="h-16 w-full rounded-lg" />
          </div>

          <!-- No history -->
          <div v-else-if="executionHistory.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
            <UIcon name="i-lucide-history" class="h-10 w-10 mx-auto mb-2 opacity-50" />
            <p>{{ t('profile.automation.noHistory') }}</p>
          </div>

          <!-- History Items -->
          <template v-else>
            <div
              v-for="execution in executionHistory"
              :key="execution.publicId"
              class="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-3 space-y-1"
            >
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium" :class="executionStatusColor(execution.status)">
                  {{ formatExecutionStatus(execution.status) }}
                </span>
                <span class="text-xs text-gray-500 dark:text-gray-400">
                  {{ formatDateTime(execution.executedAt) }}
                </span>
              </div>
              <div v-if="execution.consumedQuantity" class="text-xs text-gray-600 dark:text-gray-400">
                {{ t('common.quantity') }}: {{ execution.consumedQuantity }}
              </div>
              <div v-if="execution.notes" class="text-xs text-gray-500 dark:text-gray-400 italic">
                {{ execution.notes }}
              </div>
            </div>

            <!-- Infinite Scroll Sentinel -->
            <div ref="sentinelRef" class="h-10">
              <div v-if="isLoadingMore" class="flex items-center justify-center py-3">
                <USkeleton v-for="i in 2" :key="i" class="h-16 w-full rounded-lg mb-3" />
              </div>
              <p v-else-if="!hasMoreHistory" class="text-center text-xs text-gray-400 py-2">
                {{ t('profile.automation.noMoreHistory') }}
              </p>
            </div>
          </template>
        </div>
      </div>
    </div>
  </div>

  <!-- Edit Modal -->
  <UModal :open="isEditModalOpen" @update:open="(val: boolean) => isEditModalOpen = val" :dismissible="false">
    <template #title>
      {{ $t('profile.automation.editAutomation') }}
    </template>

    <template #description>
      {{ $t('profile.automation.editDescription') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Action Type -->
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.actionType') }} <span class="text-red-500">*</span>
          </label>
          <USelect
            v-model="form.actionType"
            :items="actionTypeOptions"
            value-key="value"
            label-key="label"
            class="w-full"
          />
        </div>

        <!-- Shopping List Selector -->
        <div v-if="form.actionType === AutomationActionType.AddToShoppingList || form.actionType === AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.shoppingList') }} <span class="text-red-500">*</span>
          </label>
          <USelect
            v-model="form.shoppingListPublicId"
            :items="shoppingListOptions"
            value-key="value"
            label-key="label"
            :placeholder="$t('profile.automation.selectShoppingList')"
            class="w-full"
          />
        </div>

        <!-- Product Selector -->
        <div v-if="form.actionType === AutomationActionType.AddToShoppingList || form.actionType === AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.product') }} <span class="text-red-500">*</span>
          </label>
          <USelect
            v-model="form.productPublicId"
            :items="productOptions"
            value-key="value"
            label-key="label"
            :placeholder="$t('profile.automation.selectProduct')"
            class="w-full"
          />
        </div>

        <!-- Threshold Quantity -->
        <div v-if="form.actionType === AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.thresholdQuantity') }} <span class="text-red-500">*</span>
          </label>
          <UInput
            v-model.number="form.thresholdQuantity"
            type="number"
            :min="0.001"
            step="0.1"
            class="w-full"
          />
        </div>

        <!-- Schedule Type -->
        <div v-if="form.actionType !== AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.scheduleType') }} <span class="text-red-500">*</span>
          </label>
          <USelect
            v-model="form.scheduleType"
            :items="scheduleTypeOptions"
            value-key="value"
            label-key="label"
            class="w-full"
          />
        </div>

        <!-- Interval Days -->
        <div v-if="form.scheduleType === ScheduleType.Interval && form.actionType !== AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.intervalDays') }} <span class="text-red-500">*</span>
          </label>
          <UInput
            v-model.number="form.intervalDays"
            type="number"
            :min="1"
            :max="365"
            class="w-full"
          />
        </div>

        <!-- Days of Week -->
        <div v-if="form.scheduleType === ScheduleType.FixedDate && form.actionType !== AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.scheduledDaysOfWeek') }}
          </label>
          <div class="flex flex-wrap gap-2">
            <button
              v-for="day in daysOfWeekOptions"
              :key="day.value"
              type="button"
              class="px-3 py-1.5 text-sm rounded-full border transition-colors"
              :class="isDaySelected(day.value) ? 'bg-primary-500 text-white border-primary-500' : 'bg-gray-100 dark:bg-gray-800 border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-300 hover:border-primary-300'"
              @click="toggleDay(day.value)"
            >
              {{ day.label }}
            </button>
          </div>
        </div>

        <!-- Day of Month -->
        <div v-if="form.scheduleType === ScheduleType.FixedDate && form.actionType !== AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.scheduledDayOfMonth') }}
          </label>
          <UInput
            v-model.number="form.scheduledDayOfMonth"
            type="number"
            :min="1"
            :max="31"
            :placeholder="$t('profile.automation.dayOfMonthPlaceholder')"
            class="w-full"
          />
        </div>

        <!-- Scheduled Time -->
        <div v-if="form.actionType !== AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.scheduledTime') }} <span class="text-red-500">*</span>
          </label>
          <UInput
            v-model="form.scheduledTime"
            type="time"
            class="w-full"
          />
        </div>

        <!-- Consume Quantity -->
        <div v-if="form.actionType === AutomationActionType.AutoConsume">
          <label class="block text-sm font-medium mb-1">
            {{ $t('common.quantity') }}
          </label>
          <div class="flex gap-2">
            <UInput
              v-model.number="form.consumeQuantity"
              type="number"
              :min="0.001"
              step="0.1"
              class="flex-1"
            />
            <USelect
              v-model="form.consumeUnit"
              :items="unitOptions"
              value-key="value"
              label-key="label"
              :placeholder="$t('common.unit')"
              class="w-32"
            />
          </div>
        </div>

        <!-- Add Quantity -->
        <div v-if="form.actionType === AutomationActionType.AddToShoppingList || form.actionType === AutomationActionType.LowStockAddToShoppingList">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.addQuantity') }} <span class="text-red-500">*</span>
          </label>
          <div class="flex gap-2">
            <UInput
              v-model.number="form.addQuantity"
              type="number"
              :min="0.001"
              step="0.1"
              class="flex-1"
            />
            <USelect
              v-model="form.addUnit"
              :items="unitOptions"
              value-key="value"
              label-key="label"
              :placeholder="$t('common.unit')"
              class="w-32"
            />
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="outline"
          @click="isEditModalOpen = false"
        />
        <UButton
          :label="$t('common.save')"
          :loading="isSaving"
          @click="handleSave"
        />
      </div>
    </template>
  </UModal>

  <!-- Delete Confirmation Modal -->
  <UModal :open="isDeleteModalOpen" @update:open="(val: boolean) => isDeleteModalOpen = val">
    <template #title>
      {{ $t('profile.automation.deleteAutomation') }}
    </template>

    <template #description>
      {{ $t('profile.automation.deleteWarning') }}
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="outline"
          @click="isDeleteModalOpen = false"
        />
        <UButton
          :label="$t('common.delete')"
          color="error"
          :loading="isDeleting"
          @click="handleDelete"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { useAutomationApi } from '~/composables/api/useAutomationApi'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useShoppingListApi } from '~/composables/api/useShoppingListApi'
import {
  ScheduleType,
  AutomationActionType,
  AutomationExecutionStatus,
  DaysOfWeek
} from '~/types/automation'
import type {
  AutomationResponse,
  AutomationExecutionResponse,
  UpdateAutomationRequest
} from '~/types/automation'
import { Unit } from '~/types/enums'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const route = useRoute()
const {
  getAutomation,
  updateAutomation,
  deleteAutomation,
  executeAutomation,
  getExecutionHistory
} = useAutomationApi()
const { getProducts } = useProductsApi()
const { getShoppingLists } = useShoppingListApi()
const { t, te } = useI18n()
const toast = useToast()

const publicId = computed(() => route.params.publicId as string)

// State
const isLoading = ref(true)
const error = ref(false)
const automation = ref<AutomationResponse | null>(null)
const isToggling = ref(false)
const isExecuting = ref(false)
const pulseState = ref<'success' | 'error' | undefined>(undefined)

// History state
const executionHistory = ref<AutomationExecutionResponse[]>([])
const isLoadingHistory = ref(true)
const isLoadingMore = ref(false)
const hasMoreHistory = ref(true)
const historySkip = ref(0)
const HISTORY_PAGE_SIZE = 5
const sentinelRef = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

// Edit modal state
const isEditModalOpen = ref(false)
const isSaving = ref(false)

// Delete modal state
const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

// Shopping lists / products for edit modal
const shoppingListOptions = ref<{ value: string; label: string }[]>([])
const productOptions = ref<{ value: string; label: string }[]>([])

// Form
const defaultForm = () => ({
  productPublicId: '' as string,
  shoppingListPublicId: '' as string,
  scheduleType: ScheduleType.Interval as ScheduleType,
  intervalDays: 7 as number | undefined,
  scheduledDaysOfWeek: 0 as number,
  scheduledDayOfMonth: undefined as number | undefined,
  scheduledTime: '07:00',
  actionType: AutomationActionType.AutoConsume as AutomationActionType,
  consumeQuantity: undefined as number | undefined,
  consumeUnit: undefined as number | undefined,
  addQuantity: undefined as number | undefined,
  addUnit: undefined as number | undefined,
  thresholdQuantity: undefined as number | undefined
})
const form = ref(defaultForm())

// Computed
const isLowStock = computed(() => automation.value?.actionType === AutomationActionType.LowStockAddToShoppingList)

const actionTypeIconName = computed(() => {
  switch (automation.value?.actionType) {
    case AutomationActionType.AutoConsume: return 'i-lucide-zap'
    case AutomationActionType.NotifyOnly: return 'i-lucide-bell'
    case AutomationActionType.AddToShoppingList: return 'i-lucide-shopping-cart'
    case AutomationActionType.LowStockAddToShoppingList: return 'i-lucide-triangle-alert'
    default: return 'i-lucide-zap'
  }
})

const actionTypeIconBg = computed(() => {
  switch (automation.value?.actionType) {
    case AutomationActionType.AutoConsume: return 'bg-blue-500'
    case AutomationActionType.NotifyOnly: return 'bg-amber-500'
    case AutomationActionType.AddToShoppingList: return 'bg-green-500'
    case AutomationActionType.LowStockAddToShoppingList: return 'bg-red-500'
    default: return 'bg-gray-500'
  }
})

const actionTypeBadgeClass = computed(() => {
  switch (automation.value?.actionType) {
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
  switch (automation.value?.actionType) {
    case AutomationActionType.AutoConsume: return t('profile.automation.autoConsume')
    case AutomationActionType.NotifyOnly: return t('profile.automation.notifyOnly')
    case AutomationActionType.AddToShoppingList: return t('profile.automation.addToShoppingList')
    case AutomationActionType.LowStockAddToShoppingList: return t('profile.automation.lowStockAddToShoppingList')
    default: return ''
  }
})

const scheduleText = computed(() => {
  if (!automation.value) return ''
  if (automation.value.scheduleType === ScheduleType.Interval) {
    return t('profile.automation.everyNDays', { n: automation.value.intervalDays })
  }
  if (automation.value.scheduledDaysOfWeek && automation.value.scheduledDaysOfWeek !== 0) {
    const dayNames = getSelectedDayNames(automation.value.scheduledDaysOfWeek)
    const flags = automation.value.scheduledDaysOfWeek
    const isSingleDay = flags !== 0 && (flags & (flags - 1)) === 0
    return isSingleDay
      ? t('profile.automation.everyWeekday', { day: dayNames })
      : t('profile.automation.everyWeekdays', { days: dayNames })
  }
  if (automation.value.scheduledDayOfMonth) {
    return t('profile.automation.everyMonthDay', { day: automation.value.scheduledDayOfMonth })
  }
  return ''
})

const quantityText = computed(() => {
  if (!automation.value) return ''
  if (automation.value.consumeQuantity) {
    const unit = automation.value.consumeUnit !== undefined ? t(`enums.unit.${automation.value.consumeUnit}`) : ''
    return `${automation.value.consumeQuantity} ${unit}`.trim()
  }
  if (automation.value.addQuantity) {
    const unit = automation.value.addUnit !== undefined ? t(`enums.unit.${automation.value.addUnit}`) : ''
    return `${automation.value.addQuantity} ${unit}`.trim()
  }
  return ''
})

// Select options
const scheduleTypeOptions = computed(() => [
  { value: ScheduleType.Interval, label: t('profile.automation.interval') },
  { value: ScheduleType.FixedDate, label: t('profile.automation.fixedDate') }
])

const actionTypeOptions = computed(() => [
  { value: AutomationActionType.AutoConsume, label: t('profile.automation.autoConsume') },
  { value: AutomationActionType.NotifyOnly, label: t('profile.automation.notifyOnly') },
  { value: AutomationActionType.AddToShoppingList, label: t('profile.automation.addToShoppingList') },
  { value: AutomationActionType.LowStockAddToShoppingList, label: t('profile.automation.lowStockAddToShoppingList') }
])

const daysOfWeekOptions = computed(() => [
  { value: DaysOfWeek.Monday, label: t('profile.automation.days.monday') },
  { value: DaysOfWeek.Tuesday, label: t('profile.automation.days.tuesday') },
  { value: DaysOfWeek.Wednesday, label: t('profile.automation.days.wednesday') },
  { value: DaysOfWeek.Thursday, label: t('profile.automation.days.thursday') },
  { value: DaysOfWeek.Friday, label: t('profile.automation.days.friday') },
  { value: DaysOfWeek.Saturday, label: t('profile.automation.days.saturday') },
  { value: DaysOfWeek.Sunday, label: t('profile.automation.days.sunday') }
])

const unitOptions = computed(() =>
  Object.entries(Unit)
    .filter(([_key]) => isNaN(Number(_key)))
    .map(([_key, value]) => ({
      value: value as number,
      label: t(`enums.unit.${value}`)
    }))
)

// Helpers
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

function isDaySelected(dayFlag: number): boolean {
  return (form.value.scheduledDaysOfWeek & dayFlag) !== 0
}

function toggleDay(dayFlag: number) {
  form.value.scheduledDaysOfWeek ^= dayFlag
}

function formatDateTime(dateStr: string): string {
  return new Date(dateStr).toLocaleString()
}

function formatExecutionStatus(status: AutomationExecutionStatus): string {
  const statusMap: Record<AutomationExecutionStatus, string> = {
    [AutomationExecutionStatus.AutoConsumed]: t('profile.automation.status.autoConsumed'),
    [AutomationExecutionStatus.NotificationSent]: t('profile.automation.status.notificationSent'),
    [AutomationExecutionStatus.ManuallyConfirmed]: t('profile.automation.status.manuallyConfirmed'),
    [AutomationExecutionStatus.Skipped]: t('profile.automation.status.skipped'),
    [AutomationExecutionStatus.Failed]: t('profile.automation.status.failed'),
    [AutomationExecutionStatus.AddedToShoppingList]: t('profile.automation.status.addedToShoppingList')
  }
  return statusMap[status] || ''
}

function executionStatusColor(status: AutomationExecutionStatus): string {
  switch (status) {
    case AutomationExecutionStatus.AutoConsumed:
    case AutomationExecutionStatus.ManuallyConfirmed:
    case AutomationExecutionStatus.AddedToShoppingList:
      return 'text-green-600 dark:text-green-400'
    case AutomationExecutionStatus.NotificationSent:
      return 'text-blue-600 dark:text-blue-400'
    case AutomationExecutionStatus.Skipped:
      return 'text-amber-600 dark:text-amber-400'
    case AutomationExecutionStatus.Failed:
      return 'text-red-600 dark:text-red-400'
    default:
      return 'text-gray-600 dark:text-gray-400'
  }
}

function playFeedbackSound(success: boolean) {
  try {
    const ctx = new AudioContext()
    const now = ctx.currentTime

    if (success) {
      const notes = [1047, 1319, 1568]
      const noteLen = 0.12
      notes.forEach((freq, i) => {
        const osc = ctx.createOscillator()
        const gain = ctx.createGain()
        osc.connect(gain)
        gain.connect(ctx.destination)
        osc.type = 'sine'
        osc.frequency.value = freq
        const start = now + i * noteLen
        gain.gain.setValueAtTime(0, start)
        gain.gain.linearRampToValueAtTime(0.14, start + 0.02)
        gain.gain.exponentialRampToValueAtTime(0.001, start + noteLen + 0.08)
        osc.start(start)
        osc.stop(start + noteLen + 0.08)
        if (i === notes.length - 1) osc.onended = () => ctx.close()
      })
    } else {
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.connect(gain)
      gain.connect(ctx.destination)
      osc.type = 'triangle'
      osc.frequency.setValueAtTime(280, now)
      osc.frequency.linearRampToValueAtTime(180, now + 0.25)
      gain.gain.setValueAtTime(0.18, now)
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.3)
      osc.start(now)
      osc.stop(now + 0.3)
      osc.onended = () => ctx.close()
    }
  } catch { /* AudioContext may not be available */ }
}

// Data loading
async function loadAutomation() {
  isLoading.value = true
  error.value = false
  try {
    const response = await getAutomation(publicId.value)
    if (response.data) {
      automation.value = response.data
    } else {
      error.value = true
    }
  } catch {
    error.value = true
  } finally {
    isLoading.value = false
  }
}

async function loadInitialHistory() {
  isLoadingHistory.value = true
  historySkip.value = 0
  executionHistory.value = []
  hasMoreHistory.value = true

  try {
    const response = await getExecutionHistory(publicId.value, 0, HISTORY_PAGE_SIZE)
    const items = response.data || []
    executionHistory.value = items
    hasMoreHistory.value = items.length >= HISTORY_PAGE_SIZE
    historySkip.value = items.length
  } catch (err) {
    console.error('Failed to load execution history:', err)
  } finally {
    isLoadingHistory.value = false
  }
}

async function loadMoreHistory() {
  if (isLoadingMore.value || !hasMoreHistory.value) return

  isLoadingMore.value = true
  try {
    const response = await getExecutionHistory(publicId.value, historySkip.value, HISTORY_PAGE_SIZE)
    const items = response.data || []
    executionHistory.value.push(...items)
    hasMoreHistory.value = items.length >= HISTORY_PAGE_SIZE
    historySkip.value += items.length
  } catch (err) {
    console.error('Failed to load more history:', err)
  } finally {
    isLoadingMore.value = false
  }
}

// Intersection Observer for infinite scroll
function handleIntersection(entries: IntersectionObserverEntry[]) {
  const entry = entries[0]
  if (entry?.isIntersecting && hasMoreHistory.value && !isLoadingMore.value) {
    loadMoreHistory()
  }
}

function setupObserver() {
  if (!sentinelRef.value) return

  observer.value = new IntersectionObserver(handleIntersection, {
    root: null,
    rootMargin: '100px',
    threshold: 0
  })

  observer.value.observe(sentinelRef.value)
}

// Toggle
async function toggleEnabled(enabled: boolean) {
  if (!automation.value) return
  isToggling.value = true
  try {
    await updateAutomation(automation.value.publicId, { isEnabled: enabled })
    automation.value.isEnabled = enabled
  } catch {
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.updateFailed'),
      color: 'error'
    })
  } finally {
    isToggling.value = false
  }
}

// Execute
async function handleExecute() {
  if (!automation.value) return
  isExecuting.value = true
  try {
    const response = await executeAutomation(automation.value.publicId, {})
    if (response.success) {
      pulseState.value = 'success'
      playFeedbackSound(true)
      // Reload automation and history
      await Promise.all([loadAutomation(), loadInitialHistory()])
    } else {
      pulseState.value = 'error'
      playFeedbackSound(false)
      const errorCode = response.errorCodes?.[0] || 'unknown'
      const errorKey = `profile.automation.executeErrors.${errorCode}`
      const errorMsg = te(errorKey) ? t(errorKey) : t('profile.automation.executeErrors.unknown')
      toast.add({
        title: t('toast.error'),
        description: errorMsg,
        color: 'error'
      })
    }
  } catch {
    pulseState.value = 'error'
    playFeedbackSound(false)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.executeErrors.unknown'),
      color: 'error'
    })
  } finally {
    isExecuting.value = false
    setTimeout(() => { pulseState.value = undefined }, 1500)
  }
}

// Edit modal
async function openEditModal() {
  if (!automation.value) return
  form.value = {
    productPublicId: automation.value.productPublicId || '',
    shoppingListPublicId: automation.value.shoppingListPublicId || '',
    scheduleType: automation.value.scheduleType,
    intervalDays: automation.value.intervalDays,
    scheduledDaysOfWeek: automation.value.scheduledDaysOfWeek || 0,
    scheduledDayOfMonth: automation.value.scheduledDayOfMonth,
    scheduledTime: automation.value.scheduledTime,
    actionType: automation.value.actionType,
    consumeQuantity: automation.value.consumeQuantity,
    consumeUnit: automation.value.consumeUnit,
    addQuantity: automation.value.addQuantity,
    addUnit: automation.value.addUnit,
    thresholdQuantity: automation.value.thresholdQuantity
  }
  isEditModalOpen.value = true
  if (automation.value.actionType === AutomationActionType.AddToShoppingList || automation.value.actionType === AutomationActionType.LowStockAddToShoppingList) {
    await Promise.all([loadShoppingLists(), loadProducts()])
  }
}

async function loadShoppingLists() {
  try {
    const response = await getShoppingLists({ returnAll: true })
    if (response.data?.items) {
      shoppingListOptions.value = response.data.items.map((list: { publicId: string; name: string }) => ({
        value: list.publicId,
        label: list.name
      }))
    }
  } catch (err) {
    console.error('Failed to load shopping lists:', err)
  }
}

async function loadProducts() {
  try {
    const response = await getProducts({ returnAll: true })
    if (response.data?.items) {
      productOptions.value = response.data.items.map((product: { publicId: string; name: string; brand?: string }) => ({
        value: product.publicId,
        label: `${product.name}${product.brand ? ` (${product.brand})` : ''}`
      }))
    }
  } catch (err) {
    console.error('Failed to load products:', err)
  }
}

async function handleSave() {
  const isShoppingListAction = form.value.actionType === AutomationActionType.AddToShoppingList || form.value.actionType === AutomationActionType.LowStockAddToShoppingList
  const isLowStockAction = form.value.actionType === AutomationActionType.LowStockAddToShoppingList

  if (isShoppingListAction) {
    if (!form.value.shoppingListPublicId) {
      toast.add({ title: t('toast.error'), description: t('profile.automation.shoppingListRequired'), color: 'error' })
      return
    }
    if (!form.value.productPublicId) {
      toast.add({ title: t('toast.error'), description: t('profile.automation.productRequired'), color: 'error' })
      return
    }
    if (!form.value.addQuantity || form.value.addQuantity <= 0) {
      toast.add({ title: t('toast.error'), description: t('profile.automation.addQuantityRequired'), color: 'error' })
      return
    }
  }

  if (isLowStockAction && (!form.value.thresholdQuantity || form.value.thresholdQuantity <= 0)) {
    toast.add({ title: t('toast.error'), description: t('profile.automation.thresholdRequired'), color: 'error' })
    return
  }

  if (!isLowStockAction && !form.value.scheduledTime) {
    toast.add({ title: t('toast.error'), description: t('profile.automation.timeRequired'), color: 'error' })
    return
  }

  if (!automation.value) return

  isSaving.value = true
  try {
    const daysOfWeek = form.value.scheduleType === ScheduleType.FixedDate && form.value.scheduledDaysOfWeek
      ? form.value.scheduledDaysOfWeek
      : undefined

    const request: UpdateAutomationRequest = {
      scheduleType: isLowStockAction ? ScheduleType.Interval : form.value.scheduleType,
      intervalDays: isLowStockAction ? 1 : (form.value.scheduleType === ScheduleType.Interval ? form.value.intervalDays : undefined),
      scheduledDaysOfWeek: isLowStockAction ? undefined : daysOfWeek,
      scheduledDayOfMonth: isLowStockAction ? undefined : (form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfMonth : undefined),
      scheduledTime: isLowStockAction ? '00:00' : form.value.scheduledTime,
      actionType: form.value.actionType,
      consumeQuantity: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeQuantity : undefined,
      consumeUnit: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeUnit : undefined,
      addQuantity: isShoppingListAction ? form.value.addQuantity : undefined,
      addUnit: isShoppingListAction ? form.value.addUnit : undefined,
      shoppingListPublicId: isShoppingListAction ? form.value.shoppingListPublicId : undefined,
      productPublicId: isShoppingListAction ? form.value.productPublicId : undefined,
      thresholdQuantity: isLowStockAction ? form.value.thresholdQuantity : undefined
    }
    await updateAutomation(automation.value.publicId, request)

    toast.add({
      title: t('toast.success'),
      description: t('profile.automation.updateSuccess'),
      color: 'success'
    })
    isEditModalOpen.value = false
    await loadAutomation()
  } catch {
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.updateFailed'),
      color: 'error'
    })
  } finally {
    isSaving.value = false
  }
}

// Delete
function confirmDelete() {
  isDeleteModalOpen.value = true
}

async function handleDelete() {
  if (!automation.value) return

  isDeleting.value = true
  try {
    await deleteAutomation(automation.value.publicId)
    isDeleteModalOpen.value = false
    navigateTo('/profile/automation')
  } catch {
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.deleteFailed'),
      color: 'error'
    })
  } finally {
    isDeleting.value = false
  }
}

// Lifecycle
onMounted(async () => {
  await loadAutomation()
  if (!error.value) {
    await loadInitialHistory()
    await nextTick()
    setupObserver()
  }
})

onBeforeUnmount(() => {
  if (observer.value) {
    observer.value.disconnect()
  }
})

// Watch for sentinel appearing in DOM (after history loads)
watch(sentinelRef, (el) => {
  if (el && !observer.value) {
    setupObserver()
  }
})
</script>

<style scoped>
.animate-pulse-success {
  animation: pulse-success 0.5s ease-in-out 3;
}
.animate-pulse-error {
  animation: pulse-error 0.5s ease-in-out 3;
}

@keyframes pulse-success {
  0%, 100% { box-shadow: 0 0 0 0 rgba(34, 197, 94, 0); }
  50% { box-shadow: 0 0 16px 4px rgba(34, 197, 94, 0.35); }
}

@keyframes pulse-error {
  0%, 100% { box-shadow: 0 0 0 0 rgba(239, 68, 68, 0); }
  50% { box-shadow: 0 0 16px 4px rgba(239, 68, 68, 0.35); }
}
</style>
