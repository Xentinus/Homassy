<template>
  <UApp class="flex flex-col min-h-screen">
    <UMain class="flex-1 px-4 sm:px-6 lg:px-8 pb-32">
      <slot />
    </UMain>

    <nav class="fixed inset-x-4 bottom-4 z-50">
      <div class="flex items-center gap-2 rounded-2xl border border-gray-200/70 dark:border-gray-800/70 bg-background/80 backdrop-blur shadow-lg px-3 pt-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))]">
        <NuxtLink
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          :aria-label="item.label"
          class="flex-1 flex flex-col items-center justify-center h-12 rounded-xl transition duration-150 hover:scale-[1.02] active:scale-95"
          :class="item.active ? 'text-primary-500' : 'text-gray-500 dark:text-gray-400 hover:text-primary-500'"
        >
          <UIcon :name="item.icon" class="h-6 w-6" />
          <span class="mt-1 text-xs hidden md:block">{{ item.label }}</span>
        </NuxtLink>
      </div>
    </nav>
  </UApp>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const { t } = useI18n()

const navItems = computed(() => [
  {
    label: t('nav.products'),
    to: '/products',
    icon: 'i-lucide-package',
    active: route.path.startsWith('/products')
  },
  {
    label: t('nav.shoppingLists'),
    to: '/shopping-lists',
    icon: 'i-lucide-shopping-cart',
    active: route.path.startsWith('/shopping-lists')
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
