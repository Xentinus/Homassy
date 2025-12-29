<template>
  <UApp class="flex flex-col min-h-screen">
    <UHeader>
      <template #title>
        <ClientOnly>
          <NuxtLink to="/" aria-label="Homassy">
            <span class="font-semibold text-lg">Homassy</span>
          </NuxtLink>
        </ClientOnly>
      </template>

      <ClientOnly>
        <UNavigationMenu :items="authItems" class="ml-auto hidden md:flex" />
      </ClientOnly>

      <template #right>
        <ClientOnly>
          <ULocaleSelect
            :model-value="locale"
            :locales="[en, hu, de]"
            @update:model-value="handleLocaleChange"
          />
          <UColorModeButton />
        </ClientOnly>

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
        <ClientOnly>
          <UNavigationMenu :items="authItems" orientation="vertical" class="-mx-2.5" />
        </ClientOnly>
      </template>
    </UHeader>

    <UMain>
      <slot />
    </UMain>

    <USeparator />

    <UFooter>
      <template #left>
        <p class="text-sm text-muted flex items-center gap-2">
          <span>{{ t('footer.madeWith') }}</span>
          <UIcon
            name="i-lucide-heart"
            class="h-4 w-4 text-primary"
          />
          <span>{{ t('footer.by') }}</span>
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
import { useI18n } from 'vue-i18n'
import type { NavigationMenuItem } from '@nuxt/ui'
import { en, hu, de } from '@nuxt/ui/locale'

const route = useRoute()
const { t, locale } = useI18n()
const { $i18n } = useNuxtApp()

const handleLocaleChange = (newLocale: string) => {
  if (newLocale === 'en' || newLocale === 'hu' || newLocale === 'de') {
    $i18n.setLocale(newLocale)
  }
}

const authItems = computed<NavigationMenuItem[]>(() => [
  {
    label: t('nav.home'),
    to: '/',
    icon: 'i-lucide-home',
    active: route.path === '/'
  },
  {
    label: t('auth.login'),
    to: '/auth/login',
    icon: 'i-lucide-log-in',
    active: route.path.startsWith('/auth/login')
  },
  {
    label: t('auth.register'),
    to: '/auth/register',
    icon: 'i-lucide-user-plus',
    active: route.path.startsWith('/auth/register')
  }
])
</script>
