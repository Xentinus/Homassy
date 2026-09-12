<template>
  <div class="flex h-full w-full flex-col bg-default text-default select-none">
    <!-- Status strip. Not a real clock: a landing page that ticks is a landing page that
         re-renders forever. -->
    <div class="flex items-center justify-between px-4 pt-3 pb-1 text-[10px] font-medium text-muted">
      <span class="tabular-nums">9:41</span>
      <div class="flex items-center gap-1">
        <UIcon name="i-lucide-signal" mode="svg" class="h-3 w-3" />
        <UIcon name="i-lucide-wifi" mode="svg" class="h-3 w-3" />
        <UIcon name="i-lucide-battery-full" mode="svg" class="h-3 w-3" />
      </div>
    </div>

    <!-- Page header -->
    <div class="flex items-center gap-2 px-4 pb-3">
      <UIcon :name="screenMeta.icon" mode="svg" class="h-5 w-5 text-primary" />
      <p class="text-sm font-semibold leading-none">{{ screenMeta.title }}</p>
      <UIcon name="i-lucide-search" mode="svg" class="ml-auto h-4 w-4 text-muted" />
      <UIcon name="i-lucide-bell" mode="svg" class="h-4 w-4 text-muted" />
    </div>

    <div class="flex-1 overflow-hidden px-3">
      <!-- Inventory: the expiration ramp, which is the thing the app is actually for. -->
      <div v-if="screen === 'inventory'" class="grid grid-cols-2 gap-2">
        <div
          v-for="item in inventoryItems"
          :key="item.name"
          class="rounded-xl border p-2"
          :class="[item.tone.border, item.tone.surface]"
        >
          <div class="mb-2 flex h-10 items-center justify-center rounded-lg bg-elevated">
            <UIcon :name="item.icon" mode="svg" class="h-5 w-5 text-muted" />
          </div>
          <p class="truncate text-[11px] font-medium leading-tight">{{ item.name }}</p>
          <p class="truncate text-[9px] text-muted">{{ item.quantity }}</p>
          <p class="mt-1 flex items-center gap-1 text-[9px] font-medium" :class="item.tone.text">
            <UIcon :name="item.tone.icon" mode="svg" class="h-2.5 w-2.5" />
            {{ item.expiry }}
          </p>
        </div>
      </div>

      <!-- Shopping list: shared, and visibly changing under someone else's hands. -->
      <div v-else-if="screen === 'shopping'" class="space-y-2">
        <div class="flex items-center gap-2 rounded-full bg-primary-100 dark:bg-primary-500/15 px-3 py-1.5">
          <span class="relative flex h-2 w-2">
            <span class="absolute inline-flex h-full w-full rounded-full bg-primary-500 opacity-60" />
            <span class="relative inline-flex h-2 w-2 rounded-full bg-primary-500" />
          </span>
          <p class="truncate text-[10px] font-medium text-primary-700 dark:text-primary-300">
            {{ t('pages.home.preview.shopping.live') }}
          </p>
          <div class="ml-auto flex -space-x-1.5">
            <span
              v-for="member in members"
              :key="member.initials"
              class="flex h-4 w-4 items-center justify-center rounded-full text-[7px] font-bold text-white ring-1 ring-default"
              :style="{ backgroundColor: member.color }"
            >{{ member.initials }}</span>
          </div>
        </div>

        <div
          v-for="row in shoppingRows"
          :key="row.name"
          class="flex items-center gap-2 rounded-xl border border-default bg-default px-2.5 py-2"
        >
          <span
            class="flex h-4 w-4 shrink-0 items-center justify-center rounded-md border"
            :class="row.done ? 'border-primary-500 bg-primary-500' : 'border-accented'"
          >
            <UIcon v-if="row.done" name="i-lucide-check" mode="svg" class="h-3 w-3 text-white" />
          </span>
          <div class="min-w-0 flex-1">
            <p class="truncate text-[11px] font-medium leading-tight" :class="row.done ? 'text-muted line-through' : ''">
              {{ row.name }}
            </p>
            <p class="truncate text-[9px] text-muted">{{ row.quantity }}</p>
          </div>
          <span
            v-if="row.by"
            class="flex h-4 w-4 items-center justify-center rounded-full text-[7px] font-bold text-white"
            :style="{ backgroundColor: row.by.color }"
          >{{ row.by.initials }}</span>
        </div>
      </div>

      <!-- Scanner: viewfinder plus the product it just resolved. -->
      <div v-else-if="screen === 'scanner'" class="flex h-full flex-col">
        <div class="relative flex-1 overflow-hidden rounded-2xl bg-slate-900">
          <div class="absolute inset-0 opacity-40 bg-[radial-gradient(circle_at_50%_40%,rgb(100_116_139),transparent_65%)]" />
          <div class="absolute left-1/2 top-1/2 h-20 w-40 -translate-x-1/2 -translate-y-1/2">
            <span class="absolute left-0 top-0 h-4 w-4 border-l-2 border-t-2 border-primary-400 rounded-tl" />
            <span class="absolute right-0 top-0 h-4 w-4 border-r-2 border-t-2 border-primary-400 rounded-tr" />
            <span class="absolute bottom-0 left-0 h-4 w-4 border-b-2 border-l-2 border-primary-400 rounded-bl" />
            <span class="absolute bottom-0 right-0 h-4 w-4 border-b-2 border-r-2 border-primary-400 rounded-br" />
            <span class="absolute inset-x-2 top-1/2 h-px bg-primary-400/80" />
          </div>
          <p class="absolute inset-x-4 bottom-3 text-center text-[9px] text-white/70">
            {{ t('pages.home.preview.scanner.hint') }}
          </p>
        </div>

        <div class="mt-2 flex items-center gap-2 rounded-2xl border border-default bg-elevated p-2.5">
          <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-default">
            <UIcon name="i-lucide-milk" mode="svg" class="h-4 w-4 text-muted" />
          </div>
          <div class="min-w-0 flex-1">
            <p class="truncate text-[11px] font-semibold leading-tight">
              {{ t('pages.home.preview.scanner.productName') }}
            </p>
            <p class="truncate text-[9px] text-muted tabular-nums">5901234123457</p>
          </div>
          <span class="rounded-full bg-primary-500 px-2 py-1 text-[9px] font-semibold text-white">
            {{ t('pages.home.preview.scanner.add') }}
          </span>
        </div>
      </div>

      <!-- Calendar: expiries and deadlines on the month they fall in. -->
      <div v-else class="space-y-2">
        <p class="text-[11px] font-semibold">{{ t('pages.home.preview.calendar.month') }}</p>
        <div class="grid grid-cols-7 gap-1 text-center">
          <span v-for="day in weekdays" :key="day" class="text-[8px] font-medium text-muted">{{ day }}</span>
          <div
            v-for="cell in calendarCells"
            :key="cell.day"
            class="flex aspect-square flex-col items-center justify-center rounded-md text-[9px]"
            :class="cell.today ? 'bg-primary-500 font-semibold text-white' : 'bg-elevated/60'"
          >
            <span class="tabular-nums">{{ cell.day }}</span>
            <span v-if="cell.dot" class="mt-0.5 h-1 w-1 rounded-full" :class="cell.dot" />
          </div>
        </div>

        <div class="space-y-1.5 pt-1">
          <div
            v-for="event in calendarEvents"
            :key="event.label"
            class="flex items-center gap-2 rounded-lg border border-default bg-default px-2 py-1.5"
          >
            <span class="h-1.5 w-1.5 shrink-0 rounded-full" :class="event.dot" />
            <p class="truncate text-[10px] font-medium leading-tight">{{ event.label }}</p>
            <span class="ml-auto shrink-0 text-[9px] text-muted tabular-nums">{{ event.when }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Bottom navigation, with the nav badge the app really shows. -->
    <div class="mt-2 flex items-center justify-around border-t border-default px-3 pb-3 pt-2">
      <div
        v-for="tab in tabs"
        :key="tab.icon"
        class="relative flex flex-col items-center gap-0.5"
        :class="tab.active ? 'text-primary-600 dark:text-primary-400' : 'text-muted'"
      >
        <UIcon :name="tab.icon" mode="svg" class="h-4 w-4" />
        <span
          v-if="tab.badge"
          class="absolute -right-1.5 -top-1 flex h-3 min-w-3 items-center justify-center rounded-full bg-error px-0.5 text-[7px] font-bold leading-none text-white tabular-nums"
        >{{ tab.badge }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

/**
 * A rendered stand-in for one of the app's screens, for the landing page (#123).
 *
 * Drawn in markup rather than shipped as a bitmap, and deliberately so: it is built from the
 * app's own semantic tokens (`bg-default`, `text-warning`, the mocha primary, the expiration
 * ramp's colours), so it follows the visitor's light/dark theme by itself, costs no image
 * bytes, and cannot go stale against a redesign the way a PNG does. It has a fixed aspect
 * ratio inside `LandingDeviceFrame`, so there is nothing for it to shift.
 *
 * It is a likeness of the product, not a capture of it: the data in it is invented. When real
 * captures exist, `LandingDeviceFrame` already takes `light` / `dark` image sources — pass
 * those instead of this component and nothing else about the page changes.
 *
 * **Every icon here is `mode="svg"`.** Nuxt Icon's default renders an empty `<span>` on the
 * server and fills it in on the client, which on a server-rendered page is both a hydration
 * mismatch and an icon that arrives late; `svg` puts the real markup in the first response. The
 * rest of the app never hit this because its pages are behind the auth gate and effectively
 * client-rendered — this is the first page that is genuinely served from the server.
 */
const props = defineProps<{
  screen: 'inventory' | 'shopping' | 'scanner' | 'calendar'
}>()

const { t } = useI18n()
const { toneForLevel } = useExpirationStatus()

const screenMeta = computed(() => ({
  inventory: { icon: 'i-lucide-package', title: t('nav.products') },
  shopping: { icon: 'i-lucide-shopping-cart', title: t('nav.shoppingLists') },
  scanner: { icon: 'i-lucide-scan-barcode', title: t('pages.home.preview.scanner.title') },
  calendar: { icon: 'i-lucide-calendar', title: t('nav.calendar') }
}[props.screen]))

const inventoryItems = computed(() => [
  {
    name: t('pages.home.preview.inventory.items.milk'),
    quantity: '2 × 1 l',
    icon: 'i-lucide-milk',
    expiry: t('pages.home.preview.inventory.expiry.tomorrow'),
    tone: toneForLevel('critical')
  },
  {
    name: t('pages.home.preview.inventory.items.yogurt'),
    quantity: '4 × 150 g',
    icon: 'i-lucide-cup-soda',
    expiry: t('pages.home.preview.inventory.expiry.days', { count: 9 }),
    tone: toneForLevel('soon')
  },
  {
    name: t('pages.home.preview.inventory.items.rice'),
    quantity: '1 × 2 kg',
    icon: 'i-lucide-wheat',
    expiry: t('pages.home.preview.inventory.expiry.months', { count: 8 }),
    tone: toneForLevel('ok')
  },
  {
    name: t('pages.home.preview.inventory.items.coffee'),
    quantity: '3 × 250 g',
    icon: 'i-lucide-coffee',
    expiry: t('pages.home.preview.inventory.expiry.months', { count: 4 }),
    tone: toneForLevel('ok')
  },
  {
    name: t('pages.home.preview.inventory.items.pasta'),
    quantity: '2 × 500 g',
    icon: 'i-lucide-wheat',
    expiry: t('pages.home.preview.inventory.expiry.months', { count: 11 }),
    tone: toneForLevel('ok')
  },
  {
    name: t('pages.home.preview.inventory.items.butter'),
    quantity: '1 × 250 g',
    icon: 'i-lucide-croissant',
    expiry: t('pages.home.preview.inventory.expiry.days', { count: 12 }),
    tone: toneForLevel('soon')
  }
])

// Invented members. The colours are the identity colours the app assigns real ones.
const members = [
  { initials: 'A', color: '#B8956A' },
  { initials: 'K', color: '#6F8FA0' },
  { initials: 'M', color: '#8B7355' }
]

const shoppingRows = computed(() => [
  { name: t('pages.home.preview.shopping.items.bread'), quantity: '2 ×', done: true, by: members[1] },
  { name: t('pages.home.preview.shopping.items.eggs'), quantity: '10 ×', done: false, by: null },
  { name: t('pages.home.preview.shopping.items.tomato'), quantity: '500 g', done: false, by: null },
  { name: t('pages.home.preview.shopping.items.detergent'), quantity: '1 ×', done: false, by: members[2] },
  { name: t('pages.home.preview.shopping.items.coffee'), quantity: '250 g', done: true, by: members[1] },
  { name: t('pages.home.preview.shopping.items.apples'), quantity: '1 kg', done: false, by: null }
])

const weekdays = computed(() => t('pages.home.preview.calendar.weekdays').split(','))

const calendarCells = computed(() => Array.from({ length: 28 }, (_, index) => {
  const day = index + 1
  return {
    day,
    today: day === 12,
    dot: day === 13 ? 'bg-error' : day === 18 ? 'bg-warning' : day === 24 ? 'bg-primary-500' : null
  }
}))

const calendarEvents = computed(() => [
  { label: t('pages.home.preview.calendar.events.milk'), when: '13.', dot: 'bg-error' },
  { label: t('pages.home.preview.calendar.events.yogurt'), when: '18.', dot: 'bg-warning' },
  { label: t('pages.home.preview.calendar.events.shopping'), when: '24.', dot: 'bg-primary-500' }
])

const tabs = computed(() => [
  { icon: 'i-lucide-calendar', active: props.screen === 'calendar', badge: null },
  { icon: 'i-lucide-package', active: props.screen === 'inventory' || props.screen === 'scanner', badge: 3 },
  { icon: 'i-lucide-shopping-cart', active: props.screen === 'shopping', badge: null },
  { icon: 'i-lucide-user', active: false, badge: null }
])
</script>
