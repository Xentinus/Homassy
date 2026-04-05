<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <NuxtLink to="/profile">
            <UButton
              icon="i-lucide-arrow-left"
              color="neutral"
              variant="ghost"
            />
          </NuxtLink>
          <UIcon name="i-lucide-timer" class="h-7 w-7 text-primary-500" />
          <h1 class="text-2xl font-semibold">{{ $t('profile.automation.title') }}</h1>
        </div>
        <NuxtLink to="/profile/automation/create">
          <UButton
            color="primary"
            size="sm"
            trailing-icon="i-lucide-plus"
          >
            {{ $t('common.add') }}
          </UButton>
        </NuxtLink>
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-28 px-4 sm:px-8 lg:px-14 pb-6">

      <!-- Loading State -->
      <template v-if="loading">
        <div class="space-y-4">
          <USkeleton v-for="i in 4" :key="i" class="h-28 w-full rounded-lg" />
        </div>
      </template>

      <!-- Empty State -->
      <div v-else-if="automations.length === 0" class="rounded-lg p-12 text-center">
        <UIcon name="i-lucide-timer" class="h-16 w-16 text-gray-400 mx-auto mb-4" />
        <p class="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">
          {{ $t('profile.automation.noAutomations') }}
        </p>
        <p class="text-gray-600 dark:text-gray-400">
          {{ $t('profile.automation.addFirstAutomation') }}
        </p>
      </div>

      <!-- Automation Rules List -->
      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <AutomationRuleCard
          v-for="automation in automations"
          :key="automation.publicId"
          :automation="automation"
          :toggling="isToggling === automation.publicId"
          :executing="isExecuting === automation.publicId"
          :pulse-state="pulseStates[automation.publicId]"
          @toggle="(val: boolean) => toggleEnabled(automation, val)"
          @edit="openEditModal(automation)"
          @delete="confirmDelete(automation)"
          @execute="handleExecute(automation)"
          @history="openHistory(automation)"
        />
      </div>
    </div>
  </div>

  <!-- Edit Modal -->
  <UModal :open="isModalOpen" @update:open="(val: boolean) => isModalOpen = val" :dismissible="false">
    <template #title>
      {{ $t('profile.automation.editAutomation') }}
    </template>

    <template #description>
      {{ $t('profile.automation.editDescription') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Action Type (moved to top to control conditional fields) -->
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

        <!-- Shopping List Selector (for AddToShoppingList) -->
        <div v-if="form.actionType === AutomationActionType.AddToShoppingList">
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

        <!-- Product Selector (for AddToShoppingList) -->
        <div v-if="form.actionType === AutomationActionType.AddToShoppingList">
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

        <!-- Schedule Type -->
        <div>
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

        <!-- Interval Days (for Interval type) -->
        <div v-if="form.scheduleType === ScheduleType.Interval">
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

        <!-- Days of Week multi-select (for FixedDate type) -->
        <div v-if="form.scheduleType === ScheduleType.FixedDate">
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

        <!-- Day of Month (for FixedDate type) -->
        <div v-if="form.scheduleType === ScheduleType.FixedDate">
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
        <div>
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.scheduledTime') }} <span class="text-red-500">*</span>
          </label>
          <UInput
            v-model="form.scheduledTime"
            type="time"
            class="w-full"
          />
        </div>

        <!-- Consume Quantity (for AutoConsume) -->
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

        <!-- Add Quantity (for AddToShoppingList) -->
        <div v-if="form.actionType === AutomationActionType.AddToShoppingList">
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
          @click="closeModal"
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

  <!-- Execution History Modal -->
  <UModal :open="isHistoryModalOpen" @update:open="(val: boolean) => isHistoryModalOpen = val">
    <template #title>
      {{ $t('profile.automation.executionHistory') }}
    </template>

    <template #description>
      {{ historyAutomation?.productName }}
    </template>

    <template #body>
      <div v-if="loadingHistory" class="space-y-3">
        <USkeleton v-for="i in 3" :key="i" class="h-16 w-full rounded-lg" />
      </div>
      <div v-else-if="executionHistory.length === 0" class="text-center py-6 text-gray-500 dark:text-gray-400">
        {{ $t('profile.automation.noHistory') }}
      </div>
      <div v-else class="space-y-3 max-h-80 overflow-y-auto">
        <div
          v-for="execution in executionHistory"
          :key="execution.publicId"
          class="rounded-lg border border-gray-200 dark:border-gray-700 p-3 space-y-1"
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
            {{ $t('common.quantity') }}: {{ execution.consumedQuantity }}
          </div>
          <div v-if="execution.notes" class="text-xs text-gray-500 dark:text-gray-400 italic">
            {{ execution.notes }}
          </div>
        </div>
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

const {
  getAutomations,
  updateAutomation,
  deleteAutomation,
  executeAutomation,
  getExecutionHistory
} = useAutomationApi()
const { getProducts } = useProductsApi()
const { getShoppingLists } = useShoppingListApi()
const { t, te } = useI18n()
const toast = useToast()

// State
const loading = ref(true)
const automations = ref<AutomationResponse[]>([])
const isToggling = ref<string | null>(null)
const isExecuting = ref<string | null>(null)
const pulseStates = ref<Record<string, 'success' | 'error' | undefined>>({})

// Modal state
const isModalOpen = ref(false)
const editingPublicId = ref<string | null>(null)
const isSaving = ref(false)

// Delete modal state
const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)
const deletingAutomation = ref<AutomationResponse | null>(null)

// History modal state
const isHistoryModalOpen = ref(false)
const loadingHistory = ref(false)
const historyAutomation = ref<AutomationResponse | null>(null)
const executionHistory = ref<AutomationExecutionResponse[]>([])

// Shopping lists for selector
const shoppingListOptions = ref<{ value: string; label: string }[]>([])

// Products for selector
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
  addUnit: undefined as number | undefined
})
const form = ref(defaultForm())

