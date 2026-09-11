/**
 * The app's one home for "how many things need attention", pushed out to the two
 * surfaces that live *outside* the running page: the installed app's icon badge
 * (`navigator.setAppBadge`) and the browser tab's title (`(3) Homassy`).
 *
 * **The number is a sum of sources, and which ones count is the user's choice.**
 * Three things can contribute — overdue shopping-list items, unread family chat
 * messages, and expiring products — and each can be switched off on its own. The
 * defaults say overdue items and unread messages, not expirations: the first two are
 * someone waiting on you, while a product that expires in ten days is a fact about
 * the cupboard, and a permanent number on the home screen turns it into a nag.
 *
 * The counts themselves are not computed here. `layouts/auth.vue` already fetches
 * the expiration and deadline counts for the bottom-nav badges and the chat's unread
 * count rides on `useFamilyChat`; the layout publishes all three, so the icon, the
 * tab title and the in-app badges can never disagree about the same number.
 *
 * **The two surfaces are gated differently, on purpose.**
 *
 * - The **title prefix** is the browser-tab equivalent of the in-app nav badges, so it
 *   follows the same rule they do: it is always shown. It is visible only to someone
 *   who already has the app open in a tab.
 * - The **icon badge** persists on the home screen / dock with the app closed, which
 *   makes it a notification, not a piece of page chrome. It is therefore suppressed
 *   for a user who turned push off (`pushNotificationsEnabled`) — badging someone who
 *   opted out of being reminded would be exactly the reminder they declined, just in
 *   another place.
 *
 * **The source switches are device-local** (`localStorage`), like the haptics switch
 * and the theme, and unlike the notification preferences. The icon badge only exists
 * on a device the app is installed on, and "badge this phone with the shopping list"
 * is a statement about that phone rather than about the account.
 *
 * All state is module-scoped, so this is safe to call from a plugin, a layout and a
 * plain function alike, and every caller sees the same count.
 */
import { ref, computed, watch } from 'vue'
import { useUserApi } from '~/composables/api/useUserApi'

/** Which counts may contribute to the badge. */
export interface BadgeSources {
  /** Shopping-list items past their deadline. */
  deadline: boolean
  /** Unread family chat messages. */
  chat: boolean
  /** Products expiring or already expired. */
  expiration: boolean
}

const STORAGE_KEY = 'homassy_badge_sources'

/**
 * What counts when nothing has been chosen: the two things that are somebody waiting
 * on you. Expirations are off — they are always true of some product somewhere, and a
 * badge that never reaches zero stops meaning anything.
 */
const DEFAULT_SOURCES: BadgeSources = {
  deadline: true,
  chat: true,
  expiration: false
}

/** The three published counts, each `0` when there is nothing to show, never `null`. */
const deadlineCount = ref(0)
const chatUnreadCount = ref(0)
const expirationCount = ref(0)

const sources = ref<BadgeSources>({ ...DEFAULT_SOURCES })

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

/** Read once on the client; a write from the settings row updates it in place. */
let sourcesLoaded = false

const badgingSupported = () =>
  import.meta.client && typeof navigator !== 'undefined' && 'setAppBadge' in navigator

/**
 * The badge's number: every enabled source, added up.
 *
 * A sum rather than a list, because the surface is a single number - what it buys the
 * reader is "there are four things", and opening the app is what says which four.
 */
const count = computed(() => {
  let total = 0
  if (sources.value.deadline) total += deadlineCount.value
  if (sources.value.chat) total += chatUnreadCount.value
  if (sources.value.expiration) total += expirationCount.value
  return total
})

function loadSources() {
  if (!import.meta.client || sourcesLoaded) return
  sourcesLoaded = true

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return

    const parsed = JSON.parse(raw) as Partial<BadgeSources>
    sources.value = {
      deadline: typeof parsed.deadline === 'boolean' ? parsed.deadline : DEFAULT_SOURCES.deadline,
      chat: typeof parsed.chat === 'boolean' ? parsed.chat : DEFAULT_SOURCES.chat,
      expiration: typeof parsed.expiration === 'boolean' ? parsed.expiration : DEFAULT_SOURCES.expiration
    }
  } catch {
    // A private window, cleared site data, or storage the browser refuses outright.
    // The defaults are a perfectly good badge; losing a stored choice is not an error
    // worth surfacing.
  }
}

function persistSources() {
  if (!import.meta.client) return

  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(sources.value))
  } catch {
    // Same as above: the switch still works for this session.
  }
}

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
// badge depends on the count (itself a sum of three sources and three switches) and
// on the asynchronously resolved permission, and whichever settles last has to paint.
if (import.meta.client) {
  watch([count, iconAllowed], applyIconBadge)
}

const clamp = (value: number): number =>
  Number.isFinite(value) && value > 0 ? Math.floor(value) : 0

export const useAppBadge = () => {
  loadSources()

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
   * Publish the current expiring-items count — exactly the number the nav badge shows.
   *
   * Published whether or not it currently counts towards the badge: the switch decides
   * what the sum includes, and flipping it on must not have to wait for the next fetch.
   */
  function setExpirationCount(value: number) {
    expirationCount.value = clamp(value)
  }

  /** Publish the overdue shopping-list count — the number the shopping-list nav badge shows. */
  function setDeadlineCount(value: number) {
    deadlineCount.value = clamp(value)
  }

  /** Publish unread family chat messages — the number on the chat bubble (#149). */
  function setChatUnreadCount(value: number) {
    chatUnreadCount.value = clamp(value)
  }

  /** Turns one source on or off, for this device. */
  function setSource(key: keyof BadgeSources, enabled: boolean) {
    sources.value = { ...sources.value, [key]: enabled }
    persistSources()
  }

  return {
    count,
    titleTemplate,
    isSupported: computed(badgingSupported),
    sources: computed(() => sources.value),
    setSource,
    resolveIconPermission,
    setExpirationCount,
    setDeadlineCount,
    setChatUnreadCount
  }
}
