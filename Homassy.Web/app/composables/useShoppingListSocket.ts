/**
 * Shopping list realtime socket (SignalR).
 *
 * Manages a single app-wide connection to the API's `/hubs/shopping-list` hub. A client
 * "joins" the list it is viewing and receives live `ItemUpserted` / `ItemDeleted` /
 * `ListUpdated` / `ListDeleted` events for it, so the open list stays current without
 * polling or manual refresh. Writes still go through the REST endpoints; the server
 * broadcasts the resulting change to everyone in the list's group.
 *
 * Auth: the Kratos session cookie is sent on the WS handshake via `withCredentials`, so the
 * existing server-side session middleware authenticates the connection — no token plumbing.
 *
 * Everything is guarded by `isSupported` (client-only); on the server the methods are no-ops
 * and `joinList` returns `null` so callers can fall back to a plain REST fetch.
 */
import * as signalR from '@microsoft/signalr'
import { ref } from 'vue'
import type { DetailedShoppingListInfo } from '~/types/shoppingList'

// Module-level singletons: one connection shared across the whole app.
let connection: signalR.HubConnection | null = null
let startPromise: Promise<void> | null = null
const isConnected = ref(false)

// Track what we're currently joined to, so we can re-join after an automatic reconnect
// (SignalR groups are connection-scoped and are lost when the connection is rebuilt).
let currentListId: string | null = null
let currentShowPurchased = false
const reconnectedCallbacks: Array<() => void> = []

export const useShoppingListSocket = () => {
  const isSupported = import.meta.client
  const config = useRuntimeConfig()
  const apiBase = (config.public.apiBase as string) || 'http://localhost:5226'

  const getConnection = (): signalR.HubConnection | null => {
    if (!isSupported) return null

    if (!connection) {
      const url = `${apiBase.replace(/\/$/, '')}/hubs/shopping-list`

      connection = new signalR.HubConnectionBuilder()
        .withUrl(url, { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build()

      connection.onreconnecting(() => { isConnected.value = false })
      connection.onclose(() => { isConnected.value = false })
      connection.onreconnected(() => {
        isConnected.value = true
        // Re-sync: let consumers reload the snapshot (which re-joins the group). If no
        // consumer registered, best-effort re-join so we keep receiving events.
        if (reconnectedCallbacks.length > 0) {
          reconnectedCallbacks.forEach((cb) => { try { cb() } catch { /* ignore */ } })
        } else if (currentListId) {
          connection?.invoke('JoinList', currentListId, currentShowPurchased).catch(() => { /* ignore */ })
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
        .then(() => { isConnected.value = true })
        .catch((error) => { startPromise = null; throw error })
    }

    await startPromise
    return conn
  }

  /**
   * Joins a shopping list's channel and returns its current items (the snapshot).
   * Returns `null` when sockets aren't available (SSR) so the caller can fetch via REST.
   */
  const joinList = async (publicId: string, showPurchased = false): Promise<DetailedShoppingListInfo | null> => {
    if (!isSupported) return null

    const conn = await ensureConnected()
    if (!conn) return null

    currentListId = publicId
    currentShowPurchased = showPurchased

    return await conn.invoke<DetailedShoppingListInfo>('JoinList', publicId, showPurchased)
  }

  /** Leaves a shopping list's channel (e.g. when switching lists). Safe to call anytime. */
  const leaveList = async (publicId: string): Promise<void> => {
    if (currentListId === publicId) currentListId = null

    const conn = connection
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return

    try {
      await conn.invoke('LeaveList', publicId)
    } catch {
      // Ignore — leaving is best-effort (the group is dropped on disconnect anyway).
    }
  }

  /** Subscribe to a hub event (ItemUpserted / ItemDeleted / ListUpdated / ListDeleted). */
  const on = (event: string, handler: (...args: any[]) => void): void => {
    getConnection()?.on(event, handler)
  }

  /** Unsubscribe a previously registered handler. */
  const off = (event: string, handler: (...args: any[]) => void): void => {
    connection?.off(event, handler)
  }

  /** Register a callback to run after an automatic reconnect (to re-sync the open list). */
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
    ensureConnected,
    joinList,
    leaveList,
    on,
    off,
    onReconnected,
    offReconnected
  }
}
