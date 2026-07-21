/**
 * Splash screen state.
 *
 * The boot splash is shown ONLY in standalone PWA mode — that decision is made
 * purely in CSS (`@media (display-mode: standalone)`), so the overlay can render
 * in the SSR HTML and paint on the very first frame without any hydration
 * mismatch. This composable only handles dismissal: `markReady()` stamps a
 * `data-splash-ready` attribute on <html>, which CSS uses to fade the overlay
 * out. Toggling an attribute on the document (instead of a reactive `v-if`)
 * keeps dismissal completely outside Vue's hydration diff, so it works even
 * when it fires before the app mounts (e.g. the anonymous fast-path).
 */
export function useSplashScreen() {
  const markReady = () => {
    if (import.meta.client) {
      document.documentElement.setAttribute('data-splash-ready', 'true')
    }
  }

  return { markReady }
}
