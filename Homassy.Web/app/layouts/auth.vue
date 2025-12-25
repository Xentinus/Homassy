<template>
  <UApp>
    <UHeader>
      <template #left>
        <NuxtLink to="/">
          <span class="font-semibold text-lg">Homassy</span>
        </NuxtLink>
      </template>

      <template #right>
        <UColorModeButton />
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
            href="https://github.com/Xentinus/Homassy"
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
