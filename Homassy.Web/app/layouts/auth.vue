<template>
  <UApp class="flex flex-col min-h-screen">
    <UMain class="flex-1 px-4 sm:px-6 lg:px-8 pb-32">
      <slot />
    </UMain>

    <nav class="fixed inset-x-4 bottom-4 z-50">
      <!-- Dynamic add button, centred on the nav's top border -->
      <NavFab />

      <div
        ref="barRef"
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
            :aria-label="item.label"
            class="relative z-10 flex-1 flex flex-col items-center justify-center h-16 md:h-12 rounded-xl transition-colors duration-300 active:scale-95"
            :class="item.active ? 'text-primary-600 dark:text-primary-400 font-semibold' : 'text-gray-500 dark:text-gray-400 hover:text-primary-500'"
          >
            <div class="relative transition-transform duration-300" :class="item.active ? 'scale-110' : ''">
              <!-- Badge for expiration count -->
              <div
                v-if="item.badge"
                class="absolute -top-1.5 -right-1.5 z-10 flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 dark:bg-red-600 shadow-md"
              >
                <span class="text-[10px] font-bold text-white leading-none">
                  {{ item.badge }}
                </span>
              </div>

              <div
                v-if="item.avatar"
                class="h-7 w-7 rounded-full border-2 border-primary-500 overflow-hidden flex items-center justify-center bg-primary-100 dark:bg-primary-900/40 shrink-0"
              >
                <!-- Avatar is auth-store driven (client-only): render on the client
                     so SSR (user=null → "?") doesn't mismatch the hydrated initials. -->
                <ClientOnly>
                  <img
                    v-if="avatarSrc"
                    :src="avatarSrc"
                    :alt="item.label"
                    class="h-full w-full object-cover"
                  >
                  <span v-else class="text-[10px] font-semibold text-primary-600 dark:text-primary-300 leading-none">
                    {{ avatarInitials }}
                  </span>
                </ClientOnly>
              </div>
              <UIcon v-else :name="item.icon" class="h-6 w-6" />
            </div>
            <span class="mt-1 text-xs hidden md:block">{{ item.label }}</span>
          </NuxtLink>
        </template>
      </div>
    </nav>
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

// Profile nav item shows the user's avatar (picture, or initials fallback) — mirrors
// the profile page (app/pages/profile/index.vue).
const avatarSrc = computed(() => {
  const b64 = authStore.user?.profilePictureBase64
  return b64 ? `data:image/jpeg;base64,${b64}` : undefined
})
const avatarInitials = computed(() => {
  const name = authStore.user?.displayName || authStore.user?.name || ''
  if (!name) return '?'
  return name.trim().split(/\s+/).map(w => w.charAt(0).toUpperCase()).join('').slice(0, 2)
})

const expirationCount = ref(0)
const deadlineCount = ref(0)

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
    }
  } catch (error) {
    console.error('Failed to fetch expiration count:', error)
    expirationCount.value = 0
  }
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

onMounted(() => {
  fetchExpirationCount()
  fetchDeadlineCount()

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
    active: route.path.startsWith('/calendar')
  },
  {
    label: t('nav.products'),
    to: '/products',
    icon: 'i-lucide-package',
    active: route.path.startsWith('/products'),
    badge: expirationCount.value > 0 ? expirationCount.value : undefined
  },
  {
    label: t('nav.shoppingLists'),
    to: '/shopping-lists',
    icon: 'i-lucide-shopping-cart',
    active: route.path.startsWith('/shopping-lists'),
    badge: deadlineCount.value > 0 ? deadlineCount.value : undefined
  },
  {
    label: t('nav.profile'),
    to: '/profile',
    icon: 'i-lucide-user',
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
</script>
