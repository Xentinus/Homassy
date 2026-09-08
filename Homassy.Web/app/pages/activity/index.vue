<template>
  <div>
    <!-- Filters. Changing either resets the cursor and the list (see resetAndLoad) -- a stale
         cursor from the previous filter would page through the wrong set. -->
    <div class="space-y-3 mb-4">
      <FilterChipGroup
        :label="t('activity.filters.type')"
        :model-value="typeFilterChip"
        :options="typeOptions"
        @update:model-value="typeFilterChip = $event"
      />
      <FilterChipGroup
        :label="t('activity.filters.member')"
        :model-value="memberFilterChip"
        :options="memberOptions"
        @update:model-value="memberFilterChip = $event"
      />
    </div>

    <!-- Initial load (and every filter-triggered reload, which also empties entries first). -->
    <div v-if="isInitialLoading" class="space-y-3">
      <SkeletonCard v-for="i in 4" :key="i" :lines="2" :footer-lines="1" />
    </div>

    <EmptyState
      v-else-if="entries.length === 0"
      :illustration="hasActiveFilters ? 'search' : 'calendar'"
      :title="t('activity.empty.title')"
      :description="t('activity.empty.description')"
      :action-label="hasActiveFilters ? t('common.filters.clear') : undefined"
      action-icon="i-lucide-filter-x"
      @action="clearFilters"
    />

    <template v-else>
      <!-- One AnimatedList per day -- a header inside one would join the cards' FLIP animation
           (same reasoning as the inventory grid's grouped sections, see products/index.vue). -->
      <section v-for="group in dayGroups" :key="group.key" :aria-labelledby="`activity-day-${group.key}`">
        <h2
          :id="`activity-day-${group.key}`"
          class="sticky z-20 -mx-1 mb-2 flex items-baseline gap-2 bg-default/95 px-1 py-2 backdrop-blur"
          :style="{ top: 'calc(var(--app-header-height, 5.5rem) - 0.25rem)' }"
        >
          <span class="text-sm font-bold uppercase tracking-wide text-highlighted">{{ dayGroupHeading(group.key) }}</span>
          <span class="text-xs text-muted tabular-nums">{{ group.entries.length }}</span>
        </h2>

        <AnimatedList class="space-y-3 mb-4">
          <template v-for="entry in group.entries" :key="entry.publicId">
            <AggregatedActivityCard v-if="entry.count > 1" :entry="entry" />

            <ActivityCardShell
              v-else
              :user-public-id="entry.userPublicId"
              :identity-color="entry.userIdentityColor"
              :activity-type="entry.activityType"
              :record-name="entry.recordName"
              :timestamp="entry.timestamp"
            >
              <template #leading>
                <UserAvatar
                  :src="entry.userProfilePictureUrl"
                  :name="entry.userName"
                  :public-id="entry.userPublicId"
                  :identity-color="entry.userIdentityColor"
                  :size="40"
                />
              </template>

              <template #meta>
                <p class="font-semibold text-gray-900 dark:text-white truncate">
                  {{ entry.userName }}
                </p>
              </template>

              <template #details>
                <div v-if="entry.quantity != null && entry.unit != null" class="flex items-center gap-2">
                  <UIcon name="i-lucide-scale" class="h-4 w-4 text-amber-600 dark:text-amber-400" />
                  <p class="text-sm text-gray-700 dark:text-gray-300">
                    {{ formatQuantity(entry.quantity) }} {{ t(`enums.unit.${entry.unit}`) }}
                  </p>
                </div>
              </template>
            </ActivityCardShell>
          </template>
        </AnimatedList>
      </section>
    </template>

    <!-- Sentinel: stops rendering (and so stops being observed at all) once there is no next
         cursor, or once a page fetch has failed -- see loadMore's own guard for why a failed
         cursor must never be retried automatically. -->
    <div v-if="hasLoadedOnce && nextCursor !== null && !loadError" ref="sentinelRef" class="h-px" />

    <div v-if="isFetchingMore" class="space-y-3 mt-3">
      <SkeletonCard v-for="i in 2" :key="i" :lines="2" :footer-lines="1" />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The family activity timeline (#108): a day-grouped, infinitely-scrolling feed over the
 * cursor-paged, server-aggregated `GET /User/activities/timeline` endpoint.
 *
 * Day grouping is the pure `groupByDay` (see `app/utils/activityTimeline.ts` and its spec) fed
 * from a plain `computed` -- there is no reactive wrapper composable for it, unlike
 * `useRealtimeStatus`/`realtimeStatus.ts`, because there is no connection state here to make
 * reactive, only a list and a clock.
 *
 * Rendering reuses `ActivityCardShell` directly (at `density="comfortable"`) rather than the
 * higher-level `ActivityCard.vue` component: `ActivityCard.vue` is built around the older
 * `ActivityInfo` + a separately-looked-up `UserInfo`, while `ActivityTimelineEntry` already
 * carries the actor's name/avatar/colour inline (that inlining is the whole point of the new
 * DTO -- see Homassy.API.Models.Activity.ActivityTimelineEntry). Building a fake `UserInfo` just
 * to satisfy `ActivityCard.vue`'s prop shape would need placeholder values for fields
 * (`timeZone`, `language`, `currency`) the timeline never has and `ActivityCard.vue` never reads.
 * `ActivityCardShell` is the actual reusable unit Task 12 extracted; this page is its first real
 * consumer, at the same density and slot layout `ActivityCard.vue` itself uses.
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { ActivityTimelineEntry } from '~/types/activity'
import { ActivityType } from '~/types/activity'
import type { FamilyMemberResponse } from '~/types/family'
import type { ItemDeletedEvent, ItemUpsertedEvent } from '~/types/realtime'
import { groupByDay, type DayBucketKey } from '~/utils/activityTimeline'

definePageMeta({ layout: 'auth' })

const { t, locale } = useI18n()
const { getActivityTimeline } = useUserApi()
const { getFamilyMembers } = useFamilyApi()
const socket = useShoppingListSocket()
const { isPendingEntity } = useUndoableAction()

usePageHeader(() => ({
  icon: 'i-lucide-activity',
  title: t('activity.title')
}))

const PAGE_SIZE = 30

const entries = ref<ActivityTimelineEntry[]>([])
/** `undefined` before the first page has ever resolved; `null` once the server says no more. */
const nextCursor = ref<string | null | undefined>(undefined)
const isFetching = ref(false)
const hasLoadedOnce = ref(false)
/** An undecodable cursor is a deliberate 400, not a transient failure -- see fetchPage. Stops
 *  paging until the next filter change (resetAndLoad) rather than being silently retried. */
const loadError = ref(false)

const isInitialLoading = computed(() => isFetching.value && entries.value.length === 0)
const isFetchingMore = computed(() => isFetching.value && entries.value.length > 0)

const dayGroups = computed(() => groupByDay(entries.value, new Date()))

/**
 * `key` -> a heading. 'today'/'yesterday' are translated; anything older is already a plain
 * `YYYY-MM-DD` (see `dayBucketKey`), reformatted per locale by rearranging that string's own
 * parts -- NOT by feeding it back through `new Date(...)` and reading local getters, which would
 * reinterpret a UTC calendar date through the viewer's own timezone and could show the wrong day
 * for a negative-UTC-offset viewer (`useDateFormat.formatDate` does exactly that reparse, which is
 * why it is not reused here).
 */
const dayGroupHeading = (key: DayBucketKey): string => {
  if (key === 'today') return t('activity.today')
  if (key === 'yesterday') return t('activity.yesterday')

  const [year, month, day] = key.split('-')
  switch (locale.value) {
    case 'hu': return `${year}.${month}.${day}`
    case 'de': return `${day}.${month}.${year}`
    default: return `${day}/${month}/${year}`
  }
}

// Format quantity: show integers without decimals, otherwise max 2 decimal places. Mirrors
// ActivityCard.vue's own helper -- not extracted into a shared util, since Task 12's scope moved
// only the icon mapping, and this is a two-line, page-local presentational detail.
const formatQuantity = (quantity: number): string =>
  Number.isInteger(quantity) ? quantity.toString() : quantity.toFixed(2)

// --- Filters -------------------------------------------------------------------------------
// FilterChipGroup's modelValue/options are string-keyed, hence the 'all' sentinel plus a
// computed translating it to the number | undefined / string | undefined the API takes.

const typeFilterChip = ref<string>('all')
const memberFilterChip = ref<string>('all')

const typeFilterValue = computed<number | undefined>(() =>
  typeFilterChip.value === 'all' ? undefined : Number(typeFilterChip.value))
const memberFilterValue = computed<string | undefined>(() =>
  memberFilterChip.value === 'all' ? undefined : memberFilterChip.value)

const hasActiveFilters = computed(() => typeFilterChip.value !== 'all' || memberFilterChip.value !== 'all')

const typeOptions = computed(() => [
  { label: t('activity.filters.all'), value: 'all' },
  ...Object.values(ActivityType)
    .filter((value): value is number => typeof value === 'number')
    .map(value => ({ label: t(`enums.activityType.${value}`), value: String(value) }))
])

const familyMembers = ref<FamilyMemberResponse[]>([])
const memberOptions = computed(() => [
  { label: t('activity.filters.all'), value: 'all' },
  ...familyMembers.value.map(member => ({ label: member.displayName, value: member.publicId }))
])

const clearFilters = (): void => {
  typeFilterChip.value = 'all'
  memberFilterChip.value = 'all'
}

// --- Fetching --------------------------------------------------------------------------------

/**
 * Fetches one page. `isFetching` is the single guard against a double-fire -- shared by the
 * initial load, "load more", and the live-refresh path below, so none of the three can ever
 * overlap another.
 */
const fetchPage = async (cursor: string | undefined): Promise<void> => {
  if (isFetching.value) return
  isFetching.value = true

  try {
    const response = await getActivityTimeline({
      cursor,
      pageSize: PAGE_SIZE,
      activityType: typeFilterValue.value,
      userPublicId: memberFilterValue.value
    })

    if (!response.success || !response.data) {
      // Deliberately not cleared/reset here -- see loadError's own doc comment. useApiClient has
      // already toasted the failure; this just stops the sentinel from retrying it forever.
      loadError.value = true
      return
    }

    loadError.value = false
    entries.value = cursor ? [...entries.value, ...response.data.entries] : response.data.entries
    nextCursor.value = response.data.nextCursor ?? null
  } finally {
    isFetching.value = false
    hasLoadedOnce.value = true
  }
}

const resetAndLoad = (): void => {
  entries.value = []
  nextCursor.value = undefined
  loadError.value = false
  void fetchPage(undefined)
}

/** The IntersectionObserver callback. Guards against both a double-fire and retrying a page that
 *  already failed -- an undecodable cursor is a deliberate 400, not something to recover from by
 *  restarting at page one (that is exactly the infinite loop the cursor exists to prevent). */
const loadMore = (): void => {
  if (isFetching.value || loadError.value) return
  if (!nextCursor.value) return
  void fetchPage(nextCursor.value)
}

watch([typeFilterValue, memberFilterValue], resetAndLoad)

// --- Infinite scroll ---------------------------------------------------------------------------

const sentinelRef = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

watch(sentinelRef, (newSentinel) => {
  observer.value?.disconnect()

  if (newSentinel) {
    observer.value = new IntersectionObserver(
      (observerEntries) => {
        const [observerEntry] = observerEntries
        if (observerEntry?.isIntersecting) loadMore()
      },
      { root: null, rootMargin: '100px', threshold: 0.1 }
    )
    observer.value.observe(newSentinel)
  }
})

// --- Live-prepend --------------------------------------------------------------------------
// No dedicated "activity created" push event exists (see Homassy.API's hub list -- only
// ShoppingList and Inventory have one); useShoppingListSocket's item events are the nearest
// available signal, exactly as the brief names. Re-fetches page one under the current filters and
// prepends only the entries not already shown, rather than trying to fabricate a synthetic
// ActivityTimelineEntry from the socket payload (which lacks the server's activity-type mapping
// and aggregation).

let refreshInFlight = false

const refreshLatest = async (): Promise<void> => {
  if (isFetching.value || refreshInFlight) return
  refreshInFlight = true

  try {
    const response = await getActivityTimeline({
      pageSize: PAGE_SIZE,
      activityType: typeFilterValue.value,
      userPublicId: memberFilterValue.value
    })
    if (!response.success || !response.data) return

    const knownIds = new Set(entries.value.map(entry => entry.publicId))
    const fresh = response.data.entries.filter(entry => !knownIds.has(entry.publicId))
    if (fresh.length > 0) entries.value = [...fresh, ...entries.value]
  } finally {
    refreshInFlight = false
  }
}

/** Skips the refresh when the change came from the current user's own optimistic write still
 *  pending its undo window -- isPendingEntity is true only for entities this session itself
 *  queued, so it already implies both "I am the actor" and "this has not settled yet". */
const handleItemUpserted = (event: ItemUpsertedEvent): void => {
  if (isPendingEntity(event.item.publicId)) return
  void refreshLatest()
}

const handleItemDeleted = (event: ItemDeletedEvent): void => {
  if (isPendingEntity(event.publicId)) return
  void refreshLatest()
}

onMounted(async () => {
  socket.on('ItemUpserted', handleItemUpserted)
  socket.on('ItemDeleted', handleItemDeleted)

  resetAndLoad()

  const membersResponse = await getFamilyMembers()
  if (membersResponse.success && membersResponse.data) {
    familyMembers.value = membersResponse.data
  }
})

onBeforeUnmount(() => {
  socket.off('ItemUpserted', handleItemUpserted)
  socket.off('ItemDeleted', handleItemDeleted)
  observer.value?.disconnect()
})
</script>
