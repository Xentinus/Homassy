/**
 * Inventory (Készletek) realtime socket (SignalR).
 *
 * Manages a single app-wide connection to the API's `/hubs/inventory` hub. Unlike shopping lists
 * (one group per opened list), inventory groups are derived from identity: on connect the server
 * subscribes the connection to the user's own group and (if any) their family group. So the client
 * just calls `joinInventory()` once to get the current grid snapshot and then receives live
 * `InventoryUpserted` / `InventoryDeleted` / `ProductUpdated` / `ProductFavoriteChanged` /
 * `ProductDeleted` events for everything it can see.
 *
 * Auth: the Kratos session cookie is sent on the WS handshake via `withCredentials`, so the existing
 * server-side session middleware authenticates the connection — no token plumbing.
 *
 * Everything is guarded by `isSupported` (client-only); on the server the methods are no-ops and
 * `joinInventory` returns `null` so callers can fall back to a plain REST fetch.
 */
import * as signalR from '@microsoft/signalr'
import { ref } from 'vue'
import type { InventoryGridProductInfo } from '~/types/product'

// Module-level singletons: one connection shared across the whole app.
let connection: signalR.HubConnection | null = null
let startPromise: Promise<void> | null = null
const isConnected = ref(false)

// Callbacks to run after an automatic reconnect (groups are re-joined server-side in
// OnConnectedAsync, but events during the gap are missed, so consumers re-fetch the snapshot).
const reconnectedCallbacks: Array<() => void> = []

export const useInventorySocket = () => {
  const isSupported = import.meta.client
  const config = useRuntimeConfig()
  const apiBase = (config.public.apiBase as string) || 'http://localhost:5226'

  const getConnection = (): signalR.HubConnection | null => {
    if (!isSupported) return null

    if (!connection) {
      const url = `${apiBase.replace(/\/$/, '')}/hubs/inventory`

      connection = new signalR.HubConnectionBuilder()
        .withUrl(url, { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build()

      connection.onreconnecting(() => { isConnected.value = false })
      connection.onclose(() => { isConnected.value = false })
      connection.onreconnected(() => {
        isConnected.value = true
        // Re-sync: let consumers reload the snapshot to catch events missed while disconnected.
        reconnectedCallbacks.forEach((cb) => { try { cb() } catch { /* ignore */ } })
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
   * Connects and returns the current inventory grid snapshot (only the fields the cards need).
   * Returns `null` when sockets aren't available (SSR) so the caller can fetch via REST.
   */
  const joinInventory = async (): Promise<InventoryGridProductInfo[] | null> => {
    if (!isSupported) return null

    const conn = await ensureConnected()
    if (!conn) return null

    return await conn.invoke<InventoryGridProductInfo[]>('JoinInventory')
  }

  /** Subscribe to a hub event. */
  const on = (event: string, handler: (...args: any[]) => void): void => {
    getConnection()?.on(event, handler)
  }

  /** Unsubscribe a previously registered handler. */
  const off = (event: string, handler: (...args: any[]) => void): void => {
    connection?.off(event, handler)
  }

  /** Register a callback to run after an automatic reconnect (to re-sync the grid snapshot). */
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
    joinInventory,
    on,
    off,
    onReconnected,
    offReconnected
  }
}
