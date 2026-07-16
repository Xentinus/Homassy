<template>
  <div class="flex flex-col h-[calc(100dvh-var(--app-header-height)-1rem-8rem)] overflow-hidden lg:block lg:h-auto lg:overflow-visible lg:pb-4">
    <!-- Desktop: 2-column (calendar left, events right); Mobile: stacked -->
    <div class="flex flex-1 flex-col min-h-0 lg:flex-none lg:grid lg:grid-cols-2 lg:gap-4 lg:items-start">

      <!-- LEFT: Calendar + Legend (sticky on desktop) -->
      <div class="shrink-0 lg:sticky lg:top-4">
        <!-- Calendar card -->
        <div class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 overflow-hidden">
          <!-- Toolbar -->
          <div class="flex items-center justify-between px-3 py-2 border-b border-gray-200 dark:border-gray-700">
            <div class="flex items-center gap-1">
              <UButton
                icon="i-lucide-chevron-left"
                size="xs"
                variant="ghost"
                color="neutral"
                @click="prevWeek"
              />
              <span class="font-semibold text-sm min-w-[150px] text-center select-none">{{ weekTitle }}</span>
              <UButton
                icon="i-lucide-chevron-right"
                size="xs"
                variant="ghost"
                color="neutral"
                @click="nextWeek"
              />
            </div>
            <div class="flex items-center gap-2">
              <UButton
                v-if="isLoading"
                size="xs"
                variant="ghost"
                color="neutral"
                loading
                disabled
              />
              <UButton size="xs" variant="outline" color="neutral" @click="goToToday">
                {{ t('pages.calendar.today') }}
              </UButton>
            </div>
          </div>

          <!-- Day-of-week headers -->
          <div class="grid grid-cols-7 border-b border-gray-200 dark:border-gray-700">
            <div
              v-for="d in dayHeaders"
              :key="d"
              class="text-center text-xs font-medium text-gray-500 dark:text-gray-400 py-1.5"
            >
              {{ d }}
            </div>
          </div>

          <!-- Calendar grid -->
          <div class="grid grid-cols-7 border-l border-gray-200 dark:border-gray-700">
            <div
              v-for="(cell, idx) in calendarCells"
              :key="idx"
              class="border-r border-b border-gray-200 dark:border-gray-700 p-0.5 min-h-[38px] sm:min-h-[52px] cursor-pointer select-none"
              :class="selectedDay === cell.dateStr ? 'ring-2 ring-inset ring-primary-500' : ''"
              @click="selectDay(cell.dateStr)"
            >
              <!-- Date row: number + activity badge -->
              <div class="flex items-center justify-between mb-0.5">
                <div
                  class="text-xs font-medium w-5 h-5 flex items-center justify-center leading-none"
                  :class="cell.isToday
                    ? 'rounded-full bg-primary-500 text-white'
                    : 'text-gray-900 dark:text-gray-100'"
                >
                  {{ cell.day }}
                </div>
                <div
                  v-if="cell.activityCount > 0"
                  class="w-4 h-4 rounded-full bg-gray-400 dark:bg-gray-500 text-white text-[9px] font-bold flex items-center justify-center leading-none cursor-pointer"
                  @click.stop="selectDay(cell.dateStr)"
                >
                  {{ cell.activityCount > 9 ? '9+' : cell.activityCount }}
                </div>
              </div>

              <!-- Mobile: colored dots -->
              <div class="flex flex-wrap gap-0.5 sm:hidden">
                <span
                  v-for="(ev, ei) in cell.events"
                  :key="ei"
                  class="w-1.5 h-1.5 rounded-full"
                  :class="ev.eventType !== CalendarEventType.ExternalCalendar ? dotClass(ev.eventType) : ''"
                  :style="ev.eventType === CalendarEventType.ExternalCalendar ? { backgroundColor: ev.color ?? '#3B82F6' } : {}"
                  :title="ev.title"
                />
              </div>

              <!-- Desktop: text chips -->
              <div class="hidden sm:block space-y-0.5">
                <div
                  v-for="(ev, ei) in cell.events"
                  :key="ei"
                  class="text-xs rounded px-1 py-0.5 truncate leading-tight"
                  :class="ev.eventType !== CalendarEventType.ExternalCalendar ? chipClass(ev.eventType) : 'text-white'"
                  :style="ev.eventType === CalendarEventType.ExternalCalendar ? { backgroundColor: ev.color ?? '#3B82F6' } : {}"
                >
                  {{ ev.title }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Legend -->
        <div class="flex flex-wrap gap-3 mt-3 px-1">
          <div v-for="type in legendTypes" :key="type.key" class="flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full" :class="type.dot" />
            <span class="text-xs text-gray-600 dark:text-gray-400">{{ t(`pages.calendar.eventTypes.${type.key}`) }}</span>
          </div>
          <div v-for="cal in externalCalendars" :key="cal.publicId" class="flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full" :style="{ backgroundColor: cal.color }" />
            <span class="text-xs text-gray-600 dark:text-gray-400">{{ cal.name }}</span>
          </div>
        </div>
      </div>

      <!-- RIGHT: Day events panel -->
      <div class="mt-3 flex flex-col flex-1 min-h-0 lg:mt-0 lg:block lg:flex-none lg:sticky lg:top-4">
        <!-- Day panel -->
        <div v-if="selectedDay" class="flex flex-col flex-1 min-h-0 lg:block">
          <!-- Panel header — divider line sits directly above the scrolling cards -->
          <div class="mb-2 px-1 pb-2 border-b border-gray-200 dark:border-gray-800 shrink-0">
            <span class="text-sm font-semibold text-gray-800 dark:text-gray-100">
              {{ formatDate(selectedDay) }}
            </span>
          </div>

            <!-- No events -->
            <div
              v-if="selectedDayItems.length === 0"
              class="text-sm text-gray-400 dark:text-gray-500 text-center py-6"
            >
              {{ t('pages.calendar.noEventsOnDay') }}
            </div>

            <!-- Events list (own scroll container) -->
            <div
              v-else
              ref="scrollContainer"
              class="space-y-2 overflow-y-auto flex-1 min-h-0 lg:flex-none lg:max-h-[calc(100vh-8rem)]"
            >
              <template v-for="(item, idx) in visibleItems" :key="item.kind + '-' + item.data.publicId + '-' + idx">
                <CalendarEventCard
                  v-if="item.kind === 'event'"
                  :title="item.data.title"
                  :event-type="item.data.eventType"
                  :detail="item.data.detail"
                  :color="item.data.color"
                />
                <CalendarActivityCard
                  v-else-if="item.kind === 'activity'"
                  :activity-type="item.data.activityType"
                  :user-name="item.data.userName"
                  :record-name="item.data.recordName"
                  :timestamp="item.data.timestamp"
                />
              </template>

              <!-- Skeleton while loading more -->
              <template v-if="isLoadingMore">
                <USkeleton v-for="i in 2" :key="i" class="h-[60px] w-full rounded-xl" />
              </template>

              <!-- Sentinel for IntersectionObserver -->
              <div ref="sentinel" class="h-px" />
            </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { CalendarEventType } from '~/types/calendar'
import type { CalendarEventInfo } from '~/types/calendar'
import type { ExternalCalendarResponse } from '~/types/externalCalendar'
import type { ActivityType, ActivityInfo } from '~/types/activity'
import { useAuthStore } from '~/stores/auth'

definePageMeta({ layout: 'auth' })

const { t, locale } = useI18n()
const { getCalendarEvents } = useCalendarApi()
const { getActivities } = useUserApi()
const { getExternalCalendars } = useExternalCalendarApi()
const authStore = useAuthStore()

const greetingName = computed(() => authStore.user?.displayName || authStore.user?.name || '')

// Persistent header (auth layout) — greeting is async (waits for the user to load).
usePageHeader(() => ({
  icon: 'i-lucide-calendar',
  title: authStore.user ? t('pages.calendar.greeting', { name: greetingName.value }) : undefined,
  loading: !authStore.user
}))

interface CalEvent {
  publicId: string
  title: string
  eventType: CalendarEventType
  dateStr: string
  endStr: string | null
  startAt: string
  detail: string | null
  relatedPublicId: string | null
  color: string | null
  isAllDay: boolean
}

interface CalActivity {
  publicId: string
  activityType: ActivityType
  userName: string
  recordName: string
  timestamp: string
  dateStr: string
}

type DayItem =
  | { kind: 'event'; data: CalEvent }
  | { kind: 'activity'; data: CalActivity }

const toLocalDate = (date: Date): string => {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

const currentDate = ref(new Date())
const isLoading = ref(false)
const calendarEvents = ref<CalEvent[]>([])
const calendarActivities = ref<CalActivity[]>([])
const externalCalendars = ref<ExternalCalendarResponse[]>([])

const selectedDay = ref<string | null>(toLocalDate(new Date()))
const visibleCount = ref(5)
const isLoadingMore = ref(false)
const sentinel = ref<HTMLElement | null>(null)
const scrollContainer = ref<HTMLElement | null>(null)

const todayStr = computed(() => toLocalDate(new Date()))

// Monday on/before currentDate — anchors the visible week (Monday-first).
const weekStart = computed(() => {
  const d = new Date(currentDate.value)
  const dow = d.getDay()
  const offset = dow === 0 ? 6 : dow - 1
  d.setHours(0, 0, 0, 0)
  d.setDate(d.getDate() - offset)
  return d
})

const weekTitle = computed(() => {
  const loc = locale.value
  const start = weekStart.value
  const end = new Date(start)
  end.setDate(end.getDate() + 6)

  const sameYear = start.getFullYear() === end.getFullYear()
  const sameMonth = sameYear && start.getMonth() === end.getMonth()
  const year = end.toLocaleDateString(loc, { year: 'numeric' })

  // Compose from well-formed single-/multi-field parts; avoid day+year-only
  // option combos, which Intl renders with a literal "(day: N)" artifact.
  if (sameMonth) {
    const startPart = start.toLocaleDateString(loc, { month: 'short', day: 'numeric' })
    const endDay = end.toLocaleDateString(loc, { day: 'numeric' })
    return `${startPart} – ${endDay}, ${year}`
  }
  if (sameYear) {
    const startPart = start.toLocaleDateString(loc, { month: 'short', day: 'numeric' })
    const endPart = end.toLocaleDateString(loc, { month: 'short', day: 'numeric' })
    return `${startPart} – ${endPart}, ${year}`
  }
  const opts = { month: 'short', day: 'numeric', year: 'numeric' } as const
  return `${start.toLocaleDateString(loc, opts)} – ${end.toLocaleDateString(loc, opts)}`
})

const dayHeaders = computed(() => {
  const base = new Date(2024, 0, 1) // Monday Jan 1 2024
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(base)
    d.setDate(d.getDate() + i)
    return d.toLocaleDateString(locale.value, { weekday: 'short' })
  })
})

