<template>
  <div class="flex flex-col h-[calc(100dvh-8rem)] overflow-hidden lg:block lg:h-auto lg:overflow-visible lg:pb-4">
    <!-- Page header (capped with a divider; pinned on desktop, always visible on mobile) -->
    <div class="mb-4 pt-4 pb-3 shrink-0 border-b border-gray-200 dark:border-gray-800 lg:sticky lg:top-0 lg:z-20 lg:bg-white lg:dark:bg-gray-900">
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-calendar" class="h-7 w-7 text-primary-500 shrink-0" />
        <div>
          <h1 class="text-xl font-semibold">{{ t('pages.calendar.greeting', { name: greetingName }) }}</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">{{ t('pages.calendar.greetingSubtitle') }}</p>
        </div>
      </div>
    </div>

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
                @click="prevMonth"
              />
              <span class="font-semibold text-sm min-w-[130px] text-center select-none">{{ monthTitle }}</span>
              <UButton
                icon="i-lucide-chevron-right"
                size="xs"
                variant="ghost"
                color="neutral"
                @click="nextMonth"
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
              :class="[
                !cell.isCurrentMonth ? 'bg-gray-50 dark:bg-gray-800/60' : '',
                selectedDay === cell.dateStr ? 'ring-2 ring-inset ring-primary-500' : ''
              ]"
              @click="selectDay(cell.dateStr)"
            >
              <!-- Date row: number + activity badge -->
              <div class="flex items-center justify-between mb-0.5">
                <div
                  class="text-xs font-medium w-5 h-5 flex items-center justify-center leading-none"
                  :class="cell.isToday
                    ? 'rounded-full bg-primary-500 text-white'
                    : cell.isCurrentMonth
                      ? 'text-gray-900 dark:text-gray-100'
                      : 'text-gray-400 dark:text-gray-600'"
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
                  :class="dotClass(ev.eventType)"
                  :title="ev.title"
                />
              </div>

              <!-- Desktop: text chips -->
              <div class="hidden sm:block space-y-0.5">
                <div
                  v-for="(ev, ei) in cell.events"
                  :key="ei"
                  class="text-xs rounded px-1 py-0.5 truncate leading-tight"
                  :class="[chipClass(ev.eventType), ev.eventType !== CalendarEventType.InventoryExpiration ? 'cursor-pointer' : 'cursor-default']"
                  @click.stop="openEventModal(ev)"
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
              <template v-for="item in visibleItems" :key="item.kind + '-' + item.data.publicId">
                <CalendarEventCard
                  v-if="item.kind === 'event'"
                  :title="item.data.title"
                  :event-type="item.data.eventType"
                  :detail="item.data.detail"
                  @click="openEventModal(item.data)"
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

    <!-- Event detail modal -->
    <UModal :open="!!selectedEvent" @update:open="(val) => { if (!val) selectedEvent = null }">
      <template v-if="selectedEvent" #title>
        {{ selectedEvent.title }}
      </template>
      <template v-if="selectedEvent" #default>
        <div class="space-y-3 p-4">
          <div class="flex items-center gap-2">
            <span
              class="inline-block w-3 h-3 rounded-full"
              :class="dotClass(selectedEvent.eventType)"
            />
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ t(`pages.calendar.eventTypes.${eventTypeKey(selectedEvent.eventType)}`) }}
            </span>
          </div>
          <div class="text-sm text-gray-600 dark:text-gray-400">
            <span class="font-medium">{{ t('pages.calendar.date') }}:</span>
            {{ formatDate(selectedEvent.dateStr) }}
          </div>
          <div v-if="selectedEvent.detail" class="text-sm text-gray-600 dark:text-gray-400">
            <span class="font-medium">{{ t('pages.calendar.detail') }}:</span>
            {{ selectedEvent.detail }}
          </div>
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import { CalendarEventType } from '~/types/calendar'
import type { CalendarEventInfo } from '~/types/calendar'
import type { ActivityType, ActivityInfo } from '~/types/activity'
import { useAuthStore } from '~/stores/auth'

