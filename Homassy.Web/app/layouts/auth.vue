<template>
  <UApp class="flex flex-col min-h-screen">
    <!-- Persistent page header — lives here (a sibling of the page slot) so it
         stays mounted across navigation, exactly like the bottom nav below. -->
    <AppHeader />

    <UMain
      class="flex-1 px-4 sm:px-6 lg:px-8 pb-32"
      :style="{ paddingTop: 'calc(var(--app-header-height, 5.5rem) + 1rem)' }"
    >
      <slot />
    </UMain>

    <nav class="fixed inset-x-4 bottom-4 z-50 max-w-2xl mx-auto">
      <!-- Dynamic add button, centred on the nav's top border -->
      <NavFab />

      <div
        ref="barRef"
        data-tour="nav-bar"
        class="relative flex items-stretch gap-1 rounded-2xl border border-primary-200 dark:border-primary-800 bg-default/95 backdrop-blur shadow-lg px-2 pt-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))]"
      >
        <!-- Sliding active indicator — glides between items and tracks the FAB reflow -->
        <div
          class="pointer-events-none absolute z-0 rounded-xl bg-primary-100 dark:bg-primary-500/15"
          :style="indicatorStyle"
        />

        <template v-for="(item, index) in navItems" :key="item.to">
          <!-- Centre slot for the '+' FAB — its width animates open/closed so the items slide apart -->
          <div
            v-if="index === centerIndex"
            class="shrink-0"
            :style="{ width: (mounted && hasFab) ? '4.5rem' : '0px', transition: `width ${NAV_DURATION}ms ${NAV_EASE}` }"
            aria-hidden="true"
          />
          <NuxtLink
            :to="item.to"
            :data-nav-index="index"
            :data-tour="item.tour"
            :aria-label="item.label"
            class="relative z-10 flex-1 flex flex-col items-center justify-center h-16 md:h-12 rounded-xl transition-colors duration-300 active:scale-95"
            :class="item.active ? 'text-primary-600 dark:text-primary-400 font-semibold' : 'text-gray-500 dark:text-gray-400 hover:text-primary-500'"
          >
            <div class="relative transition-transform duration-300" :class="item.active ? 'scale-110' : ''">
              <!-- Badge for expiration count -->
              <div
                v-if="item.badge"
                class="absolute -top-1.5 -right-1.5 z-10 flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full shadow-md"
                :class="item.badgeClass"
              >
                <!-- tabular-nums: the count changes under the mounted badge (a
                     product expires, an item goes overdue), and proportional
                     digits would resize the pill on every change. -->
                <span class="text-[10px] font-bold text-white leading-none tabular-nums">
                  {{ item.badge }}
                </span>
              </div>

              <div
                v-if="item.avatar"
                class="h-7 w-7 shrink-0"
              >
                <!-- Avatar is auth-store driven (client-only): render on the client
                     so SSR (user=null → "?") doesn't mismatch the hydrated initials. The
                     h-7 w-7 here only reserves the same footprint pre-hydration — the ring
                     itself is UserAvatar's own member-identity ring (publicId/identityColor),
                     not a hardcoded border: a wrapper's own border painted every user the same
                     colour regardless of who they were. See Item 1 of the fix branch. -->
                <ClientOnly>
                  <UserAvatar
                    :src="avatarSrc"
                    :name="avatarName"
                    :public-id="authStore.user?.publicId"
                    :identity-color="authStore.user?.identityColor"
                    :size="28"
                  />
                </ClientOnly>
              </div>
              <UIcon v-else :name="item.icon" class="h-6 w-6" />
            </div>
            <span class="mt-1 text-xs hidden md:block">{{ item.label }}</span>
          </NuxtLink>
        </template>
      </div>
    </nav>

    <!-- First-run spotlight tour (#98). Mounted here, not per page: the tour walks
         between two pages, and an overlay owned by a page would be unmounted
         underneath itself halfway through. It renders nothing until the tour runs. -->
    <ClientOnly>
      <OnboardingSpotlight />
    </ClientOnly>
  </UApp>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { useDebounceFn } from '@vueuse/core'
import { useAuthStore } from '~/stores/auth'

const route = useRoute()
const { t } = useI18n()
const { getExpirationCount } = useProductsApi()
const { getDeadlineCount } = useShoppingListApi()
const eventBus = useEventBus()
const { fabVisible } = useFab()
const authStore = useAuthStore()

// Profile nav item shows the user's avatar. UserAvatar owns the picture-or-initials
// fallback, so this layout only has to say whose avatar it is.
const avatarSrc = computed(() => authStore.user?.profilePictureUrl ?? undefined)
const avatarName = computed(() => authStore.user?.displayName || authStore.user?.name || '')

const expirationCount = ref(0)
const expiredCount = ref(0)
const deadlineCount = ref(0)
const { toneForLevel } = useExpirationStatus()
// The same count this layout badges the nav with also drives the installed app's
// icon badge and the browser tab title (#130) — one number, three surfaces.
const { setExpirationCount, resolveIconPermission } = useAppBadge()
// First-run spotlight tour (#98). The layout is where it starts from because it is the
// one component that outlives the tour's own navigation.
const { maybeAutoStart } = useOnboardingTour()

// Shared motion tokens for the nav — keep the sliding indicator and the FAB
// gap in lock-step so the pill tracks the items as they slide apart.
const NAV_DURATION = 340
const NAV_EASE = 'cubic-bezier(0.22, 1, 0.36, 1)'

// The FAB occupies the centre slot whenever the (debounced) FAB is visible, so the
// gap and the "+" animate in lock-step.
const hasFab = fabVisible

// Gate the FAB gap width on mount to avoid an SSR/client hydration mismatch: the active
// page's setup makes `fabVisible` true during SSR, but it starts false on the client, so
// SSR would render 4.5rem while the first client render expects 0px. With this flag both
// render 0px initially, then the gap animates open after hydration.
const mounted = ref(false)
onMounted(() => { mounted.value = true })

const fetchExpirationCount = async () => {
  try {
    const response = await getExpirationCount()
    if (response.success && response.data) {
      expirationCount.value = response.data.totalCount
      expiredCount.value = response.data.expiredCount
    }
  } catch (error) {
    console.error('Failed to fetch expiration count:', error)
    expirationCount.value = 0
    expiredCount.value = 0
  }
  setExpirationCount(expirationCount.value)
}

const fetchDeadlineCount = async () => {
  try {
    const response = await getDeadlineCount()
    if (response.success && response.data) {
      deadlineCount.value = response.data.totalCount
    }
  } catch (error) {
    console.error('Failed to fetch deadline count:', error)
    deadlineCount.value = 0
  }
}

// Debounce the fetch function with 500ms delay
const debouncedFetchExpirationCount = useDebounceFn(fetchExpirationCount, 500)
const debouncedFetchDeadlineCount = useDebounceFn(fetchDeadlineCount, 500)

// Handler for all inventory/product mutation events
const handleInventoryMutation = () => {
  debouncedFetchExpirationCount()
}

// Handler for all shopping list item mutation events
const handleShoppingListMutation = () => {
  debouncedFetchDeadlineCount()
}

// A preferences save can turn the icon badge on or off, so re-read the gate rather
// than waiting for the next app launch.
const handlePreferencesUpdated = () => {
  resolveIconPermission(true)
}

onMounted(() => {
  fetchExpirationCount()
  fetchDeadlineCount()
  // Whether the app-icon badge is allowed at all (#130) — one read per app load.
  resolveIconPermission()
  eventBus.on('notification-preferences:updated', handlePreferencesUpdated)

  // First-run tour (#98). It needs the user record to know whether this user has
  // already seen it, so it is also retried once the store fills in — the layout can
  // mount before the Kratos session resolves.
  maybeAutoStart()

  // Listen to all inventory and product mutation events
  eventBus.on('inventory:created', handleInventoryMutation)
  eventBus.on('inventory:updated', handleInventoryMutation)
  eventBus.on('inventory:deleted', handleInventoryMutation)
  eventBus.on('inventory:consumed', handleInventoryMutation)
  eventBus.on('inventory:split', handleInventoryMutation)
  eventBus.on('inventory:moved', handleInventoryMutation)
  eventBus.on('product:deleted', handleInventoryMutation)

  // Listen to all shopping list item mutation events
  eventBus.on('shopping-list-item:created', handleShoppingListMutation)
  eventBus.on('shopping-list-item:updated', handleShoppingListMutation)
  eventBus.on('shopping-list-item:deleted', handleShoppingListMutation)
  eventBus.on('shopping-list-item:purchased', handleShoppingListMutation)
  eventBus.on('shopping-list-item:restored', handleShoppingListMutation)

  // Place the indicator without animating in, then enable easing for later moves.
  nextTick(() => {
    measure()
    requestAnimationFrame(() => {
      indicatorAnimating.value = true
    })
  })
  window.addEventListener('resize', onResize)
})

onUnmounted(() => {
  // Clean up event listeners
  eventBus.off('notification-preferences:updated', handlePreferencesUpdated)
  eventBus.off('inventory:created', handleInventoryMutation)
  eventBus.off('inventory:updated', handleInventoryMutation)
  eventBus.off('inventory:deleted', handleInventoryMutation)
  eventBus.off('inventory:consumed', handleInventoryMutation)
  eventBus.off('inventory:split', handleInventoryMutation)
  eventBus.off('inventory:moved', handleInventoryMutation)
  eventBus.off('product:deleted', handleInventoryMutation)

  eventBus.off('shopping-list-item:created', handleShoppingListMutation)
  eventBus.off('shopping-list-item:updated', handleShoppingListMutation)
  eventBus.off('shopping-list-item:deleted', handleShoppingListMutation)
  eventBus.off('shopping-list-item:purchased', handleShoppingListMutation)
  eventBus.off('shopping-list-item:restored', handleShoppingListMutation)

  window.removeEventListener('resize', onResize)
  cancelAnimationFrame(rafId)
})

const navItems = computed(() => [
  {
    label: t('nav.calendar'),
    to: '/calendar',
    icon: 'i-lucide-calendar',
    // `data-tour` targets for the first-run spotlight tour (#98). The nav is the one
    // piece of chrome that is on screen everywhere, so most of the tour points at it.
    tour: 'nav-calendar',
    active: route.path.startsWith('/calendar')
  },
  {
    label: t('nav.products'),
    to: '/products',
    icon: 'i-lucide-package',
    tour: 'nav-products',
    active: route.path.startsWith('/products'),
    badge: expirationCount.value > 0 ? expirationCount.value : undefined,
    // Straight off the expiration ramp the cards use, so the badge and the cards cannot say
    // different things: red once something has actually expired, amber while everything is only
    // close to it. It used to be red either way.
    badgeClass: toneForLevel(expiredCount.value > 0 ? 'expired' : 'soon').badge
  },
  {
    label: t('nav.shoppingLists'),
    to: '/shopping-lists',
    icon: 'i-lucide-shopping-cart',
    tour: 'nav-shopping-lists',
    active: route.path.startsWith('/shopping-lists'),
    badge: deadlineCount.value > 0 ? deadlineCount.value : undefined,
    // An overdue shopping-list item is past its date, so it takes the ramp's expired tone.
    badgeClass: toneForLevel('expired').badge
  },
  {
    label: t('nav.profile'),
    to: '/profile',
    icon: 'i-lucide-user',
    tour: 'nav-profile',
    avatar: true,
    active: route.path.startsWith('/profile')
  }
])

// --- Sliding active indicator ------------------------------------------------
// A single highlight pill sits behind the active item and animates to its new
// position whenever the route changes or the FAB gap opens/closes.

const barRef = ref(null)
const centerIndex = computed(() => Math.floor(navItems.value.length / 2))
const activeIndex = computed(() => navItems.value.findIndex(item => item.active))

const indicator = ref({ left: 0, top: 0, width: 0, height: 0 })
const indicatorReady = ref(false)
// When true the pill eases between positions (route change); when false it snaps
// to the measured layout every frame (used while the FAB gap reflows the items).
const indicatorAnimating = ref(false)

const indicatorStyle = computed(() => ({
  left: `${indicator.value.left}px`,
  top: `${indicator.value.top}px`,
  width: `${indicator.value.width}px`,
  height: `${indicator.value.height}px`,
  opacity: indicatorReady.value && indicator.value.width > 0 ? 1 : 0,
  transition: indicatorAnimating.value
    ? `left ${NAV_DURATION}ms ${NAV_EASE}, top ${NAV_DURATION}ms ${NAV_EASE}, width ${NAV_DURATION}ms ${NAV_EASE}, height ${NAV_DURATION}ms ${NAV_EASE}, opacity 200ms ease`
    : 'opacity 200ms ease'
}))

const measure = () => {
  const bar = barRef.value
  if (!bar) return
  const el = bar.querySelector(`[data-nav-index="${activeIndex.value}"]`)
  if (!el) {
    indicatorReady.value = false
    return
  }
  indicator.value = {
    left: el.offsetLeft,
    top: el.offsetTop,
    width: el.offsetWidth,
    height: el.offsetHeight
  }
  indicatorReady.value = true
}

let rafId = 0
// Follow the live layout for the duration of the FAB gap transition so the pill
// stays glued to the active item while the row slides apart / together.
const trackReflow = () => {
  if (typeof requestAnimationFrame === 'undefined') return
  indicatorAnimating.value = false
  const start = performance.now()
  const step = (now) => {
    measure()
    if (now - start < NAV_DURATION + 40) {
      rafId = requestAnimationFrame(step)
    } else {
      indicatorAnimating.value = true
    }
  }
  cancelAnimationFrame(rafId)
  rafId = requestAnimationFrame(step)
}

// Route change: keep easing on so the pill glides to the new item.
watch(activeIndex, async () => {
  indicatorAnimating.value = true
  await nextTick()
  measure()
})

// FAB appears/disappears: items reflow over NAV_DURATION — track them frame by frame.
watch(hasFab, async () => {
  await nextTick()
  trackReflow()
})

const onResize = () => {
  indicatorAnimating.value = false
  measure()
}

// The layout can mount before the Kratos session resolves, in which case the tour has
// no user record to check and declines to decide. Retry the moment there is one.
watch(() => authStore.user?.publicId, (publicId) => {
  if (publicId) maybeAutoStart()
})
</script>
