<script setup>
const { locale, t } = useI18n()

useHead({
  meta: [
    { name: 'viewport', content: 'width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no' }
  ],
  link: [
    { rel: 'icon', href: '/favicon.ico' }
  ],
  htmlAttrs: {
    lang: computed(() => locale.value)
  }
})

const title = computed(() => t('meta.default.title'))
const description = computed(() => t('meta.default.description'))
const ogTitle = computed(() => t('meta.default.ogTitle'))
const ogDescription = computed(() => t('meta.default.ogDescription'))
/**
 * The toast viewport's own default offset is a bare `top-4`, which put every toast on top of the
 * fixed `AppHeader` — over the page title, the search button and the bell — and, carrying no
 * safe-area term, under the status bar on a notched phone the day `viewport-fit=cover` is turned
 * on. `--app-toast-top` (main.css) places it below the header the header itself measured.
 *
 * Passed as `ui.viewport` rather than set in `app.config.ts`, which is where a theme override
 * would normally go: `top-4` is declared in the toaster theme's *compoundVariants*, and an
 * app-config override merges into the `slots` layer underneath them, so `top-4` wins and the
 * override silently does nothing (the `z-` half of the same attempt did work, which is what makes
 * it worth writing down). `ui.viewport` is passed to the slot as `class`, the one layer that
 * comes after the variants.
 */
const toaster = {
  position: 'top-right',
  ui: { viewport: 'top-(--app-toast-top) z-(--z-toast)' }
}

useSeoMeta({
  title,
  description,
  ogTitle,
  ogDescription,
  twitterCard: 'summary_large_image',
  twitterTitle: title,
  twitterDescription: description
})
</script>

<template>
  <div>
    <div class="app-shell">
      <NuxtLayout :toaster="toaster">
        <NuxtPage />
      </NuxtLayout>
    </div>
    <SplashScreen />
    <!-- App-wide: reads the useUndoableAction() singleton directly, so it stays mounted (and a
         pending action stays undoable) across navigation rather than living inside one page. -->
    <UndoToast />
  </div>
</template>
