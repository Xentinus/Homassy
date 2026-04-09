<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-4">
      <div class="flex items-center gap-3">
        <NuxtLink to="/profile/automation">
          <UButton
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
          />
        </NuxtLink>
        <UIcon name="i-lucide-timer" class="h-7 w-7 text-primary-500" />
        <h1 class="text-2xl font-semibold">{{ t('profile.automation.createAutomation') }}</h1>
      </div>

      <!-- Stepper -->
      <UStepper
        :model-value="currentStep"
        :items="stepperItems"
        orientation="horizontal"
        @update:model-value="handleStepChange"
      />
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-48 px-4 sm:px-8 lg:px-14 pb-6">

      <!-- Step 1: Action Type -->
      <div v-if="currentStep === 0" class="space-y-6">
        <p class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.create.selectActionType') }}</p>

        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <button
            v-for="option in actionTypeCards"
            :key="option.value"
            type="button"
            class="rounded-xl border-2 p-6 text-left transition-all hover:shadow-md"
            :class="form.actionType === option.value
              ? 'border-primary-500 bg-primary-50 dark:bg-primary-950'
              : 'border-gray-200 dark:border-gray-700 hover:border-primary-300'"
            @click="selectActionType(option.value)"
          >
            <UIcon :name="option.icon" class="h-8 w-8 mb-3" :class="option.iconColor" />
            <p class="font-semibold text-lg">{{ option.label }}</p>
            <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">{{ option.description }}</p>
          </button>
        </div>
      </div>

      <!-- Step 2: Product Selection (search + barcode cards) -->
      <div v-if="currentStep === 1" class="space-y-6">
        <p class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.create.selectProduct') }}</p>

        <!-- Search Bar + Barcode Scanner -->
        <UFieldGroup size="lg" orientation="horizontal" class="w-full">
          <UInput
            v-model="productSearchQuery"
            :placeholder="t('profile.automation.create.searchProductPlaceholder')"
            icon="i-lucide-search"
            size="lg"
            class="flex-1"
          />
          <BarcodeScannerButton v-if="showCameraButton" @scanned="handleBarcodeScanned" />
        </UFieldGroup>

        <!-- Loading State -->
        <div v-if="isSearchingProducts" class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          <USkeleton v-for="i in 6" :key="i" class="h-32 w-full rounded-lg" />
        </div>

        <!-- Empty / Start typing -->
        <div
          v-else-if="productSearchQuery.trim() === ''"
          class="text-center py-12 text-gray-500"
        >
          {{ t('profile.automation.create.startTypingProduct') }}
        </div>

        <!-- No Results -->
        <div
          v-else-if="productSearchResults.length === 0 && !isSearchingProducts"
          class="text-center py-12 text-gray-500"
        >
          {{ t('profile.automation.create.noProductResults') }}
        </div>

        <!-- Product Grid -->
        <div
          v-else
          class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4"
        >
          <SelectableProductCard
            v-for="product in productSearchResults"
            :key="product.publicId"
            :product="product"
            :search-query="productSearchQuery"
            :is-selected="form.productPublicId === product.publicId"
            @select="onProductSelected"
          />
        </div>

        <!-- Inventory Item Sub-selection (for AutoConsume / NotifyOnly) -->
        <template v-if="form.actionType !== AutomationActionType.AddToShoppingList && form.actionType !== AutomationActionType.LowStockAddToShoppingList && form.productPublicId">
          <div class="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-3">
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ t('profile.automation.create.selectInventoryItem') }}
            </p>

            <div v-if="loadingInventoryItems" class="space-y-2">
              <USkeleton class="h-12 w-full rounded-lg" />
              <USkeleton class="h-12 w-full rounded-lg" />
            </div>

            <div v-else-if="inventoryItemOptions.length === 0" class="text-sm text-gray-500">
              {{ t('profile.automation.create.noInventoryItems') }}
            </div>

            <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <button
                v-for="item in inventoryItemOptions"
                :key="item.value"
                type="button"
                class="rounded-lg border-2 p-3 text-left transition-all hover:shadow-sm"
                :class="form.inventoryItemPublicId === item.value
                  ? 'border-primary-500 bg-primary-50 dark:bg-primary-950'
                  : 'border-gray-200 dark:border-gray-700 hover:border-primary-300'"
                @click="selectInventoryItem(item.value)"
              >
                <p class="text-sm font-medium">{{ item.label }}</p>
              </button>
            </div>
          </div>
        </template>
      </div>

      <!-- Step 3: Shopping List Selection (for AddToShoppingList and LowStockAddToShoppingList) -->
      <div v-if="currentStep === shoppingListStepIndex" class="space-y-6">
        <p class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.create.selectShoppingList') }}</p>

        <!-- Loading State -->
        <div v-if="loadingShoppingLists" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          <USkeleton v-for="i in 3" :key="i" class="h-24 w-full rounded-lg" />
        </div>

        <!-- Empty State -->
        <div v-else-if="shoppingLists.length === 0" class="text-center py-12 text-gray-500">
          {{ t('profile.automation.create.noShoppingLists') }}
        </div>

        <!-- Shopping List Cards -->
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          <button
            v-for="list in shoppingLists"
            :key="list.publicId"
            type="button"
            class="group relative rounded-xl border-2 p-4 text-left transition-all duration-200 hover:shadow-lg hover:-translate-y-0.5"
            :class="form.shoppingListPublicId === list.publicId
              ? 'border-primary-500 bg-primary-50 dark:bg-primary-950 ring-2 ring-offset-2 ring-primary-500 dark:ring-offset-gray-900'
              : 'border-gray-200 dark:border-gray-700 hover:border-primary-300 bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900'"
            @click="selectShoppingList(list.publicId)"
          >
            <!-- Selection Indicator -->
            <div class="absolute top-3 right-3">
              <div
                class="w-5 h-5 rounded-full border-2 border-primary-500 flex items-center justify-center transition-all duration-200"
                :class="form.shoppingListPublicId === list.publicId ? 'bg-primary-500' : 'bg-white dark:bg-gray-800'"
              >
                <UIcon v-if="form.shoppingListPublicId === list.publicId" name="i-lucide-check" class="w-3 h-3 text-white" />
              </div>
            </div>

            <!-- Color Dot + Name -->
            <div class="flex items-center gap-3 pr-8">
              <div
                class="w-4 h-4 rounded-full flex-shrink-0"
                :style="{ backgroundColor: list.color || '#6366f1' }"
              />
              <h3 class="font-bold text-sm text-gray-900 dark:text-white truncate">{{ list.name }}</h3>
            </div>

            <!-- Description -->
            <p v-if="list.description" class="text-xs text-gray-500 dark:text-gray-400 mt-2 line-clamp-2 pl-7">
              {{ list.description }}
            </p>

            <!-- Shared badge -->
            <div v-if="list.isSharedWithFamily" class="flex items-center gap-1 mt-2 pl-7">
              <UIcon name="i-lucide-users" class="h-3 w-3 text-gray-400" />
              <span class="text-xs text-gray-400">{{ t('profile.automation.create.sharedWithFamily') }}</span>
            </div>
          </button>
        </div>
      </div>

      <!-- Step 4: Schedule Configuration (not for LowStock) -->
      <div v-if="currentStep === scheduleOrThresholdStepIndex && !isLowStock" class="space-y-6">
        <p class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.create.configureSchedule') }}</p>

        <div class="space-y-4 max-w-lg">
          <!-- Schedule Type -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.scheduleType') }} <span class="text-red-500">*</span>
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
          <div v-if="form.scheduleType === ScheduleType.Interval">
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.intervalDays') }} <span class="text-red-500">*</span>
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
          <div v-if="form.scheduleType === ScheduleType.FixedDate">
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.scheduledDaysOfWeek') }}
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
          <div v-if="form.scheduleType === ScheduleType.FixedDate">
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.scheduledDayOfMonth') }}
            </label>
            <UInput
              v-model.number="form.scheduledDayOfMonth"
              type="number"
              :min="1"
              :max="31"
              :placeholder="t('profile.automation.dayOfMonthPlaceholder')"
              class="w-full"
            />
          </div>

          <!-- Scheduled Time -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.scheduledTime') }} <span class="text-red-500">*</span>
            </label>
            <UInput
              v-model="form.scheduledTime"
              type="time"
              class="w-full"
            />
          </div>
        </div>

        <div class="flex justify-end pt-4">
          <UButton
            :label="t('common.next')"
            trailing-icon="i-lucide-arrow-right"
            :disabled="!isScheduleValid"
            @click="currentStep = confirmStepIndex"
          />
        </div>
      </div>

      <!-- Step 4b: Threshold Configuration (LowStock only) -->
      <div v-if="currentStep === scheduleOrThresholdStepIndex && isLowStock" class="space-y-6">
        <p class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.create.configureThreshold') }}</p>

        <div class="space-y-4 max-w-lg">
          <!-- Threshold Quantity -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.thresholdQuantity') }} <span class="text-red-500">*</span>
            </label>
            <p class="text-xs text-gray-500 dark:text-gray-400 mb-2">
              {{ t('profile.automation.create.thresholdDescription') }}
            </p>
            <UInput
              v-model.number="form.thresholdQuantity"
              type="number"
              :min="0.001"
              step="0.1"
              class="w-full"
            />
          </div>

          <!-- Add Quantity + Unit -->
          <div>
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.addQuantity') }} <span class="text-red-500">*</span>
            </label>
            <p class="text-xs text-gray-500 dark:text-gray-400 mb-2">
              {{ t('profile.automation.create.lowStockAddQuantityDescription') }}
            </p>
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
                :placeholder="t('common.unit')"
                class="w-32"
              />
            </div>
          </div>
        </div>

        <div class="flex justify-end pt-4">
          <UButton
            :label="t('common.next')"
            trailing-icon="i-lucide-arrow-right"
            :disabled="!form.thresholdQuantity || form.thresholdQuantity <= 0 || !form.addQuantity || form.addQuantity <= 0"
            @click="currentStep = confirmStepIndex"
          />
        </div>
      </div>

      <!-- Step 5: Details & Confirm -->
      <div v-if="currentStep === confirmStepIndex" class="space-y-6">
        <p class="text-gray-600 dark:text-gray-400">{{ t('profile.automation.create.reviewAndConfirm') }}</p>

        <div class="space-y-4 max-w-lg">
          <!-- Consume Quantity (for AutoConsume) -->
          <div v-if="form.actionType === AutomationActionType.AutoConsume">
            <label class="block text-sm font-medium mb-1">
              {{ t('common.quantity') }}
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
                :placeholder="t('common.unit')"
                class="w-32"
              />
            </div>
          </div>

          <!-- Add Quantity (for AddToShoppingList) -->
          <div v-if="form.actionType === AutomationActionType.AddToShoppingList">
            <label class="block text-sm font-medium mb-1">
              {{ t('profile.automation.addQuantity') }} <span class="text-red-500">*</span>
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
                :placeholder="t('common.unit')"
                class="w-32"
              />
            </div>
          </div>
          <div class="flex items-center gap-2">
            <UCheckbox
              v-model="form.isSharedWithFamily"
              :label="t('profile.automation.create.sharedWithFamily')"
            />
          </div>

          <!-- Summary -->
          <div class="rounded-lg border border-gray-200 dark:border-gray-700 p-4 space-y-2 mt-6">
            <h3 class="font-semibold text-lg mb-3">{{ t('profile.automation.create.summary') }}</h3>

            <div class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.actionType') }}</span>
              <span class="font-medium">{{ actionTypeLabel(form.actionType) }}</span>
            </div>

            <div class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.product') }}</span>
              <span class="font-medium">{{ selectedProductLabel }}</span>
            </div>

            <div v-if="form.actionType !== AutomationActionType.AddToShoppingList && form.actionType !== AutomationActionType.LowStockAddToShoppingList && selectedInventoryItemLabel" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.inventoryItem') }}</span>
              <span class="font-medium">{{ selectedInventoryItemLabel }}</span>
            </div>

            <div v-if="form.actionType === AutomationActionType.AddToShoppingList || form.actionType === AutomationActionType.LowStockAddToShoppingList" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.shoppingList') }}</span>
              <span class="font-medium">{{ selectedShoppingListLabel }}</span>
            </div>

            <div v-if="!isLowStock" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.scheduleType') }}</span>
              <span class="font-medium">{{ scheduleSummary }}</span>
            </div>

            <div v-if="!isLowStock" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.scheduledTime') }}</span>
              <span class="font-medium">{{ form.scheduledTime }}</span>
            </div>

            <div v-if="isLowStock && form.thresholdQuantity" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.thresholdQuantity') }}</span>
              <span class="font-medium">{{ form.thresholdQuantity }}</span>
            </div>

            <div v-if="form.actionType === AutomationActionType.AutoConsume && form.consumeQuantity" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('common.quantity') }}</span>
              <span class="font-medium">{{ form.consumeQuantity }} {{ form.consumeUnit !== undefined ? t(`enums.unit.${form.consumeUnit}`) : '' }}</span>
            </div>

            <div v-if="form.actionType === AutomationActionType.AddToShoppingList && form.addQuantity" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.addQuantity') }}</span>
              <span class="font-medium">{{ form.addQuantity }} {{ form.addUnit !== undefined ? t(`enums.unit.${form.addUnit}`) : '' }}</span>
            </div>

            <div v-if="form.actionType === AutomationActionType.LowStockAddToShoppingList && form.addQuantity" class="flex justify-between text-sm">
              <span class="text-gray-500 dark:text-gray-400">{{ t('profile.automation.addQuantity') }}</span>
              <span class="font-medium">{{ form.addQuantity }} {{ form.addUnit !== undefined ? t(`enums.unit.${form.addUnit}`) : '' }}</span>
            </div>
          </div>
        </div>

        <div class="flex justify-end pt-4">
          <UButton
            :label="t('profile.automation.create.createButton')"
            icon="i-lucide-check"
            :loading="isCreating"
            :disabled="!isConfirmValid"
            @click="handleCreate"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { watchDebounced } from '@vueuse/core'