// Select options
const scheduleTypeOptions = computed(() => [
  { value: ScheduleType.Interval, label: t('profile.automation.interval') },
  { value: ScheduleType.FixedDate, label: t('profile.automation.fixedDate') }
])

const actionTypeOptions = computed(() => [
  { value: AutomationActionType.AutoConsume, label: t('profile.automation.autoConsume') },
  { value: AutomationActionType.NotifyOnly, label: t('profile.automation.notifyOnly') },
  { value: AutomationActionType.AddToShoppingList, label: t('profile.automation.addToShoppingList') }
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
    .filter(([key]) => isNaN(Number(key)))
    .map(([key, value]) => ({
      value: value as number,
      label: t(`enums.unit.${value}`)
    }))
)

// Load automations
async function loadAutomations() {
  loading.value = true
  try {
    const response = await getAutomations()
    automations.value = response.data || []
  } catch (error) {
    console.error('Failed to load automations:', error)
  } finally {
    loading.value = false
  }
}

// Load shopping lists for selector
async function loadShoppingLists() {
  try {
    const response = await getShoppingLists({ returnAll: true })
    if (response.data?.items) {
      shoppingListOptions.value = response.data.items.map(list => ({
        value: list.publicId,
        label: list.name
      }))
    }
  } catch (error) {
    console.error('Failed to load shopping lists:', error)
  }
}

// Load products for selector
async function loadProducts() {
  try {
    const response = await getProducts({ returnAll: true })
    if (response.data?.items) {
      productOptions.value = response.data.items.map(product => ({
        value: product.publicId,
        label: `${product.name}${product.brand ? ` (${product.brand})` : ''}`
      }))
    }
  } catch (error) {
    console.error('Failed to load products:', error)
  }
}

// Days of week helpers
function isDaySelected(dayFlag: number): boolean {
  return (form.value.scheduledDaysOfWeek & dayFlag) !== 0
}

function toggleDay(dayFlag: number) {
  form.value.scheduledDaysOfWeek ^= dayFlag
}

// (actionTypeIcon, actionTypeLabel, actionTypeBadgeClass, formatSchedule moved into AutomationRuleCard)

function formatDateTime(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleString()
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

// Toggle enabled/disabled
async function toggleEnabled(automation: AutomationResponse, enabled: boolean) {
  isToggling.value = automation.publicId
  try {
    await updateAutomation(automation.publicId, { isEnabled: enabled })
    automation.isEnabled = enabled
  } catch (error) {
    console.error('Failed to toggle automation:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.updateFailed'),
      color: 'error'
    })
  } finally {
    isToggling.value = null
  }
}