// Upper bound on the number of days a single event may span, so a malformed
// feed (e.g. an event ending years after it starts) can't blow up the buckets.
const MAX_SPAN_DAYS = 370

// All `YYYY-MM-DD` days an event covers. Single-day events return just their
// start day; multi-day events are listed on every day they span.
const eventDayKeys = (ev: CalEvent): string[] => {
  const startKey = ev.dateStr
  if (!ev.endStr) return [startKey]

  const endKey = ev.endStr.split('T')[0] ?? ev.endStr
  if (endKey <= startKey) return [startKey]

  const parse = (key: string): Date | null => {
    const [y, m, d] = key.split('-').map(Number)
    if (!y || !m || !d) return null
    return new Date(y, m - 1, d)
  }

  const start = parse(startKey)
  const end = parse(endKey)
  if (!start || !end) return [startKey]

  // iCal all-day events use an exclusive DTEND (midnight after the last day),
  // so the last visible day is end - 1; timed events end on the end date itself.
  if (ev.isAllDay) end.setDate(end.getDate() - 1)
  if (end <= start) return [startKey]

  const keys: string[] = []
  const cur = new Date(start)
  while (cur <= end && keys.length < MAX_SPAN_DAYS) {
    keys.push(toLocalDate(cur))
    cur.setDate(cur.getDate() + 1)
  }
  return keys
}