import { useAutomationApi } from '~/composables/api/useAutomationApi'
import { useProductsApi } from '~/composables/api/useProductsApi'
import { useShoppingListApi } from '~/composables/api/useShoppingListApi'
import {
  ScheduleType,
  AutomationActionType,
  DaysOfWeek
} from '~/types/automation'
import type { CreateAutomationRequest } from '~/types/automation'
import type { ProductInfo, DetailedProductInfo } from '~/types/product'
import type { ShoppingListInfo } from '~/types/shoppingList'
import { Unit } from '~/types/enums'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { createAutomation } = useAutomationApi()
const { getProducts, getProductDetails } = useProductsApi()
const { getShoppingLists } = useShoppingListApi()
const { t } = useI18n()
const toast = useToast()
const { showCameraButton } = useCameraAvailability()

// Stepper state
const currentStep = ref(0)
const stepperItems = computed(() => {
  const steps = [
    { label: t('profile.automation.create.step1') },
    { label: t('profile.automation.create.step2') }
  ]
  if (form.value.actionType === AutomationActionType.AddToShoppingList || form.value.actionType === AutomationActionType.LowStockAddToShoppingList) {
    steps.push({ label: t('profile.automation.create.step3') })
  }
  if (form.value.actionType === AutomationActionType.LowStockAddToShoppingList) {
    steps.push({ label: t('profile.automation.create.stepThreshold') })
  } else {
    steps.push({ label: t('profile.automation.create.stepSchedule') })
  }
  steps.push(
    { label: t('profile.automation.create.stepConfirm') }
  )
  return steps
})

