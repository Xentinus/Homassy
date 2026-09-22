<template>
  <div class="px-4 sm:px-8 lg:px-14 pb-6">
    <PullToRefreshIndicator
      :pull-distance="pullDistance"
      :is-pulling="isPulling"
      :is-refreshing="isRefreshing"
      :is-ready="isReady"
    />

    <RealtimeConnectionBar class="mb-4" />

    <div class="max-w-2xl mx-auto space-y-4">
      <!-- Who is around. Absent entirely for a household of one: there is no roster to show, and
           an invitation nobody asked for is not what the first screen is for. -->
      <HomePresenceStrip
        v-if="presentMembers.length > 1"
        :members="presentMembers"
        :self-public-id="authStore.user?.publicId"
      />

      <!-- First load: a skeleton where the block will be, so the notes below do not start at the
           top of the screen and then get pushed down. -->
      <div v-if="isLoading && !hasLoaded" class="rounded-xl border border-default bg-default px-3.5 py-2">
        <SkeletonRow v-for="i in 3" :key="i" icon />
      </div>

      <!-- The two contexts, shown only as far as there is anything in them: both, and they are a
           pair of alternatives behind one switcher; one, and the switcher would be a control with
           one useful position, so the block is simply that context under its own heading; neither,
           and there is nothing here at all. An empty state per context would be two panels
           announcing that nothing is waiting, which is worth less than the space it costs. -->
      <template v-else-if="activeContext">
        <div
          v-if="showContextSwitcher"
          role="tablist"
          :aria-label="$t('pages.household.contextLabel')"
          class="grid grid-cols-2 gap-1 p-1 rounded-xl bg-elevated"
        >
          <button
            v-for="tab in tabs"
            :id="`home-tab-${tab.key}`"
            :key="tab.key"
            type="button"
            role="tab"
            :aria-selected="activeContext === tab.key"
            :aria-controls="`home-panel-${tab.key}`"
            class="min-h-10 px-2 rounded-lg flex items-center justify-center gap-1.5 text-sm transition-colors duration-200"
            :class="activeContext === tab.key
              ? 'bg-default shadow-sm font-bold'
              : 'text-muted font-medium hover:text-default'"
            @click="context = tab.key"
          >
            <UIcon :name="tab.icon" class="h-4 w-4 shrink-0" />
            <span class="truncate">{{ tab.label }}</span>
            <span
              v-if="tab.count > 0"
              class="shrink-0 rounded-full px-1.5 text-[11px] font-bold tabular-nums"
              :class="tab.countClass"
            >{{ tab.count }}</span>
          </button>
        </div>

        <!-- Without the switcher the block still has to say which context it is, so the tab's own
             label becomes a heading in the same shape as the sections below it. -->
        <h2 v-else class="px-0.5 -mb-2 text-[15px] font-bold flex items-center gap-1.5">
          <UIcon :name="activeTab!.icon" class="h-4 w-4 shrink-0 text-muted" />
          {{ activeTab!.label }}
        </h2>

        <!-- AT HOME -->
        <section
          v-if="activeContext === 'atHome'"
          :id="'home-panel-atHome'"
          :role="showContextSwitcher ? 'tabpanel' : undefined"
          :aria-labelledby="showContextSwitcher ? 'home-tab-atHome' : undefined"
          class="rounded-xl border border-default bg-default px-3.5"
        >
          <div
            v-for="(item, index) in atHomeItems"
            :key="item.publicId"
            :class="index > 0 || awaitingPutAway > 0 ? 'border-t border-default/60' : ''"
          >
            <HomeAtHomeRow
              :item="item"
              :busy="busyItems[item.publicId] ?? null"
              @consume="consumeItem(item)"
              @discard="discardItem(item)"
            />
          </div>

          <!-- Bought, never put away. Not stock yet, so it cannot be a row above — but it is the
               same chore ("deal with what came home"), which is why it lives here and not in a
               block of its own. -->
          <button
            v-if="awaitingPutAway > 0"
            type="button"
            class="w-full flex items-center gap-2.5 py-2.5 text-left"
            :class="atHomeItems.length > 0 ? 'border-t border-default/60' : ''"
            @click="navigateTo('/products?action=load-inventory')"
          >
            <span class="shrink-0 flex items-center justify-center h-8 w-8 rounded-lg bg-elevated text-muted">
              <UIcon name="i-lucide-package" class="h-4 w-4" />
            </span>
            <span class="flex-1 min-w-0">
              <span class="block text-sm font-semibold truncate">
                {{ $t('pages.household.atHome.putAway', { count: awaitingPutAway }) }}
              </span>
              <span class="block text-xs text-muted truncate">
                {{ $t('pages.household.atHome.putAwayHint') }}
              </span>
            </span>
            <UIcon name="i-lucide-chevron-right" class="shrink-0 h-4 w-4 text-muted" />
          </button>
        </section>

        <!-- IF YOU GO SHOPPING -->
        <section
          v-else
          :id="'home-panel-shopping'"
          :role="showContextSwitcher ? 'tabpanel' : undefined"
          :aria-labelledby="showContextSwitcher ? 'home-tab-shopping' : undefined"
          class="rounded-xl border border-default bg-default px-3.5"
        >
          <div
            v-for="(row, index) in shoppingRows"
            :key="row.listPublicId"
            :class="index > 0 ? 'border-t border-default/60' : ''"
          >
            <HomeShoppingRow :row="row" @shop="startShopping(row)" />
          </div>
        </section>
      </template>

      <!-- PINNED NOTES -->
      <section>
        <h2 class="px-0.5 mb-2 text-[15px] font-bold">{{ $t('pages.household.notes.title') }}</h2>

        <div v-if="isLoading && !hasLoaded" class="space-y-2">
          <SkeletonRow v-for="i in 2" :key="i" />
        </div>

        <div v-else-if="pinnedNotes.length > 0" class="space-y-2">
          <button
            v-for="note in pinnedNotes"
            :key="note.publicId"
            type="button"
            class="w-full flex items-center gap-2.5 rounded-xl border px-3 py-2.5 text-left transition-colors duration-200"
            :class="isToday(note.date)
              ? 'border-warning/40 bg-warning/5'
              : 'border-default bg-default hover:bg-elevated'"
            @click="editNote(note)"
          >
            <span
              class="shrink-0 w-14 text-[11px] font-bold uppercase tracking-wide"
              :class="isToday(note.date) ? 'text-warning' : 'text-muted'"
            >{{ noteDayLabel(note.date) }}</span>
            <span class="flex-1 min-w-0 text-sm font-semibold truncate">{{ note.title }}</span>
            <UserAvatar
              :name="note.createdByName"
              :public-id="note.createdByPublicId"
              :size="22"
              :alt="note.createdByName"
            />
          </button>
        </div>

        <p v-else class="px-0.5 text-sm text-muted">{{ $t('pages.household.notes.empty') }}</p>
      </section>

      <!-- TODAY -->
      <section>
        <div class="flex items-baseline justify-between px-0.5 mb-2">
          <h2 class="text-[15px] font-bold">{{ $t('pages.household.today.title') }}</h2>
          <NuxtLink to="/calendar" class="text-[13px] font-semibold text-primary-600 dark:text-primary-400">
            {{ $t('pages.household.today.calendar') }}
          </NuxtLink>
        </div>

        <div v-if="isLoading && !hasLoaded" class="space-y-2">
          <SkeletonRow v-for="i in 2" :key="i" />
        </div>

        <div v-else-if="todayAgenda.length > 0" class="rounded-xl border border-default bg-default px-3.5">
          <div
            v-for="(event, index) in todayAgenda"
            :key="event.publicId"
            class="flex items-center gap-2.5 py-2.5"
            :class="index > 0 ? 'border-t border-default/60' : ''"
          >
            <span
              class="shrink-0 w-1 h-6 rounded-full"
              :style="{ backgroundColor: agendaColor(event) }"
              aria-hidden="true"
            />
            <span class="shrink-0 w-11 text-[13px] font-bold text-muted tabular-nums">
              {{ event.isAllDay ? $t('pages.household.today.allDay') : formatTime(event.start) }}
            </span>
            <span class="flex-1 min-w-0 text-sm truncate">{{ event.title }}</span>
          </div>
        </div>

        <p v-else class="px-0.5 text-sm text-muted">{{ $t('pages.household.today.empty') }}</p>
      </section>
    </div>

    <CalendarNoteFormDrawer
      v-model:open="isNoteDrawerOpen"
      :date="noteDrawerDate"
      :note="editingNote"
      @saved="handleNoteSaved"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * The home screen — the first thing an authenticated visitor sees.
 *
 * It answers two questions and nothing else: **what is waiting for me at home**, and **what is
 * waiting for me in a shop**. Those are alternatives rather than a list, so they live behind one
 * toggle with the counts on the tabs; the household's presence sits above them, and the two
 * things that are neither a chore nor a purchase — the pinned notes and today's appointments —
 * sit below.
 *
 * What it deliberately does *not* show is an activity feed. Activity has its own timeline at
 * `/activity`, it is also folded into the calendar, and a third rendering of "things that already
 * happened" is the opposite of a screen you act on. This page replaced the calendar as the first
 * tab for that reason; the calendar itself is one tap away under "today".
 *
 * Everything derived lives in `~/utils/homeFocus` (and is unit-tested there) — this file is the
 * fetching, the actions and the chrome.
 */
