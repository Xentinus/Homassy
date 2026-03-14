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
        <UButton
          color="primary"
          size="sm"
          trailing-icon="i-lucide-plus"
          @click="openCreateModal"
        >
          {{ $t('common.add') }}
        </UButton>
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
      <div v-else class="space-y-4">
        <div
          v-for="automation in automations"
          :key="automation.publicId"
          class="rounded-lg border border-gray-200 dark:border-gray-700 p-4 space-y-3"
        >
          <!-- Header Row -->
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-3 min-w-0">
              <UIcon
                :name="automation.actionType === AutomationActionType.AutoConsume ? 'i-lucide-zap' : 'i-lucide-bell'"
                class="text-lg flex-shrink-0"
                :class="automation.isEnabled ? 'text-primary' : 'text-gray-400'"
              />
              <div class="min-w-0">
                <p class="font-semibold truncate">{{ automation.productName }}</p>
                <p v-if="automation.productBrand" class="text-xs text-gray-500 dark:text-gray-400 truncate">{{ automation.productBrand }}</p>
              </div>
            </div>
            <div class="flex items-center gap-2 flex-shrink-0">
              <USwitch
                :model-value="automation.isEnabled"
                @update:model-value="(val: boolean) => toggleEnabled(automation, val)"
                :disabled="isToggling === automation.publicId"
                :loading="isToggling === automation.publicId"
              />
              <UButton
                icon="i-lucide-pencil"
                color="neutral"
                variant="ghost"
                size="xs"
                @click="openEditModal(automation)"
              />
              <UButton
                icon="i-lucide-trash-2"
                color="error"
                variant="ghost"
                size="xs"
                @click="confirmDelete(automation)"
              />
            </div>
          </div>

          <!-- Schedule Summary -->
          <div class="flex flex-wrap gap-2 text-xs text-gray-600 dark:text-gray-400">
            <span class="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-gray-100 dark:bg-gray-800">
              <UIcon name="i-lucide-calendar" class="text-xs" />
              {{ formatSchedule(automation) }}
            </span>
            <span class="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-gray-100 dark:bg-gray-800">
              <UIcon name="i-lucide-clock" class="text-xs" />
              {{ automation.scheduledTime }}
            </span>
            <span class="inline-flex items-center gap-1 px-2 py-1 rounded-full" :class="automation.actionType === AutomationActionType.AutoConsume ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300' : 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300'">
              <UIcon :name="automation.actionType === AutomationActionType.AutoConsume ? 'i-lucide-zap' : 'i-lucide-bell'" class="text-xs" />
              {{ automation.actionType === AutomationActionType.AutoConsume ? $t('profile.automation.autoConsume') : $t('profile.automation.notifyOnly') }}
            </span>
            <span v-if="automation.consumeQuantity" class="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-gray-100 dark:bg-gray-800">
              {{ automation.consumeQuantity }} {{ automation.consumeUnit !== undefined ? $t(`enums.unit.${automation.consumeUnit}`) : '' }}
            </span>
          </div>

          <!-- Next Execution & Actions -->
          <div class="flex items-center justify-between text-xs">
            <div v-if="automation.nextExecutionAt" class="text-gray-500 dark:text-gray-400">
              {{ $t('profile.automation.nextExecution') }}: {{ formatDateTime(automation.nextExecutionAt) }}
            </div>
            <div class="flex items-center gap-2">
              <UButton
                size="xs"
                variant="soft"
                icon="i-lucide-play"
                :label="$t('profile.automation.executeNow')"
                @click="handleExecute(automation)"
                :loading="isExecuting === automation.publicId"
                :disabled="isExecuting === automation.publicId"
              />
              <UButton
                size="xs"
                variant="ghost"
                color="neutral"
                icon="i-lucide-history"
                :label="$t('profile.automation.history')"
                @click="openHistory(automation)"
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Create/Edit Modal -->
  <UModal :open="isModalOpen" @update:open="(val: boolean) => isModalOpen = val" :dismissible="false">
    <template #title>
      {{ isEditing ? $t('profile.automation.editAutomation') : $t('profile.automation.createAutomation') }}
    </template>

    <template #description>
      {{ isEditing ? $t('profile.automation.editDescription') : $t('profile.automation.createDescription') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Inventory Item Selector (only for create) -->
        <div v-if="!isEditing">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.inventoryItem') }} <span class="text-red-500">*</span>
          </label>
          <USelect
            v-model="form.inventoryItemPublicId"
            :items="inventoryItemOptions"
            value-key="value"
            label-key="label"
            :placeholder="$t('profile.automation.selectItem')"
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

        <!-- Day of Week (for FixedDate type) -->
        <div v-if="form.scheduleType === ScheduleType.FixedDate">
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.automation.scheduledDayOfWeek') }}
          </label>
          <USelect
            v-model="form.scheduledDayOfWeek"
            :items="dayOfWeekOptions"
            value-key="value"
            label-key="label"
            :placeholder="$t('profile.automation.selectDayOfWeek')"
            class="w-full"
          />
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

        <!-- Shared with Family (only for create) -->
        <div v-if="!isEditing" class="flex items-center gap-2">
          <UCheckbox
            v-model="form.isSharedWithFamily"
            :label="$t('common.family')"
          />
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
import {
  ScheduleType,
  AutomationActionType,
  AutomationExecutionStatus
} from '~/types/automation'
import type {
  AutomationResponse,
  AutomationExecutionResponse,
  CreateAutomationRequest,
  UpdateAutomationRequest
} from '~/types/automation'
import { Unit } from '~/types/enums'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const {
  getAutomations,
  createAutomation,
  updateAutomation,
  deleteAutomation,
  executeAutomation,
  getExecutionHistory
} = useAutomationApi()
const { getDetailedProducts } = useProductsApi()
const { t } = useI18n()
const toast = useToast()

// State
const loading = ref(true)
const automations = ref<AutomationResponse[]>([])
const isToggling = ref<string | null>(null)
const isExecuting = ref<string | null>(null)

// Modal state
const isModalOpen = ref(false)
const isEditing = ref(false)
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

// Inventory items for selector
const inventoryItemOptions = ref<{ value: string; label: string }[]>([])

// Form
const defaultForm = () => ({
  inventoryItemPublicId: '' as string,
  scheduleType: ScheduleType.Interval as ScheduleType,
  intervalDays: 7 as number | undefined,
  scheduledDayOfWeek: undefined as number | undefined,
  scheduledDayOfMonth: undefined as number | undefined,
  scheduledTime: '07:00',
  actionType: AutomationActionType.AutoConsume as AutomationActionType,
  consumeQuantity: undefined as number | undefined,
  consumeUnit: undefined as number | undefined,
  isSharedWithFamily: false
})
const form = ref(defaultForm())

// Select options
const scheduleTypeOptions = computed(() => [
  { value: ScheduleType.Interval, label: t('profile.automation.interval') },
  { value: ScheduleType.FixedDate, label: t('profile.automation.fixedDate') }
])

const actionTypeOptions = computed(() => [
  { value: AutomationActionType.AutoConsume, label: t('profile.automation.autoConsume') },
  { value: AutomationActionType.NotifyOnly, label: t('profile.automation.notifyOnly') }
])

const dayOfWeekOptions = computed(() => [
  { value: 0, label: t('profile.automation.days.sunday') },
  { value: 1, label: t('profile.automation.days.monday') },
  { value: 2, label: t('profile.automation.days.tuesday') },
  { value: 3, label: t('profile.automation.days.wednesday') },
  { value: 4, label: t('profile.automation.days.thursday') },
  { value: 5, label: t('profile.automation.days.friday') },
  { value: 6, label: t('profile.automation.days.saturday') }
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

// Load inventory items for create form
async function loadInventoryItems() {
  try {
    const response = await getDetailedProducts({ returnAll: true })
    const items: { value: string; label: string }[] = []
    if (response.data?.items) {
      for (const product of response.data.items) {
        for (const item of product.inventoryItems || []) {
          items.push({
            value: item.publicId,
            label: `${product.name}${product.brand ? ` (${product.brand})` : ''} — ${item.currentQuantity} ${t(`enums.unit.${item.unit}`)}`
          })
        }
      }
    }
    inventoryItemOptions.value = items
  } catch (error) {
    console.error('Failed to load inventory items:', error)
  }
}

// Format schedule for display
function formatSchedule(automation: AutomationResponse): string {
  if (automation.scheduleType === ScheduleType.Interval) {
    return t('profile.automation.everyNDays', { n: automation.intervalDays })
  }
  if (automation.scheduledDayOfWeek !== undefined && automation.scheduledDayOfWeek !== null) {
    const dayKey = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'][automation.scheduledDayOfWeek]
    return t('profile.automation.everyWeekday', { day: t(`profile.automation.days.${dayKey}`) })
  }
  if (automation.scheduledDayOfMonth) {
    return t('profile.automation.everyMonthDay', { day: automation.scheduledDayOfMonth })
  }
  return ''
}

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
    [AutomationExecutionStatus.Failed]: t('profile.automation.status.failed')
  }
  return statusMap[status] || ''
}

function executionStatusColor(status: AutomationExecutionStatus): string {
  switch (status) {
    case AutomationExecutionStatus.AutoConsumed:
    case AutomationExecutionStatus.ManuallyConfirmed:
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
async function handleExecute(automation: AutomationResponse) {
  isExecuting.value = automation.publicId
  try {
    await executeAutomation(automation.publicId, {})
    toast.add({
      title: t('toast.success'),
      description: t('profile.automation.executeSuccess'),
      color: 'success'
    })
    await loadAutomations()
  } catch (error) {
    console.error('Failed to execute automation:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.executeFailed'),
      color: 'error'
    })
  } finally {
    isExecuting.value = null
  }
}

// Create modal
async function openCreateModal() {
  form.value = defaultForm()
  isEditing.value = false
  editingPublicId.value = null
  isModalOpen.value = true
  await loadInventoryItems()
}

// Edit modal
async function openEditModal(automation: AutomationResponse) {
  isEditing.value = true
  editingPublicId.value = automation.publicId
  form.value = {
    inventoryItemPublicId: automation.inventoryItemPublicId,
    scheduleType: automation.scheduleType,
    intervalDays: automation.intervalDays,
    scheduledDayOfWeek: automation.scheduledDayOfWeek,
    scheduledDayOfMonth: automation.scheduledDayOfMonth,
    scheduledTime: automation.scheduledTime,
    actionType: automation.actionType,
    consumeQuantity: automation.consumeQuantity,
    consumeUnit: automation.consumeUnit,
    isSharedWithFamily: false
  }
  isModalOpen.value = true
}

function closeModal() {
  isModalOpen.value = false
}

async function handleSave() {
  if (!isEditing.value && !form.value.inventoryItemPublicId) {
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.itemRequired'),
      color: 'error'
    })
    return
  }

  if (!form.value.scheduledTime) {
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.timeRequired'),
      color: 'error'
    })
    return
  }

  isSaving.value = true
  try {
    if (isEditing.value && editingPublicId.value) {
      const request: UpdateAutomationRequest = {
        scheduleType: form.value.scheduleType,
        intervalDays: form.value.scheduleType === ScheduleType.Interval ? form.value.intervalDays : undefined,
        scheduledDayOfWeek: form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfWeek : undefined,
        scheduledDayOfMonth: form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfMonth : undefined,
        scheduledTime: form.value.scheduledTime,
        actionType: form.value.actionType,
        consumeQuantity: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeQuantity : undefined,
        consumeUnit: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeUnit : undefined
      }
      await updateAutomation(editingPublicId.value, request)
    } else {
      const request: CreateAutomationRequest = {
        inventoryItemPublicId: form.value.inventoryItemPublicId,
        scheduleType: form.value.scheduleType,
        intervalDays: form.value.scheduleType === ScheduleType.Interval ? form.value.intervalDays : undefined,
        scheduledDayOfWeek: form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfWeek : undefined,
        scheduledDayOfMonth: form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfMonth : undefined,
        scheduledTime: form.value.scheduledTime,
        actionType: form.value.actionType,
        consumeQuantity: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeQuantity : undefined,
        consumeUnit: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeUnit : undefined,
        isSharedWithFamily: form.value.isSharedWithFamily
      }
      await createAutomation(request)
    }

    closeModal()
    await loadAutomations()
  } catch (error) {
    console.error('Failed to save automation:', error)
    toast.add({
      title: t('toast.error'),
      description: isEditing.value ? t('profile.automation.updateFailed') : t('profile.automation.createFailed'),
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