const handleStepChange = (newStep: string | number | undefined) => {
  if (typeof newStep === 'number' && newStep <= currentStep.value) {
    currentStep.value = newStep
  }
}

// Form state
const form = ref({
  actionType: AutomationActionType.AutoConsume as AutomationActionType,
  productPublicId: '' as string,
  inventoryItemPublicId: '' as string,
  shoppingListPublicId: '' as string,
  scheduleType: ScheduleType.Interval as ScheduleType,
  intervalDays: 7 as number | undefined,
  scheduledDaysOfWeek: 0 as number,
  scheduledDayOfMonth: undefined as number | undefined,
  scheduledTime: '07:00',
  consumeQuantity: undefined as number | undefined,
  consumeUnit: undefined as number | undefined,
  addQuantity: undefined as number | undefined,
  addUnit: undefined as number | undefined,
  thresholdQuantity: undefined as number | undefined,
  isSharedWithFamily: false
})

// Product search state
const productSearchQuery = ref('')
const productSearchResults = ref<ProductInfo[]>([])
const isSearchingProducts = ref(false)

// Inventory items (for AutoConsume / NotifyOnly after product selection)
const loadingInventoryItems = ref(false)
const inventoryItemOptions = ref<{ value: string; label: string }[]>([])

// Shopping lists state
const loadingShoppingLists = ref(false)
const shoppingLists = ref<ShoppingListInfo[]>([])

