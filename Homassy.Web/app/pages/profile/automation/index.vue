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
              @click="() => { triggeredFilter = !triggeredFilter }"
            />
          </div>
        </div>
      </template>
    </ListFilterBar>

    <!-- Content Section -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6">

      <!-- Loading State — first load only. A refetch keeps the grid mounted;
           swapping it out would remount every card and replay the bubble
           animation. The container repeats the real list's grid classes
           verbatim (it used to be a `space-y-4` stack against a grid), so the
           placeholders occupy the cells the cards will land in. -->
      <template v-if="loading && !hasLoaded">
        <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
          <SkeletonCard v-for="i in 8" :key="i" />
        </div>
      </template>

      <template v-else>
        <!-- Both empty states render next to the (then empty) grid, never in
             place of it: unmounting the grid replays the enter animation on the
             way back and swallows the leave animation of the last card. -->
        <EmptyState
          v-if="automations.length === 0"
          illustration="automation"
          :title="$t('profile.automation.noAutomations')"
          :description="$t('profile.automation.addFirstAutomation')"
          :action-label="$t('profile.automation.createAutomation')"
          action-icon="i-lucide-plus"
          @action="navigateTo('/profile/automation/create')"
        />

        <!-- No filter results -->
        <EmptyState
          v-else-if="filteredAutomations.length === 0"
          illustration="search"
          :title="$t('profile.automation.noFilterResults')"
          :description="$t('profile.automation.tryDifferentSearch')"
          :action-label="$t('common.filters.clear')"
          action-icon="i-lucide-filter-x"
          @action="clearAllFilters"
        />

        <!-- Automation Rules List. The wrapper exists so the drag composable has a plain element to
             scan for `data-reorder-key` rows — AnimatedList's own root keeps the grid classes. -->
        <div ref="gridEl">
          <AnimatedList class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            <DataAutomationCard
              v-for="automation in filteredAutomations"
              :key="automation.publicId"
              :data-reorder-key="automation.publicId"
              :automation="automation"
              reorderable
              :dragging="reorder.draggingKey.value === automation.publicId"
              @deleted="handleCardDeleted"
              @reorder-lift="(event) => reorder.startDrag(event, automation.publicId)"
              @reorder-move="(direction) => reorder.moveByKeyboard(automation.publicId, direction)"
            />
          </AnimatedList>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAutomationApi } from '~/composables/api/useAutomationApi'
import { AutomationActionType, ScheduleType } from '~/types/automation'
import type { AutomationResponse } from '~/types/automation'
import type { MasterDataDeletedEvent, MasterDataReorderedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getAutomations, reorderAutomations } = useAutomationApi()
const masterDataSocket = useMasterDataSocket()
const { t } = useI18n()
const toast = useToast()

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
// Distinguishes the first load (skeletons) from a refetch (keep the grid mounted
// so the bubble animation is not replayed for every card).
const hasLoaded = ref(false)
const automations = ref<AutomationResponse[]>([])

// Filter state
const searchQuery = ref('')

// "Show all N in Automations" from the command palette (#111). A single automation is a route
// of its own, so this page only ever receives a term.
useSearchHandoff({
  search: (term) => { searchQuery.value = term }
})

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

// Search + filter predicate, shared by the rendered list and by the reorder composable (which must
// only ever send the rules the user can actually see).
function matchesFilters(a: AutomationResponse): boolean {
  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase().trim()
    const matches = a.productName.toLowerCase().includes(query)
      || (a.productBrand && a.productBrand.toLowerCase().includes(query))
    if (!matches) return false
  }

  if (filterType.value !== 'all' && a.actionType !== Number(filterType.value)) return false

  if (filterStatus.value === 'enabled' && !a.isEnabled) return false
  if (filterStatus.value === 'disabled' && a.isEnabled) return false

  if (filterScheduleType.value !== 'all' && a.scheduleType !== Number(filterScheduleType.value)) return false

  if (triggeredFilter.value && !a.isTriggered) return false

  return true
}

// Manual ordering (#113). `sortOrder` is zero on every rule nobody has dragged, so the enabled-first,
// soonest-next order the list has always had survives as the tie-break.
const gridEl = ref<HTMLElement | null>(null)
const reorder = useReorderableList<AutomationResponse>({
  items: automations,
  container: gridEl,
  baseSort: (a, b) => {
    if (a.isEnabled !== b.isEnabled) return a.isEnabled ? -1 : 1
    return (a.nextExecutionAt ?? '').localeCompare(b.nextExecutionAt ?? '')
  },
  filter: matchesFilters,
  commit: orderedIds => reorderAutomations({ automationPublicIds: orderedIds }),
  onFailed: (error) => {
    console.error('Failed to reorder automations:', error)
    toast.add({ title: t('common.error'), description: t('common.reorder.failed'), color: 'error' })
  }
})

/** What the grid renders: the filter applied, in manual order (see `useReorderableList`). */
const filteredAutomations = computed(() => reorder.orderedItems.value)

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
    hasLoaded.value = true
  }
}

// Realtime: patch the list in place so a family member's (or another device's) create/update/delete
// shows instantly. Handlers are idempotent, so the acting client's own echoed event is a no-op.
function handleAutomationUpserted(dto: AutomationResponse) {
  const idx = automations.value.findIndex(a => a.publicId === dto.publicId)
  if (idx >= 0) automations.value.splice(idx, 1, dto)
  else automations.value.push(dto)
}

function handleAutomationDeleted(payload: MasterDataDeletedEvent) {
  automations.value = automations.value.filter(a => a.publicId !== payload.publicId)
}

// Another member dragged something: patch the positions that moved and let the order recompute.
function handleAutomationsReordered(payload: MasterDataReorderedEvent) {
  reorder.applyReorderedEntries(payload.entries)
}

// Card emitted a delete (own API call) — remove locally; the realtime echo is then a no-op.
function handleCardDeleted(publicId: string) {
  automations.value = automations.value.filter(a => a.publicId !== publicId)
}

onMounted(async () => {
  await loadAutomations()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('AutomationUpserted', handleAutomationUpserted)
  masterDataSocket.on('AutomationDeleted', handleAutomationDeleted)
  masterDataSocket.on('AutomationsReordered', handleAutomationsReordered)
  masterDataSocket.onReconnected(loadAutomations)
})

onBeforeUnmount(() => {
  masterDataSocket.off('AutomationUpserted', handleAutomationUpserted)
  masterDataSocket.off('AutomationDeleted', handleAutomationDeleted)
  masterDataSocket.off('AutomationsReordered', handleAutomationsReordered)
  masterDataSocket.offReconnected(loadAutomations)
})
</script>
