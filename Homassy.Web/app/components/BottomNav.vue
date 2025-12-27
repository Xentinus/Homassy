<template>
  <nav
    class="fixed bottom-0 left-0 right-0 border-t border-primary-700 bg-primary backdrop-blur z-[9999] pb-safe"
    style="position: fixed !important; bottom: 0 !important; left: 0 !important; right: 0 !important;"
  >
    <div class="mx-auto max-w-2xl">
      <ul class="grid grid-cols-3">
        <li v-for="item in items" :key="item.to">
          <NuxtLink
            :to="item.to"
            class="flex flex-col items-center justify-center gap-1 py-4 text-xs leading-tight text-white"
            :class="isActive(item.to) ? 'opacity-100 font-semibold' : 'opacity-70 hover:opacity-90'
            "
          >
            <UIcon :name="item.icon" class="h-6 w-6" />
            <span class="max-w-full px-1 text-center">{{ item.label }}</span>
          </NuxtLink>
        </li>
      </ul>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
const route = useRoute()

const isActive = (to: string) => {
  const path = route.path
  if (to === '/') return path === '/'
  return path.startsWith(to)
}

const items = [
  { to: '/products', icon: 'i-lucide-package', label: 'Termékek' },
  { to: '/shopping-list', icon: 'i-lucide-shopping-cart', label: 'Lista' },
  { to: '/profile', icon: 'i-lucide-user', label: 'Profil' }
]
</script>