// Loading states
const isCreating = ref(false)

// Action type cards
const actionTypeCards = computed(() => [
  {
    value: AutomationActionType.AutoConsume,
    icon: 'i-lucide-zap',
    iconColor: 'text-blue-500',
    label: t('profile.automation.autoConsume'),
    description: t('profile.automation.create.autoConsumeDesc')
  },
  {
    value: AutomationActionType.NotifyOnly,
    icon: 'i-lucide-bell',
    iconColor: 'text-amber-500',
    label: t('profile.automation.notifyOnly'),
    description: t('profile.automation.create.notifyOnlyDesc')
  },
  {
    value: AutomationActionType.AddToShoppingList,
    icon: 'i-lucide-shopping-cart',
    iconColor: 'text-green-500',
    label: t('profile.automation.addToShoppingList'),
    description: t('profile.automation.create.addToShoppingListDesc')
  },
  {
    value: AutomationActionType.LowStockAddToShoppingList,
    icon: 'i-lucide-triangle-alert',
    iconColor: 'text-red-500',
    label: t('profile.automation.lowStockAddToShoppingList'),
    description: t('profile.automation.create.lowStockAddToShoppingListDesc')
  }
])

// Select options
const scheduleTypeOptions = computed(() => [
  { value: ScheduleType.Interval, label: t('profile.automation.interval') },
  { value: ScheduleType.FixedDate, label: t('profile.automation.fixedDate') }
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
    .map(([_key, value]) => ({
      value: value as number,
      label: t(`enums.unit.${value}`)
    }))
)

// Computed labels for summary
const selectedProductLabel = computed(() => {
  const product = productSearchResults.value.find((p: ProductInfo) => p.publicId === form.value.productPublicId)
  if (product) {
    return `${product.name}${product.brand ? ` (${product.brand})` : ''}`
  }
  return ''
})

const selectedInventoryItemLabel = computed(() => {
  const item = inventoryItemOptions.value.find((i: { value: string; label: string }) => i.value === form.value.inventoryItemPublicId)
  return item?.label || ''
})

const selectedShoppingListLabel = computed(() => {
  const list = shoppingLists.value.find((l: ShoppingListInfo) => l.publicId === form.value.shoppingListPublicId)
  return list?.name || ''
})

const scheduleSummary = computed(() => {
  if (form.value.scheduleType === ScheduleType.Interval) {
    return t('profile.automation.everyNDays', { n: form.value.intervalDays })
  }
  const parts: string[] = []
  if (form.value.scheduledDaysOfWeek) {
    parts.push(getSelectedDayNames(form.value.scheduledDaysOfWeek))
  }
  if (form.value.scheduledDayOfMonth) {
    parts.push(t('profile.automation.everyMonthDay', { day: form.value.scheduledDayOfMonth }))
  }
  return parts.join(' / ') || t('profile.automation.fixedDate')
})

// Validation
const isScheduleValid = computed(() => {
  if (!form.value.scheduledTime) return false
  if (form.value.scheduleType === ScheduleType.Interval) {
    return !!form.value.intervalDays && form.value.intervalDays > 0
  }
  return form.value.scheduledDaysOfWeek > 0 || !!form.value.scheduledDayOfMonth
})

const isConfirmValid = computed(() => {
  if (form.value.actionType === AutomationActionType.AddToShoppingList) {
    return !!form.value.addQuantity && form.value.addQuantity > 0
  }
  if (form.value.actionType === AutomationActionType.LowStockAddToShoppingList) {
    return !!form.value.thresholdQuantity && form.value.thresholdQuantity > 0
      && !!form.value.addQuantity && form.value.addQuantity > 0
  }
  return true
})

// Dynamic step indices
const hasShoppingListStep = computed(() =>
  form.value.actionType === AutomationActionType.AddToShoppingList
  || form.value.actionType === AutomationActionType.LowStockAddToShoppingList
)
const isLowStock = computed(() => form.value.actionType === AutomationActionType.LowStockAddToShoppingList)
const shoppingListStepIndex = computed(() => hasShoppingListStep.value ? 2 : -1)
const scheduleOrThresholdStepIndex = computed(() => hasShoppingListStep.value ? 3 : 2)
const confirmStepIndex = computed(() => hasShoppingListStep.value ? 4 : 3)

// Product search with debounce
watchDebounced(
  productSearchQuery,
  async (newQuery: string) => {
    if (newQuery.trim() === '') {
      productSearchResults.value = []
      return
    }

    isSearchingProducts.value = true
    try {
      const response = await getProducts({
        searchText: newQuery,
        pageNumber: 1,
        pageSize: 20
      })

      if (response.success && response.data) {
        productSearchResults.value = response.data.items.sort((a, b) =>
          a.name.toLowerCase().localeCompare(b.name.toLowerCase(), 'hu')
        )
      }
    } catch (error) {
      console.error('Product search failed:', error)
    } finally {
      isSearchingProducts.value = false
    }
  },
  { debounce: 300 }
)

// Barcode scanner handler
function handleBarcodeScanned(barcode: string) {
  productSearchQuery.value = barcode
}

// Product card click handler
async function onProductSelected(product: ProductInfo) {
  form.value.productPublicId = product.publicId
  form.value.inventoryItemPublicId = ''

  if (form.value.actionType === AutomationActionType.AddToShoppingList || form.value.actionType === AutomationActionType.LowStockAddToShoppingList) {
    // Auto-advance to shopping list step
    loadShoppingLists()
    currentStep.value = 2
  } else {
    // Load inventory items; auto-advance if only one
    await loadInventoryItemsForProduct(product.publicId)
  }
}

// Inventory item click → auto-advance to schedule
function selectInventoryItem(itemPublicId: string) {
  form.value.inventoryItemPublicId = itemPublicId
  currentStep.value = scheduleOrThresholdStepIndex.value
}

// Shopping list click → auto-advance to schedule/threshold
function selectShoppingList(listPublicId: string) {
  form.value.shoppingListPublicId = listPublicId
  currentStep.value = scheduleOrThresholdStepIndex.value
}

// Load inventory items for a specific product
async function loadInventoryItemsForProduct(productPublicId: string) {
  loadingInventoryItems.value = true
  try {
    const response = await getProductDetails(productPublicId)
    if (response.data) {
      const product = response.data as DetailedProductInfo
      inventoryItemOptions.value = (product.inventoryItems || []).map(item => ({
        value: item.publicId,
        label: `${item.currentQuantity} ${t(`enums.unit.${item.unit}`)}`
      }))
      // Auto-select and advance if only one inventory item
      if (inventoryItemOptions.value.length === 1 && inventoryItemOptions.value[0]) {
        form.value.inventoryItemPublicId = inventoryItemOptions.value[0].value
        currentStep.value = scheduleOrThresholdStepIndex.value
      }
    }
  } catch (error) {
    console.error('Failed to load inventory items:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.create.loadFailed'),
      color: 'error'
    })
  } finally {
    loadingInventoryItems.value = false
  }
}