const eventsByDate = computed(() => {
  const map: Record<string, CalEvent[]> = {}
  for (const ev of calendarEvents.value) {
    for (const key of eventDayKeys(ev)) {
      ;(map[key] ??= []).push(ev)
    }
  }
  return map
})

const activitiesByDate = computed(() => {
  const map: Record<string, CalActivity[]> = {}
  for (const a of calendarActivities.value) {
    ;(map[a.dateStr] ??= []).push(a)
  }
  return map
})

const calendarCells = computed(() => {
  const cells = []
  const cur = new Date(weekStart.value)
  for (let i = 0; i < 7; i++) {
    const key = toLocalDate(cur)
    cells.push({
      date: new Date(cur),
      day: cur.getDate(),
      isToday: key === todayStr.value,
      dateStr: key,
      events: eventsByDate.value[key] ?? [],
      activityCount: (activitiesByDate.value[key] ?? []).length
    })
    cur.setDate(cur.getDate() + 1)
  }
  return cells
})

const selectedDayItems = computed((): DayItem[] => {
  if (!selectedDay.value) return []

  const events: DayItem[] = (eventsByDate.value[selectedDay.value] ?? [])
    .map(e => ({ kind: 'event' as const, data: e }))

  const activities: DayItem[] = (activitiesByDate.value[selectedDay.value] ?? [])
    .map(a => ({ kind: 'activity' as const, data: a }))

  return [...events, ...activities].sort((a, b) => {
    const aAllDay = a.kind === 'event' && a.data.isAllDay
    const bAllDay = b.kind === 'event' && b.data.isAllDay
    if (aAllDay !== bAllDay) return aAllDay ? -1 : 1
    const ta = a.kind === 'event' ? a.data.startAt : a.data.timestamp
    const tb = b.kind === 'event' ? b.data.startAt : b.data.timestamp
    return tb.localeCompare(ta)
  })
})

const visibleItems = computed(() =>
  selectedDayItems.value.slice(0, visibleCount.value)
)

