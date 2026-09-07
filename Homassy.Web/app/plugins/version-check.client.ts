/**
 * Tell the user when a new version has been deployed.
 *
 * The PWA is registered with `registerType: 'autoUpdate'`, so the service worker
 * fetches and installs a new build on its own — but the tab keeps running the
 * old bundle until something reloads it, and nothing said so. An installed PWA
 * can stay open for days, which is long enough to sit on a stale build.
 *
 * `GET /api/Version` is the version the whole platform is deployed at (it is the
 * value the splash and the profile page already show), so a change in it is a
 * deployment. This plugin remembers the version the running document booted on
 * and, when a later poll disagrees, shows one persistent toast with a Reload
 * action. Reloading is what actually swaps in the waiting service worker.
 *
 * Deliberately quiet: a failed poll (offline, API down, mid-deploy) tells the
 * user nothing and is swallowed. The toast is shown once per version, so a user
 * who dismisses it is not nagged again until the next deployment.
 *
 * This file used to be five bytes — an empty registered plugin that Nuxt loaded
 * on every client boot to do nothing at all (issue #110).
 */

/** Slow on purpose: a deploy is rare and this runs for the life of the tab. */
const POLL_INTERVAL_MS = 15 * 60 * 1000

/**
 * Returning to the app is the likeliest moment to find a new deployment, but
 * app-switching fires `visibilitychange` constantly, so a resume only triggers a
 * check when the last one is at least this old.
 */
const MIN_CHECK_GAP_MS = 5 * 60 * 1000

export default defineNuxtPlugin((nuxtApp) => {
  if (import.meta.server) return

  // Both are read at setup, while the Nuxt context is active — a timer callback
  // has none of its own. `getVersion` closes over `$api`, and vue-i18n's `t`
  // stays bound to the composer, so it still follows a later locale change.
  const { getVersion } = useVersionApi()
  // @nuxtjs/i18n types `$i18n` loosely on NuxtApp; `t` is all this needs.
  const { t } = nuxtApp.$i18n as { t: (key: string) => string }

  /** The version this document booted on; set by the first successful poll. */
  let bootVersion: string | null = null
  /** The version we have already offered a reload for, so we offer it once. */
  let announcedVersion: string | null = null
  let lastCheckedAt = 0

  const readVersion = async (): Promise<string | null> => {
    try {
      const response = await getVersion()
      return response.success && response.data ? response.data.version : null
    }
    catch {
      // Offline, API restarting, request never left the device — none of that is
      // news the user can act on.
      return null
    }
  }

  const announce = (version: string) => {
    if (announcedVersion === version) return
    announcedVersion = version

    // `useToast` reads Nuxt state and injects from the Vue app, so it needs the
    // context restored — this runs from a timer, outside any component.
    nuxtApp.runWithContext(() => {
      useToast().add({
        title: t('toast.newVersion.title'),
        description: t('toast.newVersion.description'),
        icon: 'i-lucide-rocket',
        color: 'primary',
        // 0 disables the auto-close timer: this one waits for the user.
        duration: 0,
        actions: [{
          label: t('toast.newVersion.reload'),
          color: 'neutral',
          variant: 'outline',
          onClick: () => { window.location.reload() }
        }]
      })
    })
  }

  const check = async () => {
    lastCheckedAt = Date.now()
    const version = await readVersion()
    if (!version) return

    // The very first successful read establishes the baseline. It is done here
    // rather than at boot so a poll that fails at startup does not lock in a
    // null baseline and miss every later deployment.
    if (bootVersion === null) {
      bootVersion = version
      return
    }

    if (version !== bootVersion) announce(version)
  }

  void check()
  const interval = window.setInterval(() => void check(), POLL_INTERVAL_MS)

  // Background timers are throttled or paused outright, so a long suspension can
  // swallow the interval entirely. Coming back to the foreground is both the
  // moment that misses and the moment a new deployment is most likely to be
  // waiting, so it gets its own check.
  const onVisible = () => {
    if (document.visibilityState !== 'visible') return
    if (Date.now() - lastCheckedAt < MIN_CHECK_GAP_MS) return
    void check()
  }
  document.addEventListener('visibilitychange', onVisible)

  if (import.meta.hot) {
    import.meta.hot.dispose(() => {
      window.clearInterval(interval)
      document.removeEventListener('visibilitychange', onVisible)
    })
  }
})
