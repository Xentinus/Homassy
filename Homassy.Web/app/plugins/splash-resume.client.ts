/**
 * Re-show the boot splash after a long PWA background gap.
 *
 * The splash (SplashScreen.vue / useSplashScreen) is a CSS overlay that paints
 * on a full document load and is then dismissed one-way. A background→foreground
 * resume of the SAME still-alive document therefore never re-shows it — which is
 * exactly what we want for quick app-switching. But when the OS keeps the PWA
 * alive and the user returns after a long absence, it should feel like a fresh
 * launch. This plugin watches page-lifecycle events and, when the document was
 * backgrounded for at least RESUME_SPLASH_MS, re-arms the splash for a brief,
 * self-dismissing flash. (A background gap long enough for the OS to KILL the
 * app is a cold launch = full document load = splash already shows, so that case
 * needs no handling here.)
 *
 * Standalone-only: the splash CSS only renders as an installed PWA, so the
 * listeners are skipped in a normal browser tab.
 */
import { useSplashScreen } from '~/composables/useSplashScreen'

const RESUME_SPLASH_MS = 10 * 60 * 1000 // re-show splash after ≥10 min backgrounded

export default defineNuxtPlugin(() => {
  if (import.meta.server) return

  const isStandalone = () =>
    (window.navigator as unknown as { standalone?: boolean }).standalone === true
    || window.matchMedia('(display-mode: standalone)').matches
  if (!isStandalone()) return

  const { markReady, rearm } = useSplashScreen()
  // Plugin-scoped: one alive document == one JS realm. No storage needed — a
  // killed app is a cold boot that shows the splash anyway.
  let lastHiddenAt: number | null = null

  // Wall-clock Date.now(): accurate across a suspension even though background
  // JS timers are throttled/paused.
  const onHidden = () => {
    lastHiddenAt = Date.now()
  }

  const onVisible = () => {
    if (lastHiddenAt !== null && Date.now() - lastHiddenAt >= RESUME_SPLASH_MS) {
      rearm() // re-show the splash overlay
      markReady() // dismiss again after the minimum-visible floor
    }
    lastHiddenAt = null
  }

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') onHidden()
    else if (document.visibilityState === 'visible') onVisible()
  })

  // BFCache / suspend safety net (notably iOS): pagehide stamps the time, and a
  // restored pageshow re-checks the gap even if visibilitychange did not fire.
  window.addEventListener('pagehide', onHidden)
  window.addEventListener('pageshow', (e) => {
    if ((e as PageTransitionEvent).persisted) onVisible()
  })
})