import { CalendarEventType, type CalendarEventInfo, type CalendarNoteInfo } from '~/types/calendar'
import type { DetailedProductInfo } from '~/types/product'
import type { LoadableShoppingListItemInfo, ShoppingListInfo } from '~/types/shoppingList'
import {
  buildAtHomeItems,
  buildShoppingRows,
  buildTodayAgenda,
  countAwaitingPutAway,
  dayKey,
  filterPinnedNotes,
  pickHomeContext,
  SHOPPING_HORIZON_DAYS,
  type AtHomeItem,
  type ShoppingRow
} from '~/utils/homeFocus'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { t, locale } = useI18n()
const authStore = useAuthStore()
const toast = useToast()

const { getDetailedProducts, consumeInventoryItem, deleteInventoryItem } = useProductsApi()
const { getShoppingLists, getLoadableInventoryItems } = useShoppingListApi()
const { getCalendarEvents, getCalendarNotes } = useCalendarApi()
const { formatTime } = useDateFormat()
const { run, isPendingEntity } = useUndoableAction()
const { markReady: markSplashReady } = useSplashScreen()
const inventorySocket = useInventorySocket()
const presence = usePresenceSocket()
const presentMembers = presence.members

// --- State -------------------------------------------------------------------------------

type HomeContext = 'atHome' | 'shopping'