const hasMore = computed(() =>
  visibleCount.value < selectedDayItems.value.length
)

const selectDay = (dateStr: string) => {
  if (selectedDay.value === dateStr) return
  selectedDay.value = dateStr
  visibleCount.value = 5
}

watch(selectedDay, () => {
  visibleCount.value = 5
  isLoadingMore.value = false
})

let observer: IntersectionObserver | null = null

const setupObserver = () => {
  observer?.disconnect()
  if (!sentinel.value) return

  observer = new IntersectionObserver(
    (entries) => {
      if (entries[0].isIntersecting && hasMore.value && !isLoadingMore.value) {
        isLoadingMore.value = true
        setTimeout(() => {
          visibleCount.value += 5
          isLoadingMore.value = false
        }, 350)
      }
    },
    { root: scrollContainer.value ?? null, rootMargin: '50px', threshold: 0.1 }
  )
  observer.observe(sentinel.value)
}

watch(sentinel, setupObserver)
onUnmounted(() => observer?.disconnect())

const mapEvents = (items: CalendarEventInfo[]): CalEvent[] =>
  items.map(item => ({
    publicId: item.publicId,
    title: item.title,
    eventType: item.eventType,
    dateStr: item.start.split('T')[0] ?? item.start,
    endStr: item.end ?? null,
    startAt: item.start,
    detail: item.detail,
    relatedPublicId: item.relatedEntityPublicId,
    color: item.color ?? null,
    isAllDay: item.isAllDay ?? false
  }))

const mapActivities = (items: ActivityInfo[]): CalActivity[] =>
  items.map(a => ({
    publicId: a.publicId,
    activityType: a.activityType,
    userName: a.userName,
    recordName: a.recordName,
    timestamp: a.timestamp,
    dateStr: a.timestamp.split('T')[0] ?? a.timestamp
  }))

const loadEvents = async () => {
  isLoading.value = true
  try {
    const weekEnd = new Date(weekStart.value)
    weekEnd.setDate(weekEnd.getDate() + 6)
    const startStr = toLocalDate(weekStart.value)
    const endStr = toLocalDate(weekEnd)

    const [eventsRes, activitiesRes, calendarsRes] = await Promise.all([
      getCalendarEvents(startStr, endStr),
      getActivities({ startDate: startStr, endDate: endStr, returnAll: true }),
      getExternalCalendars()
    ])

    if (eventsRes.success && eventsRes.data)
      calendarEvents.value = mapEvents(eventsRes.data)
    if (activitiesRes.success && activitiesRes.data)
      calendarActivities.value = mapActivities(activitiesRes.data.items)
    if (calendarsRes.success && calendarsRes.data)
      externalCalendars.value = calendarsRes.data.filter(c => c.isEnabled)
  } finally {
    isLoading.value = false
  }
}

watch(currentDate, () => {
  const weekEnd = new Date(weekStart.value)
  weekEnd.setDate(weekEnd.getDate() + 6)
  const startKey = toLocalDate(weekStart.value)
  const endKey = toLocalDate(weekEnd)
  const todayInWeek = todayStr.value >= startKey && todayStr.value <= endKey
  selectedDay.value = todayInWeek ? todayStr.value : startKey
  loadEvents()
})
onMounted(loadEvents)

const prevWeek = () => {
  const d = new Date(currentDate.value)
  d.setDate(d.getDate() - 7)
  currentDate.value = d
}

const nextWeek = () => {
  const d = new Date(currentDate.value)
  d.setDate(d.getDate() + 7)
  currentDate.value = d
}

const goToToday = () => {
  currentDate.value = new Date()
}

const dotClass = (type: CalendarEventType): string => {
  switch (type) {
    case CalendarEventType.InventoryExpiration: return 'bg-red-500'
    case CalendarEventType.AutomationExecution: return 'bg-blue-500'
    case CalendarEventType.ShoppingListDeadline: return 'bg-amber-500'
    default: return 'bg-gray-400'
  }
}

const chipClass = (type: CalendarEventType): string => {
  switch (type) {
    case CalendarEventType.InventoryExpiration:
      return 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300'
    case CalendarEventType.AutomationExecution:
      return 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300'
    case CalendarEventType.ShoppingListDeadline:
      return 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300'
    default:
      return 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300'
  }
}

const formatDate = (dateStr: string): string =>
  new Date(dateStr).toLocaleDateString(locale.value, {
    year: 'numeric', month: 'long', day: 'numeric'
  })

const legendTypes = [
  { key: 'inventoryExpiration', dot: 'bg-red-500' },
  { key: 'automationExecution', dot: 'bg-blue-500' },
  { key: 'shoppingListDeadline', dot: 'bg-amber-500' },
  { key: 'activity', dot: 'bg-gray-400' }
]
</script>
