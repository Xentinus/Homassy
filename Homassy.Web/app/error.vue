<template>
  <ErrorScreen :kind="kind" :status-code="statusCode" />
</template>

<script setup lang="ts">
/**
 * Replaces Nuxt's default error screen — unstyled, untranslated, theme-blind,
 * and with no way back other than the browser's own button, which in an
 * installed PWA (no address bar) is close to a dead end.
 *
 * Nuxt renders this instead of app.vue, so there is no layout and no
 * SplashScreen here. Deliberately NOT wrapped in `.app-shell`: that class is
 * held at `opacity: 0` in standalone until the splash is dismissed, and the
 * splash is not mounted on this page to do the dismissing.
 *
 * Nothing here touches the auth store or the API — it has to render when
 * everything else is broken.
 */
import type { NuxtError } from '#app'

const props = defineProps<{
  error: NuxtError
}>()

/**
 * `navigator.onLine === false` is the reliable half of the offline check. The
 * other half is a request that never reached a server: the browser reports that
 * as a bare TypeError, and its message is the only thing that distinguishes it
 * from a server-side failure. Both spellings appear in the wild ('Failed to
 * fetch' in Chromium, 'NetworkError'/'fetch failed' in Firefox and undici).
 */
const NETWORK_FAILURE = /failed to fetch|networkerror|network request failed|fetch failed|load failed/i

const isOffline = ref(false)

const looksLikeNetworkFailure = computed(() => {
  // A status code means something answered, so it cannot be a lost connection.
  if (props.error?.statusCode && props.error.statusCode >= 400) return false
  return NETWORK_FAILURE.test(props.error?.message ?? '')
})

const statusCode = computed(() => {
  const code = props.error?.statusCode
  return code && code >= 400 ? code : undefined
})

const kind = computed(() => {
  if (isOffline.value || looksLikeNetworkFailure.value) return 'offline'
  if (statusCode.value === 404) return 'notFound'
  return 'serverError'
})

// SSR cannot know the client's connectivity, so the offline branch is decided
// after mount only — reading `navigator.onLine` during render would be a
// hydration mismatch.
onMounted(() => {
  const sync = () => { isOffline.value = navigator.onLine === false }
  sync()
  window.addEventListener('online', sync)
  window.addEventListener('offline', sync)
  onBeforeUnmount(() => {
    window.removeEventListener('online', sync)
    window.removeEventListener('offline', sync)
  })
})
</script>
