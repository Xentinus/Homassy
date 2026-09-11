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
import { FamilyChatMessageKind } from '~/types/enums'
import { useAuthStore } from '~/stores/auth'
import type {
  FamilyChatActiveMember,
  FamilyChatMessage,
  FamilyChatMessageCreatedEvent,
  FamilyChatMessageDeletedEvent,
  FamilyChatReferenceDraft,
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
/**
 * True once a join or a fetch has actually answered.
 *
 * It distinguishes "never loaded" from "loaded and the conversation is empty" - the two look
 * identical in `messages` and mean opposite things to anything rendering an empty state. It does
 * **not** gate reloading: see `open`, which replaces the stream on purpose.
 */
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

/**
 * Who is watching the conversation right now, excluding this client's own user.
 *
 * The count the bubble shows. It is the server's set, never a guess: the same flag decides whether
 * a message notifies, so anything derived locally could tell one member that another is reading
 * while the server is busy sending that other member a push.
 */
const activeMembers = ref<FamilyChatActiveMember[]>([])

/** True once this client is in the family's hub group, so the bubble can join without opening the panel. */
let joined = false
let joinPromise: Promise<void> | null = null

/**
 * Bumped every time the client leaves the group.
 *
 * A join is two awaits deep before it can record that it succeeded, and `leave()` is
 * fire-and-forget from the bubble's unmount - so a join in flight when the layout is torn down
 * (a logout, an auth redirect) would otherwise resolve *after* the leave and set `joined` back to
 * true for a group the server has already dropped this connection from. Since `joined` is
 * module-scoped, the next mount would then skip joining entirely and the bubble would sit there
 * silent. A join only records its result if no leave happened while it was waiting.
 */
let membershipGeneration = 0

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
/**
 * The visibility handler currently registered on `document`, if any.
 *
 * The handler itself is a closure over one `useFamilyChat()` call, and more than one component
 * calls it (the panel drives the lifecycle, the bubble reads the badge). Storing the exact
 * function that was added - rather than a boolean saying one was - is what makes
 * `removeEventListener` able to remove it: passing a different invocation's closure would remove
 * nothing and leave a listener reporting activity for a panel that is closed.
 */
let attachedVisibilityHandler: (() => void) | null = null

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

  /**
   * The server sends the whole watching set, this client included - it is part of the answer, and
   * every client filters itself out for display rather than each being sent a different list.
   */
  const onActiveChanged = (members: FamilyChatActiveMember[]): void => {
    activeMembers.value = members.filter(m => m.publicId !== currentUserPublicId.value)
  }

  const subscribe = (): void => {
    if (subscribed) return
    socket.on('MessageCreated', onMessageCreated)
    socket.on('MessageDeleted', onMessageDeleted)
    socket.on('TypingChanged', onTypingChanged)
    socket.on('ActiveChanged', onActiveChanged)
    socket.onReconnected(rejoin)
    subscribed = true
  }

  const unsubscribe = (): void => {
    if (!subscribed) return
    socket.off('MessageCreated', onMessageCreated)
    socket.off('MessageDeleted', onMessageDeleted)
    socket.off('TypingChanged', onTypingChanged)
    socket.off('ActiveChanged', onActiveChanged)
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
    if (!panelIsOpen) {
      // The panel is closed, but the app has just come back to the foreground - and while it was
      // backgrounded the socket may have been suspended or dropped, so every `MessageCreated`
      // that would have counted into the badge was missed. The server's count is the only one
      // that survived that, so ask it. (Reconnecting re-joins the group but says nothing about
      // what arrived while it was gone.)
      if (document.visibilityState === 'visible') void refreshUnreadCount()
      return
    }

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
    if (attachedVisibilityHandler || !import.meta.client) return
    attachedVisibilityHandler = onVisibilityChange
    document.addEventListener('visibilitychange', attachedVisibilityHandler)
  }

  const detachVisibilityListener = (): void => {
    if (!attachedVisibilityHandler || !import.meta.client) return
    document.removeEventListener('visibilitychange', attachedVisibilityHandler)
    attachedVisibilityHandler = null
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

    // The badge counted the broadcasts this client was there for, and it was there for none of
    // them while the connection was down. Only the server knows what arrived in the gap.
    void refreshUnreadCount()

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
   * Opens the conversation: joins the hub and fills the stream with the newest page.
   *
   * **Reopening replaces the stream rather than merging into it**, which does mean older pages the
   * reader had scrolled back to are dropped and have to be paged again. That is the deliberate
   * choice: while the panel was closed this client received nothing, so an arbitrary number of
   * messages can sit between the tail it still holds and the page it just fetched. Merging the two
   * would splice them into one apparently continuous conversation with an invisible gap in the
   * middle - a stream that is wrong in a way nobody can see. Re-paging is cheap; a silent gap is
   * not recoverable.
   */
  const open = async (): Promise<void> => {
    subscribe()
    panelIsOpen = true
    attachVisibilityListener()

    if (loading.value) return
    loading.value = true
    failedToLoad.value = false

    const generation = membershipGeneration

    try {
      // Re-joining an already-joined group is a no-op server-side, and it is what makes opening
      // the panel answer with a page that is current rather than whatever the bubble joined with.
      const page = await socket.joinChat()

      if (page) {
        // Same generation guard as `join`: a leave during this await means the group is gone.
        if (generation === membershipGeneration) joined = true
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
  /**
   * Joins the family's hub group without opening the panel.
   *
   * What the bubble calls on mount, and the reason the group membership outlives the panel: the
   * bubble has to know who is watching and whether anybody is typing *while the chat is closed* -
   * that is the whole point of showing it there. Group membership is not the same as watching
   * (#149): joining reports nothing about attention, so this does not suppress a single
   * notification.
   *
   * Idempotent, and safe to call concurrently - a second call rides the first one's promise rather
   * than opening a second connection.
   */
  const join = async (): Promise<void> => {
    if (!import.meta.client) return

    subscribe()
    // Attached from here, not only from `open`: the badge has to be right after the app comes
    // back from the background whether or not the panel was ever opened.
    attachVisibilityListener()
    if (joined) return
    if (joinPromise) return joinPromise

    const generation = membershipGeneration

    joinPromise = (async () => {
      try {
        const page = await socket.joinChat()
        if (!page) return

        // Left while this was in flight: the server has dropped the group, so recording a join
        // would leave this client believing it is in a group it is not.
        if (generation !== membershipGeneration) return

        joined = true
        // The join answers with the newest page, so keeping it costs nothing and buys two things:
        // opening the panel is instant, and live arrivals can be counted into the unread badge
        // from the moment the app started rather than from the first time the chat was opened.
        messages.value = toStreamOrder(page.items)
        olderCursor.value = page.nextCursor ?? null
        hydrated.value = true
      } catch {
        // No family, or the hub refused the join. Either way there is nothing to watch, and the
        // bubble is not rendered for a user with no family anyway.
      } finally {
        joinPromise = null
      }
    })()

    return joinPromise
  }

  /**
   * Leaves the group entirely - the bubble is going away (logout, or the layout unmounting).
   *
   * Closing the panel deliberately does **not** do this: see `close`.
   */
  const leave = async (): Promise<void> => {
    // Bumped first, so a join already waiting on the socket cannot record itself afterwards.
    membershipGeneration++
    joined = false
    typingMembers.value = []
    activeMembers.value = []
    detachVisibilityListener()
    await socket.leaveChat()
  }

  /**
   * Closes the panel: stops reporting typing and watching, and stops the heartbeat.
   *
   * It does **not** leave the hub group. The bubble stays on screen and goes on showing who is
   * watching and whether somebody is typing, which it can only do from inside the group - and
   * leaving would also throw away the live `MessageCreated` events the unread badge counts.
   */
  const close = async (): Promise<void> => {
    // Closing the panel ends any typing this client was reporting; saying so explicitly means the
    // family sees the indicator go rather than waiting out the TTL.
    stopTyping()

    panelIsOpen = false
    setActive(false)
    stopHeartbeat()
    clearInactivityTimeout()
    // The visibility listener stays: with the panel closed its job is keeping the unread badge
    // honest across a backgrounded app, which is exactly when it matters most. `leave` is what
    // takes it down.

    await Promise.resolve()
  }

  /** Re-reads the newest page, keeping older pages that are already loaded out of it. */
  const refresh = async (): Promise<void> => {
    const generation = membershipGeneration

    try {
      const page = await socket.joinChat()
      if (page) {
        if (generation === membershipGeneration) joined = true
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
  const send = async (body: string, references: FamilyChatReferenceDraft[] = []): Promise<boolean> => {
    const text = body.trim()
    // Something attached is something to send: the API accepts a message with no body when it
    // carries references, and "the shop" plus "the milk" is a complete thought.
    if (!text && references.length === 0) return false

    const correlationId = newCorrelationId()
    const optimistic: FamilyChatStreamMessage = {
      publicId: correlationId,
      kind: FamilyChatMessageKind.Text,
      body: text || null,
      // Rendered from what the picker offered until the committed message replaces them with the
      // server's own labels - which is also when they stop being assumed to resolve.
      references: references.map(r => ({ ...r, isAvailable: true })),
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

    const response = await sendMessage({
      body: text || undefined,
      references: references.map(r => ({ kind: r.kind, publicId: r.publicId })),
      correlationId
    }).catch(() => null)
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
      kind: FamilyChatMessageKind.Image,
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

    if (message.kind === FamilyChatMessageKind.Image && message.localPreview) {
      const base64 = message.localPreview.includes(',')
        ? message.localPreview.split(',')[1]!
        : message.localPreview
      return await sendImage(base64, message.localPreview, message.body ?? undefined)
    }

    // A failed message keeps whatever it was attached to, so retrying re-sends the whole thing
    // rather than a sentence with its chips dropped.
    if (!message.body && (message.references?.length ?? 0) === 0) return false
    return await send(message.body ?? '', message.references ?? [])
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
    activeMembers,
    notifyTyping,
    stopTyping,
    join,
    leave,
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
