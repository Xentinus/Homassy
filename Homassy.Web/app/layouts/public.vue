<template>
  <UApp>
    <UHeader>
      <template #title>
        <NuxtLink to="/">
          <!-- <AppLogo class="w-auto h-6 shrink-0" /> -->
          <span class="font-semibold text-lg">Homassy</span>
        </NuxtLink>
      </template>

      <UNavigationMenu :items="authItems" class="ml-auto hidden md:flex" />

      <template #right>
        <UColorModeButton />

        <UTooltip text="GitHub Repository">
        <UButton
          color="neutral"
          variant="ghost"
          to="https://github.com/Xentinus/Homassy"
          target="_blank"
          icon="i-simple-icons-github"
          aria-label="GitHub"
        />
      </UTooltip>
      </template>

      <template #body>
        <UNavigationMenu :items="authItems" orientation="vertical" class="-mx-2.5" />
      </template>
    </UHeader>

    <UMain>
      <slot />
    </UMain>

    <USeparator />

    <UFooter>
      <template #left>
        <p class="text-sm text-muted flex items-center gap-2">
          <span>Made with</span>
          <UIcon
            name="i-lucide-heart"
            class="h-4 w-4 text-primary"
          />
          <span>by</span>
          <a
            href="https://github.com/Xentinus"
            target="_blank"
            rel="noopener"
            class="font-semibold hover:underline"
          >
            Béla Kellner
          </a>
        </p>
      </template>
    </UFooter>
  </UApp>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import type { NavigationMenuItem } from '@nuxt/ui'

const route = useRoute()

const authItems = computed<NavigationMenuItem[]>(() => [
  {
    label: 'Home',
    to: '/',
    icon: 'i-lucide-home',
    active: route.path === '/'
  },
  {
    label: 'Log in',
    to: '/auth/login',
    icon: 'i-lucide-log-in',
    active: route.path.startsWith('/auth/login')
  },
  {
    label: 'Sign up',
    to: '/auth/register',
    icon: 'i-lucide-user-plus',
    active: route.path.startsWith('/auth/register')
  }
])
</script>