definePageMeta({ layout: 'auth' })

const { t, locale } = useI18n()
const { getCalendarEvents } = useCalendarApi()
const { getActivities } = useUserApi()
const authStore = useAuthStore()

const greetingName = computed(() => authStore.user?.displayName || authStore.user?.name || '')

interface CalEvent {
  publicId: string
  title: string
  eventType: CalendarEventType
  dateStr: string
  startAt: string
  detail: string | null
  relatedPublicId: string | null
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
const selectedEvent = ref<CalEvent | null>(null)

const selectedDay = ref<string | null>(toLocalDate(new Date()))
const visibleCount = ref(5)
const isLoadingMore = ref(false)
const sentinel = ref<HTMLElement | null>(null)
const scrollContainer = ref<HTMLElement | null>(null)

const todayStr = computed(() => toLocalDate(new Date()))

const monthTitle = computed(() =>
  currentDate.value.toLocaleDateString(locale.value, { month: 'long', year: 'numeric' })
)

const dayHeaders = computed(() => {
  const base = new Date(2024, 0, 1) // Monday Jan 1 2024
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(base)
    d.setDate(d.getDate() + i)
    return d.toLocaleDateString(locale.value, { weekday: 'short' })
  })
})

const eventsByDate = computed(() => {
  const map: Record<string, CalEvent[]> = {}
  for (const ev of calendarEvents.value) {
    ;(map[ev.dateStr] ??= []).push(ev)
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
  const year = currentDate.value.getFullYear()
  const month = currentDate.value.getMonth()
  const firstDay = new Date(year, month, 1)
  const dow = firstDay.getDay()
  const offset = dow === 0 ? 6 : dow - 1
  const start = new Date(firstDay)
  start.setDate(start.getDate() - offset)

  const cells = []
  const cur = new Date(start)
  for (let i = 0; i < 42; i++) {
    const key = toLocalDate(cur)
    cells.push({
      date: new Date(cur),
      day: cur.getDate(),
      isCurrentMonth: cur.getMonth() === month,
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

const openEventModal = (ev: CalEvent) => {
  if (ev.eventType !== CalendarEventType.InventoryExpiration) {
    selectedEvent.value = ev
  }
}

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
    startAt: item.start,
    detail: item.detail,
    relatedPublicId: item.relatedEntityPublicId
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
    const year = currentDate.value.getFullYear()
    const month = currentDate.value.getMonth()
    const startStr = toLocalDate(new Date(year, month, 1))
    const endStr = toLocalDate(new Date(year, month + 1, 0))

    const [eventsRes, activitiesRes] = await Promise.all([
      getCalendarEvents(startStr, endStr),
      getActivities({ startDate: startStr, endDate: endStr, returnAll: true })
    ])

    if (eventsRes.success && eventsRes.data)
      calendarEvents.value = mapEvents(eventsRes.data)
    if (activitiesRes.success && activitiesRes.data)
      calendarActivities.value = mapActivities(activitiesRes.data.items)
  } finally {
    isLoading.value = false
  }
}

watch(currentDate, () => {
  const today = new Date()
  const isSameMonth = currentDate.value.getFullYear() === today.getFullYear()
    && currentDate.value.getMonth() === today.getMonth()
  selectedDay.value = isSameMonth ? toLocalDate(today) : null
  loadEvents()
})
onMounted(loadEvents)

const prevMonth = () => {
  const d = new Date(currentDate.value)
  d.setDate(1)
  d.setMonth(d.getMonth() - 1)
  currentDate.value = d
}

const nextMonth = () => {
  const d = new Date(currentDate.value)
  d.setDate(1)
  d.setMonth(d.getMonth() + 1)
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

const eventTypeKey = (type: CalendarEventType): string => {
  switch (type) {
    case CalendarEventType.InventoryExpiration: return 'inventoryExpiration'
    case CalendarEventType.AutomationExecution: return 'automationExecution'
    case CalendarEventType.ShoppingListDeadline: return 'shoppingListDeadline'
    default: return 'inventoryExpiration'
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
