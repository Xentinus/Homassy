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

      <!-- Search & Filter Bar -->
      <div v-if="!loading && automations.length > 0" class="flex flex-wrap items-center gap-2">
        <UInput
          v-model="searchQuery"
          :placeholder="$t('profile.automation.searchPlaceholder')"
          icon="i-lucide-search"
          size="sm"
          class="flex-1 min-w-48"
        />
        <USelect
          v-model="filterType"
          :items="typeFilterOptions"
          value-key="value"
          label-key="label"
          size="sm"
          class="w-44"
        />
        <USelect
          v-model="filterStatus"
          :items="statusFilterOptions"
          value-key="value"
          label-key="label"
          size="sm"
          class="w-36"
        />
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div :class="['pt-28 px-4 sm:px-8 lg:px-14 pb-6', { 'pt-40': !loading && automations.length > 0 }]">

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
import { AutomationActionType } from '~/types/automation'
import type { AutomationResponse } from '~/types/automation'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getAutomations } = useAutomationApi()
const { t } = useI18n()

// State
const loading = ref(true)
const automations = ref<AutomationResponse[]>([])

// Filter state
const searchQuery = ref('')
const filterType = ref<string>('all')
const filterStatus = ref<string>('all')

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

  return result
})

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
