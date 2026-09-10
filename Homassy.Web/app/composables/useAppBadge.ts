/**
 * The app's one home for "how many things need attention", pushed out to the two
 * surfaces that live *outside* the running page: the installed app's icon badge
 * (`navigator.setAppBadge`) and the browser tab's title (`(3) Homassy`).
 *
 * The number itself is not computed here — `layouts/auth.vue` already fetches the
 * expiring-items count for the bottom-nav badge, and calls `setExpirationCount()`
 * with it. This composable owns only where that number is shown and, for the icon,
 * whether it may be shown at all.
 *
 * **The two surfaces are gated differently, on purpose.**
 *
 * - The **title prefix** is the browser-tab equivalent of the in-app nav badge, so
 *   it follows the same rule the nav badge does: it is always shown. It is visible
 *   only to someone who already has the app open in a tab.
 * - The **icon badge** persists on the home screen / dock with the app closed, which
 *   makes it a notification, not a piece of page chrome. It is therefore suppressed
 *   for a user who turned expiration reminders off (`pushNotificationsEnabled`) —
 *   badging someone who opted out of being reminded would be exactly the reminder
 *   they declined, just in another place.
 *
 * All state is module-scoped, so this is safe to call from a plugin, a layout and a
 * plain function alike, and every caller sees the same count.
 */
import { ref, computed, watch } from 'vue'
import { useUserApi } from '~/composables/api/useUserApi'

/** The count both surfaces render. `0` means "nothing to show", never `null`. */
const count = ref(0)

/**
 * Whether the *icon* badge is permitted. Starts `false` and is only raised once the
 * preferences have actually been read: an icon badge set optimistically and cleared a
 * moment later would flash on the home screen of a user who opted out.
 */
const iconAllowed = ref(false)

/** Guards the one-time preference read (and lets a save re-run it). */
let preferencesPromise: Promise<void> | null = null

/**
 * True once something has ever been written to the icon badge. Without it, a user who
 * has the badge suppressed would still get a `clearAppBadge()` call on every count
 * change — harmless, but it means touching an OS surface we were told to leave alone.
 */
let iconTouched = false

const badgingSupported = () =>
  import.meta.client && typeof navigator !== 'undefined' && 'setAppBadge' in navigator

function applyIconBadge() {
  if (!badgingSupported()) return

  const nav = navigator as Navigator & {
    setAppBadge?: (contents?: number) => Promise<void>
    clearAppBadge?: () => Promise<void>
  }

  const value = iconAllowed.value ? count.value : 0

  // Every rejection here is the platform declining (not installed, no permission,
  // an unsupported argument) — nothing the app can act on, and nothing worth an
  // unhandled rejection in the console.
  if (value > 0) {
    iconTouched = true
    nav.setAppBadge?.(value).catch(() => {})
  } else if (iconTouched) {
    iconTouched = false
    nav.clearAppBadge?.().catch(() => {})
  }
}

/**
 * The `useHead` title template for the current count: `'(3) %s'`, or the bare `'%s'`
 * when there is nothing to announce. Read by `plugins/app-badge.client.ts`, which is
 * the single place it is installed — a per-page `useHead` would leave the prefix
 * behind on whichever page happened to register it last.
 */
const titleTemplate = computed(() => (count.value > 0 ? `(${count.value}) %s` : '%s'))

// One watcher for the whole app rather than a write inside every setter: the icon
// badge depends on both the count and the (asynchronously resolved) permission, and
// whichever of the two settles last has to be the one that paints.
if (import.meta.client) {
  watch([count, iconAllowed], applyIconBadge)
}

export const useAppBadge = () => {
  /**
   * Read the user's notification preferences once and decide whether the icon badge
   * is allowed. Safe to call repeatedly; pass `force` after a preferences save.
   */
  async function resolveIconPermission(force = false): Promise<void> {
    if (!import.meta.client) return
    if (preferencesPromise && !force) return preferencesPromise

    preferencesPromise = (async () => {
      try {
        const { getNotificationPreferences } = useUserApi()
        const res = await getNotificationPreferences()
        iconAllowed.value = !!res?.data?.pushNotificationsEnabled
      } catch {
        // A failed read is not consent. Leave the icon alone.
        iconAllowed.value = false
      }
    })()

    return preferencesPromise
  }

  /**
   * Publish the current expiring-items count. Called by the auth layout with exactly
   * the number the nav badge shows, so the icon, the tab title and the nav badge can
   * never disagree.
   */
  function setExpirationCount(value: number) {
    count.value = Number.isFinite(value) && value > 0 ? Math.floor(value) : 0
  }

  return {
    count: computed(() => count.value),
    titleTemplate,
    isSupported: computed(badgingSupported),
    resolveIconPermission,
    setExpirationCount
  }
}
