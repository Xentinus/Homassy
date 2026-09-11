/**
 * One realtime status, aggregated across however many of the three SignalR hubs the current page
 * has open — the single thing `RealtimeConnectionBar` renders instead of each hub getting its own
 * indicator. See `deriveRealtimeStatus` (app/utils/realtimeStatus.ts) for the pure reduction rules;
 * this composable is just the reactive wiring around it.
 */
import { computed, onScopeDispose, ref, watch } from 'vue'
import { useInventorySocket } from './useInventorySocket'
import { useMasterDataSocket } from './useMasterDataSocket'
import { useShoppingListSocket } from './useShoppingListSocket'
import { deriveRealtimeStatus, type RealtimeStatus } from '~/utils/realtimeStatus'

/**
 * How long `justReconnected` stays true after a recovery, so a "Back online" confirmation is on
 * screen long enough to read before it fades. Keep in sync with `--realtime-reconnected-hold` in
 * main.css.
 */
const JUST_RECONNECTED_MS = 2500

/** Statuses a "back online" confirmation can follow — i.e. everything that isn't already fine. */
const RECOVERABLE_STATUSES = new Set<RealtimeStatus>(['reconnecting', 'offline', 'device-offline'])

export const useRealtimeStatus = () => {
  const inventorySocket = useInventorySocket()
  const masterDataSocket = useMasterDataSocket()
  const shoppingListSocket = useShoppingListSocket()
  // The chat hub (#144) only connects while the panel is open, so it sits at 'idle' most of the
  // time - which deriveRealtimeStatus filters out. That is exactly right: it should count towards
  // the status indicator while somebody is reading a conversation, and not before.
  const familyChatSocket = useFamilyChatSocket()

  // navigator.onLine only exists client-side; SSR defaults to true (same as "never checked yet"),
  // which is harmless since a server-rendered page never has a live hub either — the aggregate
  // status is 'idle' either way until the client takes over.
  const online = ref(import.meta.client ? navigator.onLine : true)

  if (import.meta.client) {
    const syncOnline = () => { online.value = navigator.onLine }
    window.addEventListener('online', syncOnline)
    window.addEventListener('offline', syncOnline)
    onScopeDispose(() => {
      window.removeEventListener('online', syncOnline)
      window.removeEventListener('offline', syncOnline)
    })
  }

  const status = computed(() => deriveRealtimeStatus(
    [
      inventorySocket.hubState.value,
      masterDataSocket.hubState.value,
      shoppingListSocket.hubState.value,
      familyChatSocket.hubState.value
    ],
    online.value
  ))

  // True for ~JUST_RECONNECTED_MS after a recovery edge (reconnecting/offline/device-offline →
  // connected), so RealtimeConnectionBar can show a "Back online" confirmation before it fades.
  const justReconnected = ref(false)
  let clearTimer: ReturnType<typeof setTimeout> | null = null

  const clearPendingTimer = () => {
    if (clearTimer) {
      clearTimeout(clearTimer)
      clearTimer = null
    }
  }

  watch(status, (next, previous) => {
    if (previous && RECOVERABLE_STATUSES.has(previous) && next === 'connected') {
      justReconnected.value = true
      clearPendingTimer()
      clearTimer = setTimeout(() => {
        justReconnected.value = false
        clearTimer = null
      }, JUST_RECONNECTED_MS)
    } else if (next !== 'connected') {
      // Dropped again before the hold finished — nothing left to confirm.
      justReconnected.value = false
      clearPendingTimer()
    }
  })

  onScopeDispose(clearPendingTimer)

  return { status, justReconnected }
}
