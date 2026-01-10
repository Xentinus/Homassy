<template>
  <UApp class="flex flex-col min-h-screen">
    <UMain class="flex-1 px-4 sm:px-6 lg:px-8 pb-32">
      <slot />
    </UMain>

    <nav class="fixed inset-x-4 bottom-4 z-50">
      <div class="flex items-center gap-2 rounded-2xl border border-primary-200 dark:border-primary-800 bg-background/80 backdrop-blur shadow-lg px-3 pt-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))]">
        <NuxtLink
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          :aria-label="item.label"
          class="flex-1 flex flex-col items-center justify-center h-16 md:h-12 rounded-xl transition duration-150 hover:scale-[1.02] active:scale-95"
          :class="item.active ? 'text-primary-500 font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-primary-500'"
        >
          <div class="relative">
            <!-- Badge for expiration count -->
            <div
              v-if="item.badge"
              class="absolute -top-1.5 -right-1.5 z-10 flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 dark:bg-red-600 shadow-md"
            >
              <span class="text-[10px] font-bold text-white leading-none">
                {{ item.badge }}
              </span>
            </div>

            <UIcon :name="item.icon" :class="item.active ? 'h-6 w-6 font-bold' : 'h-6 w-6'" />
          </div>
          <span class="mt-1 text-xs hidden md:block">{{ item.label }}</span>
        </NuxtLink>
      </div>
    </nav>
  </UApp>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useDebounceFn } from '@vueuse/core'

const route = useRoute()
const { t } = useI18n()
const { getExpirationCount } = useProductsApi()
const { getDeadlineCount } = useShoppingListApi()
const eventBus = useEventBus()

const expirationCount = ref(0)
const deadlineCount = ref(0)

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
})

const navItems = computed(() => [
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
    label: t('nav.activity'),
    to: '/activity',
    icon: 'i-lucide-activity',
    active: route.path.startsWith('/activity')
  },
  {
    label: t('nav.profile'),
    to: '/profile',
    icon: 'i-lucide-user',
    active: route.path.startsWith('/profile')
  }
])
</script>
