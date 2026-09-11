/**
 * The notification centre's state (#116): the inbox, its unread count, and the drawer's open flag.
 *
 * Module-scoped, because three separate places talk to the same inbox — the header bell (unread
 * badge, opens it), the drawer (the list), and the service-worker bridge that wakes it when a push
 * arrives while the app is open. A per-component copy would give the bell a badge that disagrees
 * with the list underneath it.
 *
 * The unread count is *never* derived from `items`. The list is one page deep and the count is over
 * the whole inbox, so counting what happens to be loaded would under-report the moment there is
 * more than a page of unread rows. Every endpoint that changes read state answers with the new
 * number, and that number is what this stores.
 */
import { computed, ref } from 'vue'
import { useNotificationsApi } from '~/composables/api/useNotificationsApi'
import type { NotificationInfo } from '~/types/notification'

/** Rows per page. Deep enough that "a few days of notifications" is one request. */
const PAGE_SIZE = 25

const items = ref<NotificationInfo[]>([])
const unreadCount = ref(0)
const nextCursor = ref<string | null>(null)
const isLoading = ref(false)
const isLoadingMore = ref(false)
const hasLoaded = ref(false)
const isOpen = ref(false)

/** Guards the one-time service-worker listener. */
let bridgeInstalled = false

export const useNotificationCenter = () => {
  const { getNotifications, getUnreadCount, markRead, markAllRead, dismiss } = useNotificationsApi()

  const hasMore = computed(() => nextCursor.value !== null)

  /** Refresh the badge only. Cheap enough for boot and for every push arrival. */
  async function refreshUnreadCount() {
    const res = await getUnreadCount()
    if (res?.success && res.data) unreadCount.value = res.data.unreadCount
  }

  /** Load (or reload) the first page, replacing whatever is in the list. */
  async function loadFirstPage() {
    isLoading.value = true
    try {
      const res = await getNotifications(null, PAGE_SIZE)
      if (res?.success && res.data) {
        items.value = res.data.items
        nextCursor.value = res.data.nextCursor
        unreadCount.value = res.data.unreadCount
        hasLoaded.value = true
      }
    } finally {
      isLoading.value = false
    }
  }

  /** Append the next page. A no-op at the end of the list or while one is already in flight. */
  async function loadMore() {
    if (!nextCursor.value || isLoadingMore.value) return
    isLoadingMore.value = true
    try {
      const res = await getNotifications(nextCursor.value, PAGE_SIZE)
      if (res?.success && res.data) {
        // Filter by id rather than trusting the cursor blindly: a notification delivered between
        // the two requests shifts nothing (the cursor is a row, not an offset), but a *dismissal*
        // from another device can, and a duplicate key would break the transition group.
        const known = new Set(items.value.map(item => item.publicId))
        items.value = [...items.value, ...res.data.items.filter(item => !known.has(item.publicId))]
        nextCursor.value = res.data.nextCursor
        unreadCount.value = res.data.unreadCount
      }
    } finally {
      isLoadingMore.value = false
    }
  }

  /**
   * Open the drawer. Always refetches the first page: the inbox changes from out-of-process
   * workers, so a list from the last time it was opened is stale by definition.
   */
  async function open() {
    isOpen.value = true
    await loadFirstPage()
  }

  function close() {
    isOpen.value = false
  }

  /**
   * Mark one row read, optimistically.
   *
   * The row is flipped locally first so the tap feels immediate — this is normally called on the
   * way to navigating somewhere, and the row will not be on screen long enough for a round trip
   * to land. The server's answer then corrects the badge, which is the number that has to be
   * right; if the call failed, the next open reloads the truth.
   */
  async function markOneRead(publicId: string) {
    const row = items.value.find(item => item.publicId === publicId)
    if (!row || row.isRead) return

    row.isRead = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)

    const res = await markRead(publicId)
    if (res?.success && res.data) unreadCount.value = res.data.unreadCount
  }

  /** Mark everything read. */
  async function markEverythingRead() {
    if (unreadCount.value === 0) return

    for (const item of items.value) item.isRead = true
    unreadCount.value = 0

    await markAllRead()
  }

  /**
   * Dismiss one row — swipe-to-dismiss. Removed from the list optimistically, since the gesture
   * has already animated it away and putting it back would be worse than a stale list.
   */
  async function dismissOne(publicId: string) {
    const index = items.value.findIndex(item => item.publicId === publicId)
    if (index === -1) return

    const [removed] = items.value.splice(index, 1)
    if (removed && !removed.isRead) unreadCount.value = Math.max(0, unreadCount.value - 1)

    const res = await dismiss(publicId)
    if (res?.success && res.data) unreadCount.value = res.data.unreadCount
  }

  /**
   * A push arrived while the app was open.
   *
   * The badge is always refreshed. The list is only reloaded when the drawer is open, and the new
   * rows are prepended rather than replacing the list — replacing it would discard the "load more"
   * pages the reader had already scrolled through, and reset their scroll position under them.
   */
  async function handlePushArrival() {
    if (!isOpen.value) {
      await refreshUnreadCount()
      return
    }

    const res = await getNotifications(null, PAGE_SIZE)
    if (!res?.success || !res.data) return

    const known = new Set(items.value.map(item => item.publicId))
    const fresh = res.data.items.filter(item => !known.has(item.publicId))

    if (fresh.length > 0) items.value = [...fresh, ...items.value]
    unreadCount.value = res.data.unreadCount
  }

  /**
   * Listen for the service worker's push announcement (see `public/sw-push.js`). Called once from
   * the app header, which is mounted for the whole authenticated session.
   *
   * A message from the service worker, not a `push` event: a page cannot observe push delivery
   * directly. The message carries no content on purpose — the row is already in the database, and
   * the page fetches it, so there is only ever one description of a notification.
   */
  function installServiceWorkerBridge() {
    if (!import.meta.client || bridgeInstalled) return
    if (!('serviceWorker' in navigator)) return

    bridgeInstalled = true
    navigator.serviceWorker.addEventListener('message', (event: MessageEvent) => {
      if (event.data?.type !== 'homassy:notification') return
      handlePushArrival()

      // A push reached this device, so something happened that the socket may not have delivered -
      // a suspended tab receives a push but no hub broadcast. The chat's own unread count is the
      // other badge on screen, and re-reading it is one request.
      void useFamilyChat().refreshUnreadCount()
    })
  }

  return {
    items: computed(() => items.value),
    unreadCount: computed(() => unreadCount.value),
    isLoading: computed(() => isLoading.value),
    isLoadingMore: computed(() => isLoadingMore.value),
    hasLoaded: computed(() => hasLoaded.value),
    isOpen: computed(() => isOpen.value),
    hasMore,
    open,
    close,
    loadFirstPage,
    loadMore,
    refreshUnreadCount,
    markOneRead,
    markEverythingRead,
    dismissOne,
    installServiceWorkerBridge
  }
}
