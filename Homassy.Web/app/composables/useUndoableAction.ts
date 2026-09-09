/**
 * The app-wide optimistic-action queue, reactive and timed. See `app/utils/undoQueue.ts` for the
 * pure queue rules (same-entity replacement, the shared deadline, collapsing, the revert-ownership
 * rule) and `app/utils/undoOrchestrator.ts` for the pure commit/revert orchestration built on top
 * of it (run()/expiry/undoAll, the `commits`/`reverts` bookkeeping) — this file is what is left
 * once both of those are pure and framework-free: real Vue refs, a real `setTimeout`, the
 * `requestAnimationFrame` clock, and the error-toast wiring, the same split `useRealtimeStatus.ts`
 * uses around `realtimeStatus.ts`.
 *
 * Module-level singletons (not per-call state), like `useShoppingListSocket`'s connection: every
 * caller — every swipe-to-delete, every purchase toggle, every socket handler's pending check, and
 * the one `UndoToast` mounted in `app.vue` — shares the same orchestrator, the same timer and the
 * same clock, so a pending action started on one page is still there (and still committing on
 * time) if the user navigates before the undo window closes.
 *
 * The queue's replacement rule (undoQueue.ts) assumes every `commit` is an absolute write, safe to
 * fully supersede because applying only the newer one still leaves the row correct. A relative
 * delta (e.g. inventory "consume", which decrements rather than sets) does not fit that: replacing
 * it would silently drop the older delta's server round-trip while the UI shows both applied. Do
 * not route a delta through `run()` — see `InventoryItemRow.vue`'s consume handler for the fuller
 * reasoning and the pre-existing server-side race a queued delta would also widen.
 */
import { computed, ref } from 'vue'
import { createUndoOrchestrator, type UndoRunOptions } from '~/utils/undoOrchestrator'
import { UNDO_WINDOW_MS, type CommitResult, type PendingAction } from '~/utils/undoQueue'

// A plain reactive snapshot of the orchestrator's pending list, refreshed via onChange below — the
// orchestrator itself is intentionally not reactive (it is a pure module with its own unit tests).
const pending = ref<PendingAction[]>([])

let timer: ReturnType<typeof setTimeout> | null = null

const clearTimer = (): void => {
  if (timer !== null) {
    clearTimeout(timer)
    timer = null
  }
}

/** Surfaces a failed (and already-reverted) commit through the app's existing toast, matching the
 *  shape `InventoryItemRow.vue` uses for its own error toasts. The one Nuxt-dependent bit in this
 *  whole file — everything else the orchestrator needs is a plain callback. */
function reportFailure(): void {
  const toast = useToast()
  const { t } = useI18n()
  toast.add({
    title: t('toast.error'),
    description: t('undo.failed'),
    color: 'error'
  })
}

const orchestrator = createUndoOrchestrator({
  onChange: (snapshot) => { pending.value = snapshot },
  // (Re)arms the single queue-wide timer for `expiresAt`. Never more than one timer at a time —
  // clearTimer() first is what keeps a later run() from ever stacking a second one.
  scheduleExpiry: (expiresAt, fire) => {
    clearTimer()
    timer = setTimeout(() => {
      timer = null
      fire()
    }, Math.max(0, expiresAt - Date.now()))
  },
  cancelScheduledExpiry: clearTimer,
  onSettleFailure: reportFailure
})

// --- requestAnimationFrame clock -------------------------------------------------------------
// One loop for the whole app, running only while something is pending, driving `now` for
// `remainingRatio`. Not a `setInterval`: this only needs to move while the ring is actually on
// screen and tabs paint, and it costs nothing while the queue is empty.
const now = ref(Date.now())
let rafHandle: number | null = null

function tick(): void {
  now.value = Date.now()
  if (pending.value.length === 0) {
    rafHandle = null
    return
  }
  rafHandle = requestAnimationFrame(tick)
}

function startClock(): void {
  if (rafHandle === null && import.meta.client) {
    rafHandle = requestAnimationFrame(tick)
  }
}

const remainingRatio = computed(() => {
  if (pending.value.length === 0) return 0
  const earliest = Math.min(...pending.value.map(a => a.expiresAt))
  return Math.max(0, Math.min(1, (earliest - now.value) / UNDO_WINDOW_MS))
})

/**
 * Every socket handler's realtime contract: an incoming event for a pending entity is dropped
 * rather than applied, so a stale echo of the pre-change server state cannot fight (or resurrect)
 * a change the user just made locally. Once the pending window ends the entity is no longer
 * "pending" — win or lose, the commit's own resolution (and whatever the server broadcasts back
 * because of it) is what reconciles from there, last-write-wins by entity id with the server as
 * the tiebreak.
 */
const isPendingEntity = (entityId: string): boolean => orchestrator.isPendingEntity(entityId)

const run = <T extends CommitResult,>(options: UndoRunOptions<T>): void => {
  orchestrator.run(options)
  // Only this composable knows about the clock; the orchestrator itself has no concept of
  // rendering anything and so no reason to start it.
  startClock()
}

/** Undo for the whole toast: reverts every currently pending action and cancels the timer before
 *  any of their commits are ever sent — pressing Undo means no request happens, not that one gets
 *  reverted afterwards. */
const undoAll = (): void => orchestrator.undoAll()

export const useUndoableAction = () => {
  return {
    run,
    pending,
    remainingRatio,
    undoAll,
    isPendingEntity
  }
}