// Load shopping lists
async function loadShoppingLists() {
  loadingShoppingLists.value = true
  try {
    const response = await getShoppingLists({ returnAll: true })
    if (response.data?.items) {
      shoppingLists.value = response.data.items
    }
  } catch (error) {
    console.error('Failed to load shopping lists:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.create.loadFailed'),
      color: 'error'
    })
  } finally {
    loadingShoppingLists.value = false
  }
}

// Days of week helpers
function isDaySelected(dayFlag: number): boolean {
  return (form.value.scheduledDaysOfWeek & dayFlag) !== 0
}

function toggleDay(dayFlag: number) {
  form.value.scheduledDaysOfWeek ^= dayFlag
}

function getSelectedDayNames(flags: number): string {
  const dayMap: { flag: number; key: string }[] = [
    { flag: DaysOfWeek.Monday, key: 'monday' },
    { flag: DaysOfWeek.Tuesday, key: 'tuesday' },
    { flag: DaysOfWeek.Wednesday, key: 'wednesday' },
    { flag: DaysOfWeek.Thursday, key: 'thursday' },
    { flag: DaysOfWeek.Friday, key: 'friday' },
    { flag: DaysOfWeek.Saturday, key: 'saturday' },
    { flag: DaysOfWeek.Sunday, key: 'sunday' }
  ]
  const selected = dayMap
    .filter(d => (flags & d.flag) !== 0)
    .map(d => t(`profile.automation.daysShort.${d.key}`))
  return selected.join(', ')
}

