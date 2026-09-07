<template>
  <ErrorScreen kind="offline" />
</template>

<script setup lang="ts">
/**
 * The service worker's offline fallback document.
 *
 * app/error.vue covers a *client-side* navigation that fails while the app is
 * already running, but it cannot cover a cold start with no network: nothing has
 * booted yet, so the browser shows its own error page instead — and in an
 * installed PWA that is a dead end. So the SW precaches this route and serves it
 * as `precacheFallback.fallbackURL` for a navigation it cannot fetch (see the
 * `pwa.workbox` block in nuxt.config.ts). It renders the same ErrorScreen as the
 * offline branch of error.vue.
 *
 * No layout and no middleware on purpose: it must render with no session, no
 * API and no network. It also has to exist as a static file for the SW to
 * precache, which is why `/offline` is in `nitro.prerender.routes`.
 *
 * "Try again" reloads, and reloading keeps the URL the user actually asked for —
 * the SW served this document *for that URL* — so a retry once the network is
 * back lands on the real page, not here.
 */
definePageMeta({ layout: false })
</script>
