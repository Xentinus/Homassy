/**
 * Splash screen state.
 *
 * The boot splash is shown ONLY in standalone PWA mode — that decision is made
 * in CSS (`@media (display-mode: standalone)` + a `pwa-standalone` class set by
 * an inline head script for iOS), so the overlay can render in the SSR HTML and
 * paint on the very first frame without any hydration mismatch. This composable
 * only handles dismissal: `markReady()` stamps a `data-splash-ready` attribute
 * on <html>, which CSS uses to fade the overlay out. Toggling an attribute on
 * the document (instead of a reactive `v-if`) keeps dismissal completely outside
 * Vue's hydration diff, so it works even when it fires before the app mounts.
 *
 * Minimum visible time: when the app is ready very fast (cached calendar), the
 * splash would flash too briefly to read. `markReady()` therefore never hides
 * the splash before MIN_VISIBLE_MS has elapsed; a slow load still hides exactly
 * when it becomes ready (no extra artificial delay).
 *
 * Re-show on resume: `rearm()` reverses the dismissal (removes the attribute and
 * restarts the minimum-visible baseline) so the splash can be shown again when
 * the PWA is resumed after a long background gap — see plugins/splash-resume.
 */
const MIN_VISIBLE_MS = 1800

// Client-only baseline ≈ when the splash first became visible (first paint).
let shownAt: number | null = null

export function useSplashScreen() {
  if (import.meta.client && shownAt === null) {
    shownAt = performance.now()
  }

  const reveal = () => {
    document.documentElement.setAttribute('data-splash-ready', 'true')
  }

  const rearm = () => {
    if (!import.meta.client) return
    // Restart the MIN_VISIBLE_MS floor from now so a resumed splash is also
    // readable, then drop the attribute so CSS shows .splash again (standalone).
    shownAt = performance.now()
    document.documentElement.removeAttribute('data-splash-ready')
  }

  const markReady = () => {
    if (!import.meta.client) return
    const remaining = MIN_VISIBLE_MS - (performance.now() - (shownAt ?? performance.now()))
    if (remaining > 0) {
      window.setTimeout(reveal, remaining)
    }
    else {
      reveal()
    }
  }

  return { markReady, rearm }
}