interface HomeTab {
  key: HomeContext
  label: string
  icon: string
  count: number
  countClass: string
}

/**
 * Which context the reader last picked. Only a preference: what is actually shown is
 * `activeContext`, which will not point at a context that has nothing in it.
 */
const context = ref<HomeContext>('atHome')
const products = ref<DetailedProductInfo[]>([])
const lists = ref<ShoppingListInfo[]>([])
const events = ref<CalendarEventInfo[]>([])
const notes = ref<CalendarNoteInfo[]>([])
const loadable = ref<LoadableShoppingListItemInfo[]>([])
const isLoading = ref(false)
// First load draws skeletons; a refetch (pull, socket event, reconnect) leaves the rows in place
// so the screen does not blink on every change — the same rule the inventory grid follows.
const hasLoaded = ref(false)
const busyItems = ref<Record<string, 'consume' | 'discard'>>({})

const isNoteDrawerOpen = ref(false)
const editingNote = ref<CalendarNoteInfo | null>(null)
const noteDrawerDate = ref(dayKey(new Date()))

// --- Derived -----------------------------------------------------------------------------

const atHomeItems = computed<AtHomeItem[]>(() => buildAtHomeItems(products.value))
const awaitingPutAway = computed(() => countAwaitingPutAway(loadable.value))
const shoppingRows = computed<ShoppingRow[]>(() => buildShoppingRows(lists.value, events.value))
const todayAgenda = computed(() => buildTodayAgenda(events.value))
// Three is what fits above the fold next to everything else; the rest are on the calendar, which
// is the screen for "the whole week" and is linked directly below this block.
const pinnedNotes = computed(() => filterPinnedNotes(notes.value).slice(0, 3))

const atHomeCount = computed(() => atHomeItems.value.length + (awaitingPutAway.value > 0 ? 1 : 0))

const tabs = computed<HomeTab[]>(() => [
  {
    key: 'atHome' as const,
    label: t('pages.household.atHome.tab'),
    icon: 'i-lucide-home',
    count: atHomeCount.value,
    countClass: atHomeItems.value.some(item => item.level === 'expired')
      ? 'bg-error text-white'
      : 'bg-warning text-white'
  },
  {
    key: 'shopping' as const,
    label: t('pages.household.shopping.tab'),
    icon: 'i-lucide-shopping-cart',
    count: shoppingRows.value.length,
    countClass: shoppingRows.value.some(row => row.overdue)
      ? 'bg-error text-white'
      : 'bg-elevated text-muted'
  }
])

