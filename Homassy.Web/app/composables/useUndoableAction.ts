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
  /**
   * Undoes exactly what `apply` did, but only for `ownedEntityIds` — the subset of this action's
   * own `entityIds` not currently claimed by some newer pending action (see undoQueue.ts's
   * `owned()` and its file header's fourth NON-OBVIOUS RULE). For an ordinary single-entity action
   * this is either `[entityId]` or `[]`; a batch must check membership per entity rather than
   * reverting the whole set. Never called once `commit` has been sent.
   */
  revert: (ownedEntityIds: string[]) => void
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
const reverts = new Map<string, (ownedEntityIds: string[]) => void>()

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
    //
    // queue.owned(action.entityIds) is evaluated lazily, inside this thunk, so it reflects
    // ownership at the moment the commit actually resolves — not at drain() time above. A normal
    // expiry's entities can never overlap another *currently* pending action (an overlap would
    // have settled this one early instead — see undoQueue.ts), but a brand new action can still
    // claim one of them while this commit is in flight; scoping the revert this way covers that
    // race the same way it covers a settled-early one (settleEarly below).
    const succeeded = await settleCommit(commit, () => revert?.(queue.owned(action.entityIds)))
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

/**
 * Settles one displaced action's commit right now instead of waiting out its remaining undo
 * window — what `run()` below does with every action `queue.add()` reports in `AddResult.toSettle`
 * (see undoQueue.ts's file header: a partial-overlap displacement must not simply drop the older
 * action's write). Same failure handling as a normally-expired commit (`settleCommit` + the one
 * shared toast, `onExpire` below) — only triggered early, and for one action instead of a batch.
 */
async function settleEarly(commit: () => Promise<CommitResult>, revert: () => void): Promise<void> {
  const succeeded = await settleCommit(commit, revert)
  if (!succeeded) reportFailure()
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

  const id = nextActionId()
  const expiresAt = Date.now() + UNDO_WINDOW_MS

  // The queue decides what an overlap means — an exact entity-set match is simply replaced (its
  // commit never runs), a partial one must be settled instead (see undoQueue.ts's file header and
  // `AddResult`). Either way every action it reports removing had its own commit()/revert()
  // bookkeeping in the two maps below, keyed by id — the queue only ever deals in `PendingAction`,
  // never those closures, so releasing (and, for `toSettle`, firing) them is this composable's job.
  const { replaced, toSettle } = queue.add({ id, entityIds, kind, label, expiresAt })
  const toSettleIds = new Set(toSettle.map(a => a.id))

  for (const previous of replaced) {
    const previousCommit = commits.get(previous.id)
    const previousRevert = reverts.get(previous.id)
    commits.delete(previous.id)
    reverts.delete(previous.id)

    // Not reported for settling: an exact-set match, dropped outright — its revert must never
    // fire once a newer apply has run on top of it (same as before this fix).
    if (!toSettleIds.has(previous.id) || !previousCommit) continue

    // Fire it now rather than waiting out its remaining window: the entities this new action
    // doesn't touch still need this write to reach the server, or it is lost for good. If that
    // commit then fails, its revert must be scoped to queue.owned(previous.entityIds) — the
    // entities *this* new action just claimed (id, above — B in the batch-move-then-solo-move
    // example) are no longer `previous`'s to restore; see undoQueue.ts's fourth NON-OBVIOUS RULE.
    void settleEarly(previousCommit, () => previousRevert?.(queue.owned(previous.entityIds)))
  }

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
    // Always the full entityIds in practice — a still-pending action's ids can't overlap any
    // other pending action's (see undoQueue.ts) — but routed through queue.owned() anyway so
    // every revert call in this file honours the same contract.
    revert?.(queue.owned(action.entityIds))
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
