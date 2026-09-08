/**
 * Pure reduction of however many SignalR hubs a page has open down to the one realtime status a
 * user-facing indicator shows. See `useRealtimeStatus.ts` for the composable that feeds this from
 * the three socket composables (`useInventorySocket`, `useMasterDataSocket`,
 * `useShoppingListSocket`) plus `navigator.onLine`, and `RealtimeConnectionBar.vue` for the one
 * component that renders the result — never two, so a page with more than one hub open can never
 * show two contradicting indicators.
 */

/**
 * One hub's own connection state, as tracked by its socket composable's module-level `hubState`.
 *
 * `idle` means "this page never opened this hub" (its module-level ref is still at its initial
 * value) — not "briefly disconnected". Most pages only ever open one of the three hubs, so this
 * must not drag the aggregate down: two `idle` hubs and zero opened ones both read as `idle`
 * overall, never as an offline page.
 */
export type HubState = 'idle' | 'connected' | 'reconnecting' | 'closed'

/**
 * The one status a user-facing indicator (`RealtimeConnectionBar`) ever shows:
 *  - `connected`      every hub this page opened is up.
 *  - `reconnecting`   at least one hub is mid-retry (SignalR's own automatic reconnect).
 *  - `offline`        at least one hub is fully closed, and the device itself has network — most
 *                      likely the server/hub side, so "we'll keep trying" is an honest thing to say.
 *  - `device-offline` the device itself reports no network (`navigator.onLine === false`) — a
 *                      different, more honest message than `offline`/`reconnecting`, since nothing
 *                      SignalR does will fix a phone that left the tunnel.
 *  - `idle`           this page never opened any hub (or was given an empty hub list).
 */
export type RealtimeStatus = 'connected' | 'reconnecting' | 'offline' | 'device-offline' | 'idle'

/**
 * Reduces every hub this page has opened down to a single worst-case status.
 *
 * `idle` hubs are filtered out first — a page that never opened a given hub must not show
 * "Offline" because of it — and if nothing is left after that, the result is `idle` too. Of what
 * remains, `closed` outranks `reconnecting` outranks `connected`: the aggregate always reports the
 * worst live hub, so two open hubs can never show two contradicting indicators.
 *
 * `online` (`navigator.onLine`) only ever *demotes the wording*, from "offline"/"reconnecting" to
 * "device-offline" — it can never promote a bad hub state to "connected". A connected socket is
 * proof of connectivity and always wins over a `navigator.onLine` of `false`: that flag is well
 * known to be false-positive prone (captive portals, some VPNs, some mobile stacks), while a hub
 * that is actually open cannot be lying about it.
 */
export const deriveRealtimeStatus = (hubs: HubState[], online: boolean): RealtimeStatus => {
  const live = hubs.filter((hub): hub is Exclude<HubState, 'idle'> => hub !== 'idle')
  if (live.length === 0) return 'idle'

  if (live.every(hub => hub === 'connected')) return 'connected'

  if (live.some(hub => hub === 'closed')) return online ? 'offline' : 'device-offline'

  // Nothing closed, but not all connected either — the rest must be 'reconnecting'.
  return online ? 'reconnecting' : 'device-offline'
}
