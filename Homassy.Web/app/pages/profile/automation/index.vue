<template>
  <div>
    <!-- Fixed Header with search + filters -->
    <ListFilterBar
      v-model:search="searchQuery"
      :title="$t('profile.automation.title')"
      icon="i-lucide-timer"
      back-to="/profile/data"
      :search-placeholder="$t('profile.automation.searchPlaceholder')"
      :active-filters="activeFilters"
      :filter-count="activeFilterCount"
      :result-count="filteredAutomations.length"
      @clear-all="clearAllFilters"
    >
      <template #filters>
        <FilterChipGroup
          v-model="filterType"
          :label="$t('profile.automation.actionType')"
          :options="typeFilterOptions"
        />
        <FilterChipGroup
          v-model="filterStatus"
          :label="$t('profile.automation.filterLabels.status')"
          :options="statusFilterOptions"
        />
        <FilterChipGroup
          v-model="filterScheduleType"
          :label="$t('profile.automation.scheduleType')"
          :options="scheduleTypeFilterOptions"
        />

        <!-- Boolean property toggles -->
        <div role="group" :aria-label="$t('profile.automation.filterLabels.properties')">
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
            {{ $t('profile.automation.filterLabels.properties') }}
          </p>
          <div class="flex flex-wrap gap-2">
            <UButton
              :label="$t('profile.automation.triggered')"
              icon="i-lucide-bell-ring"
              size="sm"
              class="rounded-full"
              :color="triggeredFilter ? 'primary' : 'neutral'"
              :variant="triggeredFilter ? 'solid' : 'outline'"
              :aria-pressed="triggeredFilter"
              @click="triggeredFilter = !triggeredFilter"
            />
          </div>
        </div>
      </template>
    </ListFilterBar>

    <!-- Content Section -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6">

      <!-- Loading State -->
      <template v-if="loading">
        <div class="space-y-4">
          <USkeleton v-for="i in 4" :key="i" class="h-20 w-full rounded-lg" />
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

      <!-- No filter results -->
      <div v-else-if="filteredAutomations.length === 0" class="rounded-lg p-12 text-center">
        <UIcon name="i-lucide-search-x" class="h-12 w-12 text-gray-400 mx-auto mb-4" />
        <p class="text-gray-600 dark:text-gray-400">
          {{ $t('profile.automation.noFilterResults') }}
        </p>
      </div>

      <!-- Automation Rules List -->
      <div v-else class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
        <AutomationRuleCard
          v-for="automation in filteredAutomations"
          :key="automation.publicId"
          :automation="automation"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAutomationApi } from '~/composables/api/useAutomationApi'
import { AutomationActionType, ScheduleType } from '~/types/automation'
import type { AutomationResponse } from '~/types/automation'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getAutomations } = useAutomationApi()
const { t } = useI18n()

// Add-action lives on the dynamic nav FAB instead of an inline header button.
useFabActions(() => [
  {
    label: t('common.add'),
    icon: 'i-lucide-plus',
    handler: () => navigateTo('/profile/automation/create')
  }
])

// State
const loading = ref(true)
const automations = ref<AutomationResponse[]>([])

// Filter state
const searchQuery = ref('')
const filterType = ref<string>('all')
const filterStatus = ref<string>('all')
const filterScheduleType = ref<string>('all')
const triggeredFilter = ref(false)

// Filter options
const typeFilterOptions = computed(() => [
  { value: 'all', label: t('profile.automation.allTypes') },
  { value: String(AutomationActionType.AutoConsume), label: t('profile.automation.autoConsume') },
  { value: String(AutomationActionType.NotifyOnly), label: t('profile.automation.notifyOnly') },
  { value: String(AutomationActionType.AddToShoppingList), label: t('profile.automation.addToShoppingList') },
  { value: String(AutomationActionType.LowStockAddToShoppingList), label: t('profile.automation.lowStockAddToShoppingList') }
])

const statusFilterOptions = computed(() => [
  { value: 'all', label: t('profile.automation.allStatuses') },
  { value: 'enabled', label: t('profile.automation.enabled') },
  { value: 'disabled', label: t('profile.automation.disabled') }
])

const scheduleTypeFilterOptions = computed(() => [
  { value: 'all', label: t('common.filters.all') },
  { value: String(ScheduleType.Interval), label: t('profile.automation.interval') },
  { value: String(ScheduleType.FixedDate), label: t('profile.automation.fixedDate') }
])

// Filtered automations
const filteredAutomations = computed(() => {
  let result: AutomationResponse[] = automations.value

  // Search filter
  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase().trim()
    result = result.filter(a =>
      a.productName.toLowerCase().includes(query) ||
      (a.productBrand && a.productBrand.toLowerCase().includes(query))
    )
  }

  // Type filter
  if (filterType.value !== 'all') {
    const typeNum = Number(filterType.value)
    result = result.filter(a => a.actionType === typeNum)
  }

  // Status filter
  if (filterStatus.value === 'enabled') {
    result = result.filter(a => a.isEnabled)
  } else if (filterStatus.value === 'disabled') {
    result = result.filter(a => !a.isEnabled)
  }

  // Schedule type filter
  if (filterScheduleType.value !== 'all') {
    const scheduleNum = Number(filterScheduleType.value)
    result = result.filter(a => a.scheduleType === scheduleNum)
  }

  // Triggered toggle
  if (triggeredFilter.value) {
    result = result.filter(a => a.isTriggered)
  }

  return result
})

// Active filter chips
const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (filterType.value !== 'all') {
    const opt = typeFilterOptions.value.find(o => o.value === filterType.value)
    chips.push({ key: 'type', label: opt?.label ?? '', clear: () => { filterType.value = 'all' } })
  }
  if (filterStatus.value !== 'all') {
    const opt = statusFilterOptions.value.find(o => o.value === filterStatus.value)
    chips.push({ key: 'status', label: opt?.label ?? '', clear: () => { filterStatus.value = 'all' } })
  }
  if (filterScheduleType.value !== 'all') {
    const opt = scheduleTypeFilterOptions.value.find(o => o.value === filterScheduleType.value)
    chips.push({ key: 'scheduleType', label: opt?.label ?? '', clear: () => { filterScheduleType.value = 'all' } })
  }
  if (triggeredFilter.value) {
    chips.push({ key: 'triggered', label: t('profile.automation.triggered'), clear: () => { triggeredFilter.value = false } })
  }
  return chips
})

const activeFilterCount = computed(() => activeFilters.value.length)

function clearAllFilters() {
  filterType.value = 'all'
  filterStatus.value = 'all'
  filterScheduleType.value = 'all'
  triggeredFilter.value = false
}

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

onMounted(() => {
  loadAutomations()
})
</script>
