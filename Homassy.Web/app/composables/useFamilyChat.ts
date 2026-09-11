/**
 * The family conversation itself (#146): the stream, its paging, and the writes.
 *
 * Module-scoped, like the socket composables it sits on: the panel can be closed and reopened
 * (and, on a route change, remounted) without throwing away the history already loaded or the
 * scroll position that goes with it.
 *
 * Where the messages come from:
 *
 * - **Opening** joins the hub, and the join *is* the first page (`JoinChat` answers with it), so
 *   there is one round trip rather than a join plus a fetch. With no socket - SSR, or a hub that
 *   will not connect - it falls back to REST, and the stream is then simply not live.
 * - **Older pages** always come over REST, cursor by cursor.
 * - **New messages** arrive on `MessageCreated`, including the caller's own. That is what makes
 *   the correlation id necessary: the sender appended the message optimistically before the
 *   request even left, so without something to match on they would see it twice.
 */
import { computed, ref } from 'vue'
import { useAuthStore } from '~/stores/auth'
import type {
  FamilyChatMessage,
  FamilyChatMessageCreatedEvent,
  FamilyChatMessageDeletedEvent,
  FamilyChatStreamMessage,
  FamilyChatTypingMember
} from '~/types/familyChat'

/** How many messages a page of history carries. The API clamps anything larger. */
const PAGE_SIZE = 30

/** Oldest first - the order the stream renders in, and the reverse of what the API returns. */
const messages = ref<FamilyChatStreamMessage[]>([])
const olderCursor = ref<string | null>(null)
const loading = ref(false)
const loadingOlder = ref(false)
const failedToLoad = ref(false)
/** True once a join or a fetch has filled the stream, so reopening the panel does not reload it. */
const hydrated = ref(false)
let subscribed = false

/**
 * Who is typing right now, as the server last reported it (#148).
 *
 * Module-scoped with the rest of the stream, because the bubble needs it too: while the panel is
 * closed the bubble carries a typing pulse, and that is the same fact as the dots above the
 * composer.
 */
const typingMembers = ref<FamilyChatTypingMember[]>([])

/** At most one `SetTyping(true)` per this many ms while the composer has content. */
const TYPING_THROTTLE_MS = 2000
/** Silence after which the client reports that typing has stopped. */
const TYPING_STOP_MS = 4000

let lastTypingSentAt = 0
let typingStopTimer: ReturnType<typeof setTimeout> | null = null

/** Unread messages, as the server counts them (#149). Drives the badge on the bubble. */
const unreadCount = ref(0)

/**
 * Whether this client is currently telling the server it is watching the conversation (#149).
 *
 * Held so the state can be re-reported after a reconnect: per-connection flags die with the
 * connection, and a rebuilt socket would otherwise leave a reader looking at the chat while the
 * server believes nobody is.
 */
let reportedActive = false
/** Whether the panel is currently open, for the visibility handler to key off. */
let panelIsOpen = false
/** Keeps the server's active flag alive while the panel is open and visible. */
let heartbeatTimer: ReturnType<typeof setInterval> | null = null
/** Stops counting a panel left open on a desk as somebody reading it. */
let inactivityTimer: ReturnType<typeof setTimeout> | null = null
let visibilityListenerAttached = false

/** Refresh interval for the active flag - comfortably inside the server's TTL. */
const ACTIVE_HEARTBEAT_MS = 20_000
/** How long a panel can sit open and untouched before it stops counting as watched. */
const ACTIVE_IDLE_MS = 5 * 60_000

