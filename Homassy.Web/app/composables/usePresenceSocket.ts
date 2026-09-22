/**
 * Household presence socket (SignalR).
 *
 * One app-wide connection to the API's `/hubs/presence` hub. Groups are derived from identity and
 * joined server-side on connect (like the inventory hub, unlike shopping lists), so a client only
 * has to connect and ask once: `join()` starts the connection and returns the current roster, and
 * `PresenceChanged` keeps it live from then on.
 *
 * The roster is module-scoped and **includes the viewer**, because the home screen's strip shows
 * the whole household — that is the opposite of `useShoppingListSocket`'s `presentMembers`, which
 * answers "who else is here" and therefore filters self out.
 *
 * `reportShopping()` is the one thing this client tells the server: that it entered or left
 * in-store shopping mode. It sends the list's public id, never a name — the server resolves the
 * name through the access-checked path, so what reaches the household is not client-authored text.
 * It is fire-and-forget: shopping mode must never fail to open because a socket was down.
 *
 * Everything is guarded by `isSupported` (client-only); on the server the methods are no-ops and
 * `join()` returns `null`.
 */
import * as signalR from '@microsoft/signalr'
import { ref } from 'vue'
import type { FamilyPresenceMember } from '~/types/presence'
import type { HubEventHandler, SignalRHandler } from '~/types/realtime'
import type { HubState } from '~/utils/realtimeStatus'

// Module-level singletons: one connection, one roster, shared across the whole app.
let connection: signalR.HubConnection | null = null
let startPromise: Promise<void> | null = null
const isConnected = ref(false)
const hubState = ref<HubState>('idle')
const members = ref<FamilyPresenceMember[]>([])

/**
 * What this connection last reported as its shopping context, replayed after a reconnect: the
 * server's registry is keyed by connection id, and a reconnect is a *new* connection that knows
 * nothing about the shop its user is standing in.
 */
let reportedShoppingList: string | null = null

export const usePresenceSocket = () => {
  const isSupported = import.meta.client
  const config = useRuntimeConfig()
  const apiBase = (config.public.apiBase as string) || 'http://localhost:5226'

  const getConnection = (): signalR.HubConnection | null => {
    if (!isSupported) return null

    if (!connection) {
      const url = `${apiBase.replace(/\/$/, '')}/hubs/presence`

      connection = new signalR.HubConnectionBuilder()
        .withUrl(url, { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build()

      connection.on('PresenceChanged', (roster: FamilyPresenceMember[]) => {
        members.value = Array.isArray(roster) ? roster : []
      })

      connection.onreconnecting(() => {
        isConnected.value = false
        hubState.value = 'reconnecting'
        // Nobody is reachable through a socket that is down, so claiming the household is still
        // around would be a lie that outlives the outage. An empty strip is the honest render.
        members.value = []
      })

      connection.onclose(() => {
        isConnected.value = false
        hubState.value = 'closed'
        members.value = []
      })

      connection.onreconnected(() => {
        isConnected.value = true
        hubState.value = 'connected'
        void resync()
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

  /** Re-announce this connection's shopping context and re-read the roster after a reconnect. */
  const resync = async (): Promise<void> => {
    const conn = connection
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return

    try {
      if (reportedShoppingList) {
        await conn.invoke('SetShopping', reportedShoppingList)
      }

      members.value = await conn.invoke<FamilyPresenceMember[]>('GetPresence') ?? []
    }
    catch (error) {
      console.debug('[Presence] Re-sync after reconnect failed', error)
    }
  }

  /**
   * Connects and returns the household's current roster. `null` when sockets aren't available
   * (SSR) — presence simply does not render, there is no REST equivalent to fall back to.
   */
  const join = async (): Promise<FamilyPresenceMember[] | null> => {
    if (!isSupported) return null

    const conn = await ensureConnected()
    if (!conn) return null

    const roster = await conn.invoke<FamilyPresenceMember[]>('GetPresence') ?? []
    members.value = roster
    return roster
  }

  /**
   * Tell the household this device entered (a list's public id) or left (null) shopping mode.
   * Never throws and never blocks the caller: a presence report is an aside to the mode itself.
   */
  const reportShopping = (shoppingListPublicId: string | null): void => {
    if (!isSupported) return

    reportedShoppingList = shoppingListPublicId

    void (async () => {
      try {
        const conn = await ensureConnected()
        await conn?.invoke('SetShopping', shoppingListPublicId)
      }
      catch (error) {
        console.debug('[Presence] Reporting shopping mode failed', error)
      }
    })()
  }

  const on = (event: string, handler: HubEventHandler): void => {
    getConnection()?.on(event, handler as SignalRHandler)
  }

  const off = (event: string, handler: HubEventHandler): void => {
    connection?.off(event, handler as SignalRHandler)
  }

  return {
    isSupported,
    isConnected,
    hubState,
    members,
    ensureConnected,
    join,
    reportShopping,
    on,
    off
  }
}