// Manual execute
function playFeedbackSound(success: boolean) {
  try {
    const ctx = new AudioContext()
    const now = ctx.currentTime

    if (success) {
      // Three-note ascending chime (C6 → E6 → G6)
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
      // Low buzz for error
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

async function silentReloadAutomations() {
  try {
    const response = await getAutomations()
    automations.value = response.data || []
  } catch { /* silent */ }
}

async function handleExecute(automation: AutomationResponse) {
  isExecuting.value = automation.publicId
  try {
    const response = await executeAutomation(automation.publicId, {})
    if (response.success) {
      pulseStates.value[automation.publicId] = 'success'
      playFeedbackSound(true)
      await silentReloadAutomations()
    } else {
      pulseStates.value[automation.publicId] = 'error'
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
  } catch (error) {
    console.error('Failed to execute automation:', error)
    pulseStates.value[automation.publicId] = 'error'
    playFeedbackSound(false)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.executeErrors.unknown'),
      color: 'error'
    })
  } finally {
    isExecuting.value = null
    const id = automation.publicId
    setTimeout(() => {
      pulseStates.value[id] = undefined
    }, 1500)
  }
}

// Edit modal
async function openEditModal(automation: AutomationResponse) {
  editingPublicId.value = automation.publicId
  form.value = {
    productPublicId: automation.productPublicId || '',
    shoppingListPublicId: automation.shoppingListPublicId || '',
    scheduleType: automation.scheduleType,
    intervalDays: automation.intervalDays,
    scheduledDaysOfWeek: automation.scheduledDaysOfWeek || 0,
    scheduledDayOfMonth: automation.scheduledDayOfMonth,
    scheduledTime: automation.scheduledTime,
    actionType: automation.actionType,
    consumeQuantity: automation.consumeQuantity,
    consumeUnit: automation.consumeUnit,
    addQuantity: automation.addQuantity,
    addUnit: automation.addUnit
  }
  isModalOpen.value = true
  if (automation.actionType === AutomationActionType.AddToShoppingList) {
    await Promise.all([loadShoppingLists(), loadProducts()])
  }
}

function closeModal() {
  isModalOpen.value = false
}

async function handleSave() {
  // Validate for AddToShoppingList
  if (form.value.actionType === AutomationActionType.AddToShoppingList) {
    if (!form.value.shoppingListPublicId) {
      toast.add({
        title: t('toast.error'),
        description: t('profile.automation.shoppingListRequired'),
        color: 'error'
      })
      return
    }
    if (!form.value.productPublicId) {
      toast.add({
        title: t('toast.error'),
        description: t('profile.automation.productRequired'),
        color: 'error'
      })
      return
    }
    if (!form.value.addQuantity || form.value.addQuantity <= 0) {
      toast.add({
        title: t('toast.error'),
        description: t('profile.automation.addQuantityRequired'),
        color: 'error'
      })
      return
    }
  }

  if (!form.value.scheduledTime) {
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.timeRequired'),
      color: 'error'
    })
    return
  }

  if (!editingPublicId.value) return

  isSaving.value = true
  try {
    const daysOfWeek = form.value.scheduleType === ScheduleType.FixedDate && form.value.scheduledDaysOfWeek
      ? form.value.scheduledDaysOfWeek
      : undefined

    const request: UpdateAutomationRequest = {
      scheduleType: form.value.scheduleType,
      intervalDays: form.value.scheduleType === ScheduleType.Interval ? form.value.intervalDays : undefined,
      scheduledDaysOfWeek: daysOfWeek,
      scheduledDayOfMonth: form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfMonth : undefined,
      scheduledTime: form.value.scheduledTime,
      actionType: form.value.actionType,
      consumeQuantity: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeQuantity : undefined,
      consumeUnit: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeUnit : undefined,
      addQuantity: form.value.actionType === AutomationActionType.AddToShoppingList ? form.value.addQuantity : undefined,
      addUnit: form.value.actionType === AutomationActionType.AddToShoppingList ? form.value.addUnit : undefined,
      shoppingListPublicId: form.value.actionType === AutomationActionType.AddToShoppingList ? form.value.shoppingListPublicId : undefined,
      productPublicId: form.value.actionType === AutomationActionType.AddToShoppingList ? form.value.productPublicId : undefined
    }
    await updateAutomation(editingPublicId.value, request)

    toast.add({
      title: t('toast.success'),
      description: t('profile.automation.updateSuccess'),
      color: 'success'
    })
    closeModal()
    await loadAutomations()
  } catch (error) {
    console.error('Failed to save automation:', error)
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
function confirmDelete(automation: AutomationResponse) {
  deletingAutomation.value = automation
  isDeleteModalOpen.value = true
}

async function handleDelete() {
  if (!deletingAutomation.value) return

  isDeleting.value = true
  try {
    await deleteAutomation(deletingAutomation.value.publicId)
    isDeleteModalOpen.value = false
    await loadAutomations()
  } catch (error) {
    console.error('Failed to delete automation:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.deleteFailed'),
      color: 'error'
    })
  } finally {
    isDeleting.value = false
  }
}

// History
async function openHistory(automation: AutomationResponse) {
  historyAutomation.value = automation
  executionHistory.value = []
  isHistoryModalOpen.value = true
  loadingHistory.value = true

  try {
    const response = await getExecutionHistory(automation.publicId)
    executionHistory.value = response.data || []
  } catch (error) {
    console.error('Failed to load execution history:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.historyFailed'),
      color: 'error'
    })
  } finally {
    loadingHistory.value = false
  }
}

onMounted(() => {
  loadAutomations()
})
</script>