/**
 * What the block shows — see `pickHomeContext`, which is where the three cases are decided (and
 * unit-tested). Derived rather than watched: a watcher writing `context` back would fight the
 * reader's next tap to return to a context that has just filled up again.
 */
const contextChoice = computed(() => pickHomeContext(atHomeCount.value, shoppingRows.value.length, context.value))
const activeContext = computed<HomeContext | null>(() => contextChoice.value.active)
const showContextSwitcher = computed(() => contextChoice.value.showSwitcher)

/** The active context's tab, for the heading shown in place of a one-position switcher. */
const activeTab = computed(() => tabs.value.find(tab => tab.key === activeContext.value))

const isToday = (date: string) => date === dayKey(new Date())

/** "Today" / "Tomorrow" / the weekday — a pinned note is never further out than a glance. */
const noteDayLabel = (date: string) => {
  if (isToday(date)) return t('pages.household.notes.today')

  const tomorrow = new Date()
  tomorrow.setDate(tomorrow.getDate() + 1)
  if (date === dayKey(tomorrow)) return t('pages.household.notes.tomorrow')

  return new Intl.DateTimeFormat(locale.value, { weekday: 'short' }).format(new Date(`${date}T00:00:00`))
}

/**
 * The agenda's colour bar. An external calendar brings its own colour (the only event type the
 * server ever colours); an automation is the app's own doing and takes the brand colour.
 */
const agendaColor = (event: CalendarEventInfo) =>
  event.eventType === CalendarEventType.ExternalCalendar
    ? (event.color ?? 'var(--ui-primary)')
    : 'var(--ui-primary)'

// --- Loading -----------------------------------------------------------------------------

const isoDay = (offsetDays: number) => {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  return dayKey(date)
}

const load = async () => {
  isLoading.value = true

  try {
    // One round of parallel calls, not a waterfall: every block on this screen is independent,
    // and the slowest of five is a much better first paint than the sum of five.
    const [productsRes, listsRes, eventsRes, notesRes, loadableRes] = await Promise.all([
      getDetailedProducts({ returnAll: true }),
      getShoppingLists({ returnAll: true }),
      getCalendarEvents(isoDay(0), isoDay(SHOPPING_HORIZON_DAYS)),
      getCalendarNotes(isoDay(0), isoDay(60)),
      getLoadableInventoryItems({ returnAll: true })
    ])

    if (productsRes.success && productsRes.data) products.value = productsRes.data.items ?? []
    if (listsRes.success && listsRes.data) lists.value = listsRes.data.items ?? []
    if (eventsRes.success && eventsRes.data) events.value = eventsRes.data
    if (notesRes.success && notesRes.data) notes.value = notesRes.data
    if (loadableRes.success && loadableRes.data) loadable.value = loadableRes.data.items ?? []

    hasLoaded.value = true
  }
  catch (error) {
    console.error('[Home] Failed to load', error)
  }
  finally {
    isLoading.value = false
    // This is the screen a normal relaunch lands on, so it owns the splash dismissal — the same
    // job the calendar page used to do when it was the first tab.
    markSplashReady()
  }
}

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(() => load())

// --- Actions -----------------------------------------------------------------------------

const setBusy = (publicId: string, state: 'consume' | 'discard' | null) => {
  busyItems.value = state
    ? { ...busyItems.value, [publicId]: state }
    : Object.fromEntries(Object.entries(busyItems.value).filter(([id]) => id !== publicId))
}

/** Drop one item out of the local grid without refetching the whole inventory for it. */
const removeItemLocally = (publicId: string) => {
  products.value = products.value.map(product => ({
    ...product,
    inventoryItems: (product.inventoryItems ?? []).filter(item => item.publicId !== publicId)
  }))
}

/**
 * "Used it" consumes the whole remaining quantity, because that is what the row asserts: this
 * carton is gone. A partial amount is a different question and belongs in the inventory drawer,
 * which has the stepper for it.
 *
 * Deliberately a round trip with a spinner rather than an undo toast: consumption is a relative
 * delta, and the undo queue's same-entity replacement would silently swallow one of two rapid
 * decrements. That reasoning is spelled out in `InventoryItemRow.vue` — this page follows it
 * rather than re-deciding it.
 */
