/**
 * The app-wide optimistic-action queue, reactive and timed. See `app/utils/undoQueue.ts` for the
 * pure queue rules (same-entity replacement, the shared deadline, collapsing); this file is just
 * the timer, the `requestAnimationFrame` clock, and the error-toast wiring around it — the same
 * split `useRealtimeStatus.ts` uses around `realtimeStatus.ts`.
 *
 * Module-level singletons (not per-call state), like `useShoppingListSocket`'s connection: every
 * caller — every swipe-to-delete, every purchase toggle, every socket handler's pending check, and
 * the one `UndoToast` mounted in `app.vue` — shares the same queue, the same timer and the same
 * clock, so a pending action started on one page is still there (and still committing on time) if
 * the user navigates before the undo window closes.
 *
 * The queue's replacement rule (undoQueue.ts) assumes every `commit` is an absolute write, safe to
 * fully supersede because applying only the newer one still leaves the row correct. A relative
 * delta (e.g. inventory "consume", which decrements rather than sets) does not fit that: replacing
 * it would silently drop the older delta's server round-trip while the UI shows both applied. Do
 * not route a delta through `run()` — see `InventoryItemRow.vue`'s consume handler for the fuller
 * reasoning and the pre-existing server-side race a queued delta would also widen.
 */
import { computed, ref } from 'vue'
import {
  createUndoQueue,
  nextActionId,
  settleCommit,
  UNDO_WINDOW_MS,
  type CommitResult,
  type PendingAction,
  type UndoKind
} from '~/utils/undoQueue'

interface RunOptions<T extends CommitResult> {
  /** The row(s)/item(s) this action is about — one entry for an ordinary single-row action,
   *  several for a batch (see undoQueue.ts). What `isPendingEntity` and overlap-based replacement
   *  key off. */
  entityIds: string[]
  kind: UndoKind
  /** This action's own label, shown verbatim while it is the only one pending. */
  label: string
  /** Mutates local state immediately — the row disappears / the toggle flips now. */
  apply: () => void
  /** Undoes exactly what `apply` did. Never called once `commit` has been sent. */
  revert: () => void
  /**
   * The real network request. Only ever invoked after the undo window has fully elapsed. Must
   * resolve to `{ success: boolean, ... }` rather than throw on a business-rule failure — every
   * API composable call already does (see `useApiClient.ts`'s `request()`) — because
   * `settleCommit` (undoQueue.ts) reverts on either a rejection or a resolved `success: false`.
   */
  commit: () => Promise<T>
}

const queue = createUndoQueue()

// A plain reactive snapshot of the queue's contents, refreshed on every mutation below — the
// queue itself is intentionally not reactive (it is a pure module with its own unit tests).
const pending = ref<PendingAction[]>([])
const sync = (): void => { pending.value = queue.list() }

// commit()/revert() closures, keyed by PendingAction.id rather than by an entity id: an
// overlap-based replacement drops the old action's entry entirely (see undoQueue.ts), so there is
// never a stale closure left behind for an id no longer in the queue.
const commits = new Map<string, () => Promise<CommitResult>>()
const reverts = new Map<string, () => void>()

let timer: ReturnType<typeof setTimeout> | null = null

const clearTimer = (): void => {
  if (timer !== null) {
    clearTimeout(timer)
    timer = null
  }
}

/** (Re)arms the single queue-wide timer for `expiresAt`. Never more than one timer at a time. */
const armTimer = (expiresAt: number): void => {
  clearTimer()
  timer = setTimeout(onExpire, Math.max(0, expiresAt - Date.now()))
}

async function onExpire(): Promise<void> {
  timer = null
  const due = queue.drain()
  sync()

  for (const action of due) {
    const commit = commits.get(action.id)
    const revert = reverts.get(action.id)
    commits.delete(action.id)
    reverts.delete(action.id)
    if (!commit) continue

    // A rejection and a resolved `success: false` are both a failure (see undoQueue.ts's
    // `settleCommit` — the shape every real commit() resolves to) and both revert; only the
    // toast (this composable's one Nuxt-dependent bit) lives out here.
    const succeeded = await settleCommit(commit, () => revert?.())
    if (!succeeded) reportFailure()
  }

  // Something may have been queued while the above commits were in flight — arm the next wave.
  const remaining = queue.list()
  if (remaining.length > 0) armTimer(remaining[0]!.expiresAt)
}

/** Surfaces a failed (and already-reverted) commit through the app's existing toast, matching the
 *  shape `InventoryItemRow.vue` uses for its own error toasts. */
function reportFailure(): void {
  const toast = useToast()
  const { t } = useI18n()
  toast.add({
    title: t('toast.error'),
    description: t('undo.failed'),
    color: 'error'
  })
}

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
const isPendingEntity = (entityId: string): boolean => queue.has(entityId)

const run = <T extends CommitResult,>(options: RunOptions<T>): void => {
  const { entityIds, kind, label, apply, revert, commit } = options

  apply()

  // An overlap-based replacement (see undoQueue.ts) drops every previous action sharing any
  // entity id with this one, bookkeeping included — more than one can overlap a new batch (e.g.
  // two single-item actions each folded into one new multi-item move), so clean up all of them,
  // not just the first match. Each dropped action's revert must never fire once a newer apply has
  // run on top of it.
  const overlapping = queue.list().filter(a => a.entityIds.some(id => entityIds.includes(id)))
  for (const previous of overlapping) {
    commits.delete(previous.id)
    reverts.delete(previous.id)
  }

  const id = nextActionId()
  const expiresAt = Date.now() + UNDO_WINDOW_MS

  queue.add({ id, entityIds, kind, label, expiresAt })
  commits.set(id, commit)
  reverts.set(id, revert)
  sync()

  // The queue just gave every pending action this same expiresAt (see undoQueue.ts), so arming
  // the one timer against it keeps the JS timer and `remainingRatio` in lockstep.
  armTimer(expiresAt)
  startClock()
}

/** Undo for the whole toast: reverts every currently pending action and cancels the timer before
 *  any of their commits are ever sent — pressing Undo means no request happens, not that one gets
 *  reverted afterwards. */
const undoAll = (): void => {
  const all = queue.list()
  clearTimer()

  for (const action of all) {
    queue.cancel(action.id)
    const revert = reverts.get(action.id)
    commits.delete(action.id)
    reverts.delete(action.id)
    revert?.()
  }

  sync()
}

export const useUndoableAction = () => {
  return {
    run,
    pending,
    remainingRatio,
    undoAll,
    isPendingEntity
  }
}
