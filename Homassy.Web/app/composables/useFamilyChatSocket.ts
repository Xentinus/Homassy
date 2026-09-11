/**
 * Family chat realtime socket (SignalR), #144.
 *
 * A single app-wide connection to the API's `/hubs/family-chat` hub, in the same module-singleton
 * shape as `useShoppingListSocket` and `useInventorySocket`: one connection whatever number of
 * components ask for it, automatic reconnect, client-only.
 *
 * Two differences from its siblings, both because the chat is family-scoped rather than
 * resource-scoped:
 *
 * - **`joinChat()` takes no argument.** The group is derived server-side from the session, so
 *   there is no id to pass — and no id a client could substitute for another family's.
 * - **Joining is deferred to when the panel opens**, not done on connect. The bubble is on screen
 *   everywhere, but a socket group is only worth holding while somebody is reading, and #149's
 *   "actively watching" flag is tied to the same moment.
 *
 * Auth: the Kratos session cookie rides the WS handshake via `withCredentials`, so the existing
 * server-side session middleware authenticates the connection — no token plumbing.
 *
 * On the server the methods are no-ops and `joinChat` resolves to `null`, so callers fall back to
 * a REST fetch.
 */
import * as signalR from '@microsoft/signalr'
import { ref } from 'vue'
import type { HubEventHandler, SignalRHandler } from '~/types/realtime'
import type { FamilyChatPage } from '~/types/familyChat'
import type { HubState } from '~/utils/realtimeStatus'

// Module-level singletons: one connection shared across the whole app.
let connection: signalR.HubConnection | null = null
let startPromise: Promise<void> | null = null
const isConnected = ref(false)
// Same lifecycle as isConnected, but distinguishes "never opened" from "currently down" — see
// useRealtimeStatus, which aggregates the hubs into one status indicator.
const hubState = ref<HubState>('idle')

/** Whether this connection is currently in the family's group, so a reconnect can re-join. */
let joined = false
const reconnectedCallbacks: Array<() => void> = []

export const useFamilyChatSocket = () => {
  const isSupported = import.meta.client
  const config = useRuntimeConfig()
  const apiBase = (config.public.apiBase as string) || 'http://localhost:5226'

  const getConnection = (): signalR.HubConnection | null => {
    if (!isSupported) return null

    if (!connection) {
      const url = `${apiBase.replace(/\/$/, '')}/hubs/family-chat`

      connection = new signalR.HubConnectionBuilder()
        .withUrl(url, { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build()

      connection.onreconnecting(() => {
        isConnected.value = false
        hubState.value = 'reconnecting'
      })
      connection.onclose(() => {
        isConnected.value = false
        hubState.value = 'closed'
        joined = false
      })
      connection.onreconnected(() => {
        isConnected.value = true
        hubState.value = 'connected'

        // Groups are connection-scoped and are lost when the connection is rebuilt, and so is
        // every per-connection flag the server holds for us (#148's typing state, #149's
        // "actively watching"). Consumers re-establish all of it from these callbacks; the
        // best-effort re-join below is the floor if nobody registered one.
        if (reconnectedCallbacks.length > 0) {
          reconnectedCallbacks.forEach((cb) => { try { cb() } catch { /* ignore */ } })
        } else if (joined) {
          connection?.invoke('JoinChat').catch(() => { /* ignore */ })
        }
      })
    }

    return connection
  }

  const ensureConnected = async (): Promise<signalR.HubConnection | null> => {
    const conn = getConnection()
    if (!conn) return null

    if (conn.state === signalR.HubConnectionState.Connected) return conn

    if (!startPromise) {
      startPromise = conn.start()
        .then(() => { isConnected.value = true; hubState.value = 'connected' })
        .catch((error) => { startPromise = null; throw error })
    }

    await startPromise
    return conn
  }

  /**
   * Joins the family's chat group and returns the newest page of it.
   *
   * One round trip for both, rather than a join followed by a REST fetch: the page cannot then be
   * a moment older than the group membership that keeps it current. Returns `null` when sockets
   * are unavailable, so the caller fetches over REST instead.
   */
  const joinChat = async (): Promise<FamilyChatPage | null> => {
    if (!isSupported) return null

    const conn = await ensureConnected()
    if (!conn) return null

    const page = await conn.invoke<FamilyChatPage>('JoinChat')
    joined = true
    return page
  }

  /** Leaves the family's chat group (the panel was closed). Safe to call anytime. */
  const leaveChat = async (): Promise<void> => {
    joined = false

    const conn = connection
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return

    try {
      await conn.invoke('LeaveChat')
    } catch {
      // Ignore — leaving is best-effort (the group is dropped on disconnect anyway).
    }
  }

  /**
   * Calls a hub method, swallowing a failure.
   *
   * For the fire-and-forget signals (#148's typing, #149's activity flag): every one of them is a
   * hint that expires on the server by itself, so a dropped call costs a few seconds of staleness
   * and nothing else. None of them is worth an error path in the caller.
   */
  const invokeQuietly = async (method: string, ...args: unknown[]): Promise<void> => {
    const conn = connection
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return

    try {
      await conn.invoke(method, ...args)
    } catch {
      // Ignore, deliberately — see above.
    }
  }

  /** Subscribe to a hub event (MessageCreated / MessageDeleted / …). */
  const on = (event: string, handler: HubEventHandler): void => {
    getConnection()?.on(event, handler as SignalRHandler)
  }

  /** Unsubscribe a previously registered handler. */
  const off = (event: string, handler: HubEventHandler): void => {
    connection?.off(event, handler as SignalRHandler)
  }

  /** Register a callback to run after an automatic reconnect (to re-join and re-report state). */
  const onReconnected = (callback: () => void): void => {
    if (!reconnectedCallbacks.includes(callback)) reconnectedCallbacks.push(callback)
  }

  const offReconnected = (callback: () => void): void => {
    const index = reconnectedCallbacks.indexOf(callback)
    if (index >= 0) reconnectedCallbacks.splice(index, 1)
  }

  return {
    isSupported,
    isConnected,
    hubState,
    ensureConnected,
    joinChat,
    leaveChat,
    invokeQuietly,
    on,
    off,
    onReconnected,
    offReconnected
  }
}