function actionTypeLabel(actionType: AutomationActionType): string {
  switch (actionType) {
    case AutomationActionType.AutoConsume: return t('profile.automation.autoConsume')
    case AutomationActionType.NotifyOnly: return t('profile.automation.notifyOnly')
    case AutomationActionType.AddToShoppingList: return t('profile.automation.addToShoppingList')
    case AutomationActionType.LowStockAddToShoppingList: return t('profile.automation.lowStockAddToShoppingList')
    default: return ''
  }
}

// Action type selection → advance to step 2
function selectActionType(actionType: AutomationActionType) {
  form.value.actionType = actionType
  // Reset selections when action type changes
  form.value.productPublicId = ''
  form.value.inventoryItemPublicId = ''
  form.value.shoppingListPublicId = ''
  form.value.thresholdQuantity = undefined
  inventoryItemOptions.value = []
  productSearchQuery.value = ''
  productSearchResults.value = []

  currentStep.value = 1
}

// Create automation
async function handleCreate() {
  if (form.value.actionType === AutomationActionType.AddToShoppingList) {
    if (!form.value.addQuantity || form.value.addQuantity <= 0) {
      toast.add({
        title: t('toast.error'),
        description: t('profile.automation.addQuantityRequired'),
        color: 'error'
      })
      return
    }
  }

  const isLowStockType = form.value.actionType === AutomationActionType.LowStockAddToShoppingList
  const needsProduct = form.value.actionType === AutomationActionType.AddToShoppingList || isLowStockType
  const needsSchedule = !isLowStockType

  isCreating.value = true
  try {
    const daysOfWeek = needsSchedule && form.value.scheduleType === ScheduleType.FixedDate && form.value.scheduledDaysOfWeek
      ? form.value.scheduledDaysOfWeek
      : undefined

    const request: CreateAutomationRequest = {
      inventoryItemPublicId: !needsProduct ? form.value.inventoryItemPublicId : undefined,
      productPublicId: needsProduct ? form.value.productPublicId : undefined,
      shoppingListPublicId: needsProduct ? form.value.shoppingListPublicId : undefined,
      scheduleType: needsSchedule ? form.value.scheduleType : ScheduleType.Interval,
      intervalDays: needsSchedule && form.value.scheduleType === ScheduleType.Interval ? form.value.intervalDays : (isLowStockType ? 1 : undefined),
      scheduledDaysOfWeek: daysOfWeek,
      scheduledDayOfMonth: needsSchedule && form.value.scheduleType === ScheduleType.FixedDate ? form.value.scheduledDayOfMonth : undefined,
      scheduledTime: needsSchedule ? form.value.scheduledTime : '00:00',
      actionType: form.value.actionType,
      consumeQuantity: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeQuantity : undefined,
      consumeUnit: form.value.actionType === AutomationActionType.AutoConsume ? form.value.consumeUnit : undefined,
      addQuantity: needsProduct ? form.value.addQuantity : undefined,
      addUnit: needsProduct ? form.value.addUnit : undefined,
      thresholdQuantity: isLowStockType ? form.value.thresholdQuantity : undefined,
      isSharedWithFamily: form.value.isSharedWithFamily
    }

    await createAutomation(request)

    toast.add({
      title: t('toast.success'),
      description: t('profile.automation.create.createSuccess'),
      color: 'success'
    })

    await navigateTo('/profile/automation')
  } catch (error) {
    console.error('Failed to create automation:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.automation.createFailed'),
      color: 'error'
    })
  } finally {
    isCreating.value = false
  }
}
</script>