const consumeItem = async (item: AtHomeItem) => {
  setBusy(item.publicId, 'consume')

  try {
    const response = await consumeInventoryItem(item.publicId, { quantity: item.quantity })
    if (response.success) {
      removeItemLocally(item.publicId)
    }
  }
  catch {
    toast.add({
      title: t('toast.error'),
      description: t('pages.household.atHome.consumeFailed'),
      color: 'error',
      icon: 'i-heroicons-x-circle'
    })
  }
  finally {
    setBusy(item.publicId, null)
  }
}

/**
 * "Threw it out" removes the stock, optimistically and with the app's undo window — the same
 * shape as deleting from the inventory drawer, because it is the same deletion and a mis-tap on
 * a small row is exactly what that window is for.
 */
const discardItem = (item: AtHomeItem) => {
  const snapshot = products.value

  run({
    entityIds: [item.publicId],
    kind: 'delete',
    label: t('undo.item.delete', { name: item.productName }),
    apply: () => removeItemLocally(item.publicId),
    revert: () => { products.value = snapshot },
    commit: () => deleteInventoryItem(item.publicId)
  })
}

const startShopping = (row: ShoppingRow) =>
  navigateTo({ path: '/shopping-lists', query: { shop: row.listPublicId } })

const editNote = (note: CalendarNoteInfo) => {
  editingNote.value = note
  noteDrawerDate.value = note.date
  isNoteDrawerOpen.value = true
}

const createNote = () => {
  editingNote.value = null
  noteDrawerDate.value = dayKey(new Date())
  isNoteDrawerOpen.value = true
}

const handleNoteSaved = async () => {
  const response = await getCalendarNotes(isoDay(0), isoDay(60))
  if (response.success && response.data) notes.value = response.data
}

// --- Chrome ------------------------------------------------------------------------------

usePageHeader(() => ({
  icon: 'i-lucide-house',
  title: t('pages.household.greeting', { name: authStore.user?.displayName ?? '' }),
  subtitle: new Intl.DateTimeFormat(locale.value, { weekday: 'long', month: 'long', day: 'numeric' }).format(new Date()),
  hasSubtitle: true
}))

useFabActions(() => [
  {
    label: t('pages.household.fab.note'),
    icon: 'i-lucide-sticky-note',
    description: t('pages.household.fab.noteDescription'),
    handler: () => createNote()
  },
  {
    label: t('pages.household.fab.inventory'),
    icon: 'i-lucide-package-plus',
    description: t('pages.household.fab.inventoryDescription'),
    handler: () => navigateTo('/products?action=add')
  },
  {
    label: t('pages.household.fab.listItem'),
    icon: 'i-lucide-list-plus',
    description: t('pages.household.fab.listItemDescription'),
    handler: () => navigateTo('/shopping-lists?action=add')
  }
])

// --- Realtime ----------------------------------------------------------------------------

// A stock change elsewhere (someone else's phone, an automation, the shopping-mode tick) can add
// or clear a row here. The payloads are per item and this screen shows a filtered projection of
// them, so it refetches rather than patching — debounced, because a shop trip lands a burst of
// them and five reloads in two seconds is no more correct than one.
let refreshTimer: ReturnType<typeof setTimeout> | null = null

const scheduleRefresh = (publicId?: string) => {
  // An entity inside its undo window must not be resurrected by a refetch that raced the commit.
  if (publicId && isPendingEntity(publicId)) return

  if (refreshTimer) clearTimeout(refreshTimer)
  refreshTimer = setTimeout(() => { refreshTimer = null; void load() }, 400)
}

const onInventoryUpserted = (payload: { item?: { publicId?: string } }) => scheduleRefresh(payload?.item?.publicId)
const onInventoryDeleted = (payload: { publicId?: string }) => scheduleRefresh(payload?.publicId)
const onReconnected = () => scheduleRefresh()

onMounted(async () => {
  await load()

  inventorySocket.on('InventoryUpserted', onInventoryUpserted as never)
  inventorySocket.on('InventoryDeleted', onInventoryDeleted as never)
  inventorySocket.onReconnected(onReconnected)
  void inventorySocket.ensureConnected().catch(() => { /* the bar reports it */ })

  // Presence is the one thing on this screen with no REST fallback: if the socket cannot open,
  // the strip simply does not render, which is the honest answer to "who is around".
  void presence.join().catch(() => { /* no roster, no strip */ })
})

onBeforeUnmount(() => {
  if (refreshTimer) clearTimeout(refreshTimer)
  inventorySocket.off('InventoryUpserted', onInventoryUpserted as never)
  inventorySocket.off('InventoryDeleted', onInventoryDeleted as never)
  inventorySocket.offReconnected(onReconnected)
})
</script>
