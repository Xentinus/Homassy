<template>
  <div>
    <!-- Filters. Changing either resets the cursor and the list (see resetAndLoad) -- a stale
         cursor from the previous filter would page through the wrong set. -->
    <div class="space-y-3 mb-4">
      <div>
        <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ t('activity.filters.type') }}</p>
        <div class="flex items-center gap-2">
          <!-- 29 ActivityType values don't fit as chips on a phone (16 wrapped rows) -- a
               searchable USelectMenu costs one line. The backend timeline endpoint takes a single
               exact ActivityType (see ActivityTimelineRequest), never a group or list, so this
               still resolves to exactly one value (or none, meaning all). "All" is reached by
               clearing the selection, same as the shopping-location picker on ShoppingListItemCard. -->
          <USelectMenu
            v-model="typeFilter"
            :items="typeOptions"
            value-key="value"
            :placeholder="t('activity.filters.allTypes')"
            :search-input="{ placeholder: t('common.search') }"
            :aria-label="t('activity.filters.type')"
            class="flex-1"
          />
          <UButton
            v-if="typeFilter !== undefined"
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            :aria-label="t('common.clear')"
            @click="typeFilter = undefined"
          />
        </div>
      </div>
      <FilterChipGroup
        :label="t('activity.filters.member')"
        :model-value="memberFilterChip"
        :options="memberOptions"
        @update:model-value="memberFilterChip = $event"
      />
    </div>

    <!-- Showing one window rather than everything (#127) - arrived here from the away-delta card.
         Says so out loud and offers a way out: a timeline silently hiding older entries would read
         as a bug, and the query string is not something anyone should have to notice. -->
    <div v-if="hasWindow" class="mb-3 flex items-center gap-2 rounded-lg bg-elevated px-3 py-2">
      <UIcon name="i-lucide-history" class="h-4 w-4 shrink-0 text-primary" />
      <span class="min-w-0 flex-1 truncate text-sm text-muted">{{ t('activity.away.windowNotice') }}</span>
      <UButton
        size="xs"
        color="neutral"
        variant="ghost"
        icon="i-lucide-x"
        :label="t('activity.away.showAll')"
        @click="clearWindow"
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
          <!-- Inside a delta window every row is, by definition, something that changed while the
               user was away - so the whole windowed list carries R4's existing "newer than what
               was on screen" flash (.row-updated-flash in main.css) rather than a second keyframe
               of its own. It is deliberately not applied to the unwindowed timeline, where a row
               being present says nothing about it being new. The class is already neutralised
               under prefers-reduced-motion where it is defined. -->
          <template v-for="entry in group.entries" :key="entry.publicId">
            <AggregatedActivityCard
              v-if="entry.count > 1"
              :entry="entry"
              :class="windowedRowClass"
            />

            <ActivityCardShell
              v-else
              :class="windowedRowClass"
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
import { formatDayBucketDate, groupByDay, mergeLiveEntries, type DayBucketKey } from '~/utils/activityTimeline'

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

/**
 * The optional window (#127), read straight from this page's own query string: the away-delta card
 * links here as `/activity?since=...&until=...` with the exact window its count was computed over.
 *
 * A single string (or nothing) rather than a parsed date: it is passed through to the API verbatim,
 * so parsing it here would only create a second chance to change its meaning. A malformed value is
 * the server's 400 to give, not this page's to guess at.
 *
 * Reactive on the route, so arriving at the windowed view and then clearing the window (or the
 * card linking again with a different one) re-runs the fetch through the same reset path a filter
 * change uses.
 */
const route = useRoute()

const queryValue = (value: unknown): string | undefined =>
  typeof value === 'string' && value.length > 0 ? value : undefined

const windowSince = computed(() => queryValue(route.query.since))
const windowUntil = computed(() => queryValue(route.query.until))

/** True while the timeline is showing one specific window rather than everything. */
const hasWindow = computed(() => windowSince.value !== undefined || windowUntil.value !== undefined)

/**
 * R4's existing "newer than what was on screen" flash, applied to every row of a windowed list -
 * inside a delta window a row being present IS the news. Empty for the ordinary timeline, where
 * presence says nothing about novelty. The class already neutralises itself under
 * `prefers-reduced-motion` where it is defined (main.css), so there is nothing to guard here.
 */
const windowedRowClass = computed(() => (hasWindow.value ? 'row-updated-flash rounded-2xl' : ''))

const clearWindow = (): void => {
  const query = { ...route.query }
  delete query.since
  delete query.until
  void navigateTo({ path: '/activity', query })
}

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
 * `key` -> a heading. 'today'/'yesterday' are translated; anything older is a plain
 * `YYYY-MM-DD` (see `dayBucketKey`) formatted by `formatDayBucketDate`, which the notification
 * centre's own day headers share (#116) -- see that helper for why the key's parts are rearranged
 * rather than reparsed as a Date.
 */
const dayGroupHeading = (key: DayBucketKey): string => {
  if (key === 'today') return t('activity.today')
  if (key === 'yesterday') return t('activity.yesterday')

  return formatDayBucketDate(key, locale.value)
}

// Format quantity: show integers without decimals, otherwise max 2 decimal places. Mirrors
// ActivityCard.vue's own helper -- not extracted into a shared util, since Task 12's scope moved
// only the icon mapping, and this is a two-line, page-local presentational detail.
const formatQuantity = (quantity: number): string =>
  Number.isInteger(quantity) ? quantity.toString() : quantity.toFixed(2)

// --- Filters -------------------------------------------------------------------------------
// The member filter stays chips (a family has a handful of members). The type filter is a
// searchable USelectMenu instead -- FilterChipGroup's modelValue/options are string-keyed hence
// the 'all' sentinel there, but typeFilter binds straight to the ActivityType enum (or undefined
// for "all"), so it needs no such translation.

const typeFilter = ref<ActivityType | undefined>(undefined)
const memberFilterChip = ref<string>('all')

const memberFilterValue = computed<string | undefined>(() =>
  memberFilterChip.value === 'all' ? undefined : memberFilterChip.value)

const hasActiveFilters = computed(() => typeFilter.value !== undefined || memberFilterChip.value !== 'all')

const typeOptions = computed(() =>
  Object.values(ActivityType)
    .filter((value): value is number => typeof value === 'number')
    .map(value => ({ label: t(`enums.activityType.${value}`), value }))
)

const familyMembers = ref<FamilyMemberResponse[]>([])
const memberOptions = computed(() => [
  { label: t('activity.filters.all'), value: 'all' },
  ...familyMembers.value.map(member => ({ label: member.displayName, value: member.publicId }))
])

const clearFilters = (): void => {
  typeFilter.value = undefined
  memberFilterChip.value = 'all'
}

// --- Fetching --------------------------------------------------------------------------------

/**
 * Bumped by every `resetAndLoad` (a filter change) and captured by each `fetchPage` call at the
 * moment it starts. A fetch whose captured generation no longer matches the current one has been
 * superseded by a filter change that started while it was in flight -- it discards its own result
 * instead of writing stale, wrong-filter data over what the new generation's own fetch is loading
 * (or has already loaded). Plain state, not a ref: nothing renders off it directly.
 */
let requestGeneration = 0

/**
 * Fetches one page. `isFetching` is the guard against a double-fire -- shared by the initial
 * load, "load more", and the live-refresh path below, so none of the three can ever overlap
 * another *within the same generation*. `resetAndLoad` deliberately clears it before starting the
 * new generation's own fetch, so a reset is never blocked by a still-in-flight superseded one.
 */
const fetchPage = async (cursor: string | undefined): Promise<void> => {
  if (isFetching.value) return
  isFetching.value = true
  const generation = requestGeneration

  try {
    const response = await getActivityTimeline({
      cursor,
      pageSize: PAGE_SIZE,
      activityType: typeFilter.value,
      userPublicId: memberFilterValue.value,
      since: windowSince.value,
      until: windowUntil.value
    })

    // A filter change superseded this fetch while it was in flight (resetAndLoad bumped
    // requestGeneration and started its own fetch already) -- this response belongs to a filter
    // that is no longer selected. Discard it rather than let it land on top of the new
    // generation's cleared-then-reloaded state, or resurrect entries/nextCursor for a filter the
    // user has since changed away from.
    if (generation !== requestGeneration) return

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
    // Same supersession check: a stale fetch's finally must not clear isFetching/hasLoadedOnce
    // out from under the new generation's own fetch, which by now has legitimately set isFetching
    // back to true for itself.
    if (generation === requestGeneration) {
      isFetching.value = false
      hasLoadedOnce.value = true
    }
  }
}

const resetAndLoad = (): void => {
  requestGeneration++
  // A still-in-flight fetch from the previous generation must not keep this guard held -- without
  // this, the reload two lines down would see isFetching still true (from that superseded fetch)
  // and return immediately, silently dropping the reload entirely. Safe to clear synchronously:
  // fetchPage below re-sets it to true before its own first await, so nothing else can observe it
  // false in between.
  isFetching.value = false
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

// The window joins the filters: arriving from the away-delta card, changing to a different window,
// or clearing it all go through the same reset-and-reload path a filter change does.
watch([typeFilter, memberFilterValue, windowSince, windowUntil], resetAndLoad)

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
      activityType: typeFilter.value,
      userPublicId: memberFilterValue.value
    })
    if (!response.success || !response.data) return

    // A "new" publicId is not necessarily an unrelated new entry — it can be a run that already
    // had a standalone entry on screen, just re-anchored to a newer row as it grew (see
    // mergeLiveEntries's own doc comment). knownIds only catches the first kind; mergeLiveEntries
    // also drops whichever existing entry the second kind now subsumes.
    const knownIds = new Set(entries.value.map(entry => entry.publicId))
    const fresh = response.data.entries.filter(entry => !knownIds.has(entry.publicId))
    if (fresh.length > 0) entries.value = mergeLiveEntries(fresh, entries.value)
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