export const useFamilyChat = () => {
  const socket = useFamilyChatSocket()
  const {
    getMessages,
    sendMessage,
    sendImageMessage,
    getUnreadCount,
    markRead: markReadApi,
    deleteMessage
  } = useFamilyChatApi()
  const authStore = useAuthStore()

  const currentUserPublicId = computed(() => authStore.user?.publicId ?? null)

  const hasOlder = computed(() => olderCursor.value !== null)

  /** The API answers newest-first; the stream renders oldest-first. */
  const toStreamOrder = (page: FamilyChatMessage[]): FamilyChatStreamMessage[] =>
    [...page].reverse()

  const indexOfMessage = (publicId: string): number =>
    messages.value.findIndex(m => m.publicId === publicId)

  // --- Live events ---------------------------------------------------------

  const onMessageCreated = (event: FamilyChatMessageCreatedEvent): void => {
    const { message, correlationId } = event

    // The sender's own message, already on screen as a pending bubble. Reconcile rather than
    // append: the optimistic row is what the reader has been looking at, and replacing it in
    // place keeps its position in the stream.
    if (correlationId) {
      const pendingIndex = messages.value.findIndex(m => m.correlationId === correlationId)
      if (pendingIndex >= 0) {
        messages.value[pendingIndex] = { ...message, sendState: 'sent', correlationId }
        return
      }
    }

    // Already here - the POST's own response arrived before the broadcast, which is the usual
    // race on a fast connection.
    if (indexOfMessage(message.publicId) >= 0) return

    messages.value.push(message)

    // Somebody else's message, arriving while nobody is watching (the panel is closed, or the tab
    // is in the background): the badge is the only thing that will say so until it is read.
    if (message.sender.publicId !== currentUserPublicId.value && !reportedActive) {
      unreadCount.value++
    }
  }

  const onMessageDeleted = (event: FamilyChatMessageDeletedEvent): void => {
    const index = indexOfMessage(event.publicId)
    if (index >= 0) messages.value.splice(index, 1)
  }

  /**
   * The server sends the whole typing set, never a diff, so a client that missed an event still
   * ends up correct. The caller is already excluded server-side; filtering again here covers the
   * other half of the same rule - your *other* device typing is still you.
   */
  const onTypingChanged = (members: FamilyChatTypingMember[]): void => {
    typingMembers.value = members.filter(m => m.publicId !== currentUserPublicId.value)
  }

  const subscribe = (): void => {
    if (subscribed) return
    socket.on('MessageCreated', onMessageCreated)
    socket.on('MessageDeleted', onMessageDeleted)
    socket.on('TypingChanged', onTypingChanged)
    socket.onReconnected(rejoin)
    subscribed = true
  }

  const unsubscribe = (): void => {
    if (!subscribed) return
    socket.off('MessageCreated', onMessageCreated)
    socket.off('MessageDeleted', onMessageDeleted)
    socket.off('TypingChanged', onTypingChanged)
    socket.offReconnected(rejoin)
    subscribed = false
  }

  // --- Typing (#148) -------------------------------------------------------

  /**
   * Reports that the composer has something in it, at most once every couple of seconds.
   *
   * Throttled rather than sent per keystroke: the server flag lasts five seconds, so a message
   * every two keeps it alive with a fraction of the traffic - and the indicator is a hint, not a
   * transcript. Every call also re-arms the stop timer, which is what turns "stopped typing" into
   * something the client notices rather than something the server has to wait out.
   */
  const notifyTyping = (): void => {
    const now = Date.now()

    if (now - lastTypingSentAt > TYPING_THROTTLE_MS) {
      lastTypingSentAt = now
      void socket.invokeQuietly('SetTyping', true)
    }

    if (typingStopTimer) clearTimeout(typingStopTimer)
    typingStopTimer = setTimeout(stopTyping, TYPING_STOP_MS)
  }

  /**
   * Reports that typing has stopped — on send, on losing focus with an empty input, and after a
   * few seconds of silence.
   *
   * Best-effort by design: the server flag expires on its own, so a call that never arrives costs
   * a few seconds of a stale indicator rather than a permanent one.
   */
  const stopTyping = (): void => {
    if (typingStopTimer) {
      clearTimeout(typingStopTimer)
      typingStopTimer = null
    }

    lastTypingSentAt = 0
    void socket.invokeQuietly('SetTyping', false)
  }

  // --- Watching and read state (#149) --------------------------------------

  /**
   * Tells the server whether this client is actually watching the conversation.
   *
   * "Actively watching" means the panel is open *and* the document is visible, which only the
   * client knows: the socket is an app-wide singleton and a backgrounded tab keeps a WebSocket
   * alive, so being connected says nothing about whether anyone is looking. Reporting this
   * honestly is what decides whether a message notifies - so over-reporting it would swallow
   * notifications for somebody who is not there.
   */
  const setActive = (isActive: boolean): void => {
    reportedActive = isActive
    void socket.invokeQuietly('SetChatActive', isActive)
  }

  /** Refreshes the server's TTL so a reader who is not typing or scrolling still counts. */
  const startHeartbeat = (): void => {
    stopHeartbeat()
    heartbeatTimer = setInterval(() => {
      if (!import.meta.client) return
      // Only while the tab is visible: a heartbeat from a backgrounded tab would be the exact
      // lie this flag exists to avoid.
      if (document.visibilityState === 'visible') setActive(true)
    }, ACTIVE_HEARTBEAT_MS)
  }

  const stopHeartbeat = (): void => {
    if (heartbeatTimer) {
      clearInterval(heartbeatTimer)
      heartbeatTimer = null
    }
  }

  /**
   * Re-arms the "nobody is really here" timer.
   *
   * A panel left open on a desk would otherwise suppress every notification for as long as the
   * app is running, which is the opposite of what a reader who walked away wants.
   */
  const armInactivityTimeout = (): void => {
    if (inactivityTimer) clearTimeout(inactivityTimer)
    inactivityTimer = setTimeout(() => {
      setActive(false)
      stopHeartbeat()
    }, ACTIVE_IDLE_MS)
  }

  const clearInactivityTimeout = (): void => {
    if (inactivityTimer) {
      clearTimeout(inactivityTimer)
      inactivityTimer = null
    }
  }

  /**
   * Backgrounding the tab or locking the phone flips the flag to inactive even with the panel
   * still open - the panel being open is not the same as somebody looking at it.
   */
  const onVisibilityChange = (): void => {
    if (!panelIsOpen) return

    if (document.visibilityState === 'visible') {
      setActive(true)
      startHeartbeat()
      armInactivityTimeout()
      void markRead()
    } else {
      setActive(false)
      stopHeartbeat()
      clearInactivityTimeout()
    }
  }

  const attachVisibilityListener = (): void => {
    if (visibilityListenerAttached || !import.meta.client) return
    document.addEventListener('visibilitychange', onVisibilityChange)
    visibilityListenerAttached = true
  }

  const detachVisibilityListener = (): void => {
    if (!visibilityListenerAttached || !import.meta.client) return
    document.removeEventListener('visibilitychange', onVisibilityChange)
    visibilityListenerAttached = false
  }

  /** Re-reads the unread count from the server. Never derived from the loaded stream - that is one page deep. */
  const refreshUnreadCount = async (): Promise<void> => {
    const response = await getUnreadCount().catch(() => null)
    if (response?.success && response.data) {
      unreadCount.value = response.data.totalCount
    }
  }

  /**
   * Marks the conversation read up to now.
   *
   * Called when the newest message is actually on screen, not on mount: a panel opened in a
   * background tab, or one scrolled far back through history, has not shown the reader anything
   * new, and clearing the badge there would lose the only signal that something arrived.
   */
  const markRead = async (): Promise<void> => {
    const response = await markReadApi().catch(() => null)
    if (response?.success && response.data) {
      unreadCount.value = response.data.totalCount
    }
  }

  /**
   * Re-joins after an automatic reconnect and reloads the newest page.
   *
   * The reload is the point: a connection that was down missed every broadcast it was down for,
   * and a stream that silently skips those messages is worse than one that pauses.
   */
  const rejoin = (): void => {
    void refresh()

    // Per-connection state died with the old connection: the group membership, the typing flag and
    // the "actively watching" flag. Re-report what this client still believes is true, or the
    // server goes on notifying a reader who is sitting in front of the conversation.
    if (reportedActive) setActive(true)
  }

  // --- Loading -------------------------------------------------------------

  /** Fetches the newest page over REST, for when the socket is unavailable. */
  const fetchNewestPage = async (): Promise<boolean> => {
    const response = await getMessages(null, PAGE_SIZE)
    if (!response.success || !response.data) return false

    messages.value = toStreamOrder(response.data.items)
    olderCursor.value = response.data.nextCursor ?? null
    return true
  }

  /**
   * Opens the conversation: joins the hub and fills the stream.
   *
   * Safe to call again - a second open with the stream already hydrated re-joins (the group is
   * connection-scoped) but does not refetch what is already on screen.
   */
  const open = async (): Promise<void> => {
    subscribe()
    panelIsOpen = true
    attachVisibilityListener()

    if (loading.value) return
    loading.value = true
    failedToLoad.value = false

    try {
      const page = await socket.joinChat()

      if (page) {
        messages.value = toStreamOrder(page.items)
        olderCursor.value = page.nextCursor ?? null
        hydrated.value = true
        return
      }

      // No socket: the stream still works, it just is not live.
      const ok = await fetchNewestPage()
      hydrated.value = ok
      failedToLoad.value = !ok
    } catch {
      // A hub that refuses the join (no family, or the connection failed outright) still leaves
      // REST as a way to read the conversation.
      const ok = await fetchNewestPage().catch(() => false)
      hydrated.value = ok
      failedToLoad.value = !ok
    } finally {
      loading.value = false

      // Reported after the join, not before it: the flag is per connection, and a connection that
      // is not in the group yet is not watching anything.
      if (import.meta.client && document.visibilityState === 'visible') {
        setActive(true)
        startHeartbeat()
        armInactivityTimeout()
      }
    }
  }

  /** Leaves the hub group. The stream is kept, so reopening the panel is instant. */
  const close = async (): Promise<void> => {
    // Closing the panel ends any typing this client was reporting; the server clears the flag on
    // leave too, but saying so first means the family sees it go immediately.
    stopTyping()
    typingMembers.value = []

    panelIsOpen = false
    setActive(false)
    stopHeartbeat()
    clearInactivityTimeout()
    detachVisibilityListener()

    await socket.leaveChat()
  }

  /** Re-reads the newest page, keeping older pages that are already loaded out of it. */
  const refresh = async (): Promise<void> => {
    try {
      const page = await socket.joinChat()
      if (page) {
        messages.value = toStreamOrder(page.items)
        olderCursor.value = page.nextCursor ?? null
        hydrated.value = true
        return
      }
    } catch {
      // Fall through to REST below.
    }

    await fetchNewestPage()
  }

  /**
   * Prepends the next older page.
   *
   * The caller is responsible for holding the scroll position across the prepend - only it knows
   * the viewport, and a stream that jumps when older messages load is a stream nobody can read
   * backwards.
   */
  const loadOlder = async (): Promise<void> => {
    if (loadingOlder.value || !olderCursor.value) return

    loadingOlder.value = true
    try {
      const response = await getMessages(olderCursor.value, PAGE_SIZE)
      if (!response.success || !response.data) return

      const older = toStreamOrder(response.data.items)
      // Filter rather than trust: a message can arrive over the socket while a page that also
      // contains it is in flight.
      const known = new Set(messages.value.map(m => m.publicId))
      messages.value = [...older.filter(m => !known.has(m.publicId)), ...messages.value]
      olderCursor.value = response.data.nextCursor ?? null
    } finally {
      loadingOlder.value = false
    }
  }

  // --- Writes --------------------------------------------------------------

  const newCorrelationId = (): string =>
    (import.meta.client && 'randomUUID' in crypto)
      ? crypto.randomUUID()
      : `c-${Date.now()}-${Math.random().toString(36).slice(2)}`

  /**
   * Appends a message optimistically and sends it.
   *
   * The optimistic row is not a nicety: the socket round trip is what would otherwise sit between
   * pressing send and seeing the message, and a chat that lags its own sender feels broken even
   * when it is fast. The row carries a correlation id so the broadcast - which the sender also
   * receives - reconciles onto it rather than appending a duplicate.
   */
  const send = async (body: string): Promise<boolean> => {
    const text = body.trim()
    if (!text) return false

    const correlationId = newCorrelationId()
    const optimistic: FamilyChatStreamMessage = {
      publicId: correlationId,
      kind: 'Text',
      body: text,
      sentAt: new Date().toISOString(),
      sender: {
        publicId: currentUserPublicId.value ?? '',
        displayName: authStore.user?.displayName || authStore.user?.name || '',
        profilePictureUrl: authStore.user?.profilePictureUrl ?? null,
        identityColor: authStore.user?.identityColor ?? null
      },
      sendState: 'pending',
      correlationId
    }

    messages.value.push(optimistic)
    // Having sent it, this client is no longer typing it. The server clears the flag on the write
    // path too (#148); this is the half that does not wait for the round trip.
    stopTyping()

    const response = await sendMessage({ body: text, correlationId }).catch(() => null)
    const index = messages.value.findIndex(m => m.correlationId === correlationId)

    if (!response?.success || !response.data) {
      // Left in place as a failed bubble with a retry on it, rather than removed: the text the
      // user wrote is in it, and dropping it silently loses what they typed.
      if (index >= 0) messages.value[index] = { ...messages.value[index]!, sendState: 'failed' }
      return false
    }

    // The broadcast may have reconciled this row already; if so the id below is the real one and
    // this write is a no-op rather than a duplicate.
    if (index >= 0) {
      messages.value[index] = { ...response.data, sendState: 'sent', correlationId }
    }

    return true
  }

  /**
   * Appends a picture optimistically and uploads it (#147).
   *
   * The optimistic row carries the local `data:` URL the picker produced, so the sender sees
   * their own photo immediately instead of a grey box for the length of the upload. It is
   * replaced wholesale by the committed message, which carries an image *URL* — that is the
   * moment the base64 stops being held in memory.
   *
   * There is no byte-level progress bar: the upload is one JSON POST through the shared API
   * client, which reports none. The pictures are resized and compressed on this side before they
   * are sent, so the wait is short enough that a spinner on the bubble says as much as a bar
   * would; the async job pipeline that *does* report progress (`/image/upload-async`) costs a
   * second round trip and a poll loop, which is the wrong trade for a chat photo.
   */
  const sendImage = async (imageBase64: string, previewDataUrl: string, caption?: string): Promise<boolean> => {
    const correlationId = newCorrelationId()
    const optimistic: FamilyChatStreamMessage = {
      publicId: correlationId,
      kind: 'Image',
      body: caption?.trim() || null,
      sentAt: new Date().toISOString(),
      sender: {
        publicId: currentUserPublicId.value ?? '',
        displayName: authStore.user?.displayName || authStore.user?.name || '',
        profilePictureUrl: authStore.user?.profilePictureUrl ?? null,
        identityColor: authStore.user?.identityColor ?? null
      },
      sendState: 'pending',
      correlationId,
      localPreview: previewDataUrl
    }

    messages.value.push(optimistic)

    const response = await sendImageMessage({
      imageBase64,
      caption: caption?.trim() || undefined,
      correlationId
    }).catch(() => null)

    const index = messages.value.findIndex(m => m.correlationId === correlationId)

    if (!response?.success || !response.data) {
      // Kept, with its preview, so "retry" can send the same picture rather than asking the user
      // to find it again.
      if (index >= 0) messages.value[index] = { ...messages.value[index]!, sendState: 'failed' }
      return false
    }

    if (index >= 0) {
      messages.value[index] = { ...response.data, sendState: 'sent', correlationId }
    }

    return true
  }

  /**
   * Re-sends a failed message, in place.
   *
   * A failed picture is re-sent from the preview it is still showing - that data URL is the
   * cropped image itself, which is why the failed row keeps it rather than asking the sender to
   * find the photo again.
   */
  const retry = async (message: FamilyChatStreamMessage): Promise<boolean> => {
    if (message.sendState !== 'failed') return false

    const index = messages.value.findIndex(m => m.publicId === message.publicId)
    if (index >= 0) messages.value.splice(index, 1)

    if (message.kind === 'Image' && message.localPreview) {
      const base64 = message.localPreview.includes(',')
        ? message.localPreview.split(',')[1]!
        : message.localPreview
      return await sendImage(base64, message.localPreview, message.body ?? undefined)
    }

    if (!message.body) return false
    return await send(message.body)
  }

  /**
   * Deletes one of the caller's own messages.
   *
   * Removed locally only after the API accepts it: the row is other people's too, and a message
   * that vanishes from your screen but stays on theirs is the wrong way round.
   */
  const remove = async (publicId: string): Promise<boolean> => {
    const response = await deleteMessage(publicId)
    if (!response.success) return false

    const index = indexOfMessage(publicId)
    if (index >= 0) messages.value.splice(index, 1)
    return true
  }

  /** Drops a failed message without sending it. */
  const discard = (publicId: string): void => {
    const index = indexOfMessage(publicId)
    if (index >= 0) messages.value.splice(index, 1)
  }

  const isOwn = (message: FamilyChatMessage): boolean =>
    message.sender.publicId === currentUserPublicId.value

  return {
    messages,
    loading,
    loadingOlder,
    failedToLoad,
    hydrated,
    hasOlder,
    isConnected: socket.isConnected,
    currentUserPublicId,
    typingMembers,
    notifyTyping,
    stopTyping,
    unreadCount,
    refreshUnreadCount,
    markRead,
    armInactivityTimeout,
    open,
    close,
    refresh,
    loadOlder,
    send,
    sendImage,
    retry,
    remove,
    discard,
    isOwn,
    unsubscribe
  }
}
