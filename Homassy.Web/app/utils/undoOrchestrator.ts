/**
 * The commit/revert orchestration behind `useUndoableAction.ts`'s `run()` / expiry / `undoAll` —
 * extracted into its own pure, framework-free module (the same split `undoQueue.ts` already
 * describes for itself) so it can be unit tested without booting Nuxt: this project deliberately
 * carries no `@vue/test-utils` or `happy-dom`, and `useUndoableAction.ts` itself calls Nuxt
 * auto-imports (`useToast`, `useI18n`) that only resolve inside a running Nuxt app, which is what
 * made it untestable before this file existed.
 *
 * This module owns the `commits`/`reverts` bookkeeping (keyed by `PendingAction.id` — the queue
 * itself never holds those closures, see `undoQueue.ts`) and every rule about *when* something
 * settles, expires, or reverts. It performs no side effect it cannot do itself in plain
 * synchronous/async JS: arming or cancelling a real timer, and reporting a failure (a toast, in
 * practice) are both callbacks the composable supplies. `useUndoableAction.ts` is a thin wrapper
 * providing real Vue refs for `pending`, real `setTimeout`/`clearTimeout`, and the real toast —
 * everything it does beyond that (the `requestAnimationFrame` clock behind `remainingRatio`) has
 * nothing to do with commit/revert orchestration and stays there.
 */
import {
  createUndoQueue,
  nextActionId,
  settleCommit,
  UNDO_WINDOW_MS,
  type CommitResult,
  type PendingAction,
  type UndoKind
} from './undoQueue'

export interface UndoRunOptions<T extends CommitResult> {
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
   * The real network request. Only ever invoked after the undo window has fully elapsed (or the
   * action is settled early — see undoQueue.ts). Must resolve to `{ success: boolean, ... }`
   * rather than throw on a business-rule failure, because `settleCommit` reverts on either a
   * rejection or a resolved `success: false`.
   */
  commit: () => Promise<T>
}

/**
 * Every side effect this module cannot perform on its own. The composable supplies real
 * implementations (Vue refs, `setTimeout`, a toast); a test supplies plain `vi.fn()`s.
 */
export interface UndoOrchestratorCallbacks {
  /** Called after every mutation to the pending list, with the fresh snapshot — what the
   *  composable assigns into its own reactive `pending` ref. */
  onChange: (pending: PendingAction[]) => void
  /**
   * (Re)arms a single timer for `expiresAt`, invoking `fire` once it elapses. Never call `fire`
   * synchronously from within this callback — it must behave like a real timer, not an immediate
   * call, or `run()`'s own synchronous bookkeeping below `scheduleExpiry` would not have happened
   * yet when `fire` runs.
   */
  scheduleExpiry: (expiresAt: number, fire: () => void) => void
  /** Cancels whatever `scheduleExpiry` last armed, if anything is still pending. */
  cancelScheduledExpiry: () => void
  /** A settled commit resolved unsuccessfully (rejection or `{ success: false }`) and has already
   *  been reverted — report it however the caller sees fit (a toast, in practice). */
  onSettleFailure: () => void
}

export interface UndoOrchestrator {
  run: <T extends CommitResult>(options: UndoRunOptions<T>) => void
  /** Reverts every currently pending action and cancels the timer before any of their commits are
   *  ever sent — pressing Undo means no request happens, not that one gets reverted afterwards. */
  undoAll: () => void
  /** Every socket handler's realtime contract: true for a single real entity id currently claimed
   *  by any pending action (see undoQueue.ts's `has`). */
  isPendingEntity: (entityId: string) => boolean
  /** The queue's current snapshot, for the composable's initial reactive value and tests alike. */
  list: () => PendingAction[]
}

/**
 * Builds one orchestrator instance around a fresh, private `createUndoQueue()`. `useUndoableAction.ts`
 * calls this exactly once at module scope, same as it constructed the queue directly before this
 * extraction — the app-wide singleton behaviour (every caller shares one queue, one timer) is a
 * property of *where* this is called from, not of this factory itself, which is why tests can call
 * it as many times as they like for fully isolated instances.
 */
export const createUndoOrchestrator = (callbacks: UndoOrchestratorCallbacks): UndoOrchestrator => {
  const queue = createUndoQueue()

  // commit()/revert() closures, keyed by PendingAction.id rather than by an entity id: an
  // overlap-based replacement drops the old action's entry entirely (see undoQueue.ts), so there
  // is never a stale closure left behind for an id no longer in the queue.
  const commits = new Map<string, () => Promise<CommitResult>>()
  const reverts = new Map<string, (ownedEntityIds: string[]) => void>()

  const notifyChange = (): void => callbacks.onChange(queue.list())

  /**
   * Settles one displaced action's commit right now instead of waiting out its remaining undo
   * window — what `run()` below does with every action `queue.add()` reports in
   * `AddResult.toSettle` (see undoQueue.ts's file header: a partial-overlap displacement must not
   * simply drop the older action's write). Same failure handling as a normally-expired commit.
   */
  const settleEarly = async (commit: () => Promise<CommitResult>, revert: () => void): Promise<void> => {
    const succeeded = await settleCommit(commit, revert)
    if (!succeeded) callbacks.onSettleFailure()
  }

  const onExpire = async (): Promise<void> => {
    const due = queue.drain()
    notifyChange()

    for (const dueAction of due) {
      const commit = commits.get(dueAction.id)
      const revert = reverts.get(dueAction.id)
      commits.delete(dueAction.id)
      reverts.delete(dueAction.id)
      if (!commit) continue

      // queue.owned(...) is evaluated lazily, inside this thunk, so it reflects ownership at the
      // moment the commit actually resolves rather than at drain() time above — see
      // undoQueue.ts's fourth NON-OBVIOUS RULE for why a normal expiry needs this too, not just a
      // settled-early one.
      const succeeded = await settleCommit(commit, () => revert?.(queue.owned(dueAction.entityIds)))
      if (!succeeded) callbacks.onSettleFailure()
    }

    // Something may have been queued while the above commits were in flight — arm the next wave.
    const remaining = queue.list()
    if (remaining.length > 0) callbacks.scheduleExpiry(remaining[0]!.expiresAt, () => void onExpire())
  }

  const run = <T extends CommitResult,>(options: UndoRunOptions<T>): void => {
    const { entityIds, kind, label, apply, revert, commit } = options

    apply()

    const id = nextActionId()
    const expiresAt = Date.now() + UNDO_WINDOW_MS

    // The queue decides what an overlap means — an exact entity-set match is simply replaced (its
    // commit never runs), a partial one must be settled instead (see undoQueue.ts's file header
    // and `AddResult`). Either way every action it reports removing had its own commit()/revert()
    // bookkeeping in the two maps above, keyed by id — the queue only ever deals in
    // `PendingAction`, never those closures, so releasing (and, for `toSettle`, firing) them is
    // this module's job.
    const { replaced, toSettle } = queue.add({ id, entityIds, kind, label, expiresAt })
    const toSettleIds = new Set(toSettle.map(a => a.id))

    for (const previous of replaced) {
      const previousCommit = commits.get(previous.id)
      const previousRevert = reverts.get(previous.id)
      commits.delete(previous.id)
      reverts.delete(previous.id)

      // Not reported for settling: an exact-set match, dropped outright — its revert must never
      // fire once a newer apply has run on top of it.
      if (!toSettleIds.has(previous.id) || !previousCommit) continue

      // Fire it now rather than waiting out its remaining window: the entities this new action
      // doesn't touch still need this write to reach the server, or it is lost for good. Scoped
      // to queue.owned(previous.entityIds): the entities *this* new action just claimed are no
      // longer `previous`'s to restore if that commit then fails.
      void settleEarly(previousCommit, () => previousRevert?.(queue.owned(previous.entityIds)))
    }

    commits.set(id, commit)
    reverts.set(id, revert)
    notifyChange()

    // The queue just gave every pending action this same expiresAt (see undoQueue.ts), so arming
    // the one timer against it keeps the caller's timer and remainingRatio in lockstep.
    callbacks.scheduleExpiry(expiresAt, () => void onExpire())
  }

  const undoAll = (): void => {
    const all = queue.list()
    callbacks.cancelScheduledExpiry()

    for (const pendingAction of all) {
      queue.cancel(pendingAction.id)
      const revert = reverts.get(pendingAction.id)
      commits.delete(pendingAction.id)
      reverts.delete(pendingAction.id)
      // Always the full entityIds in practice — a still-pending action's ids can't overlap any
      // other pending action's (see undoQueue.ts) — but routed through queue.owned() anyway so
      // every revert call here honours the same contract run()/onExpire do.
      revert?.(queue.owned(pendingAction.entityIds))
    }

    notifyChange()
  }

  const isPendingEntity = (entityId: string): boolean => queue.has(entityId)
  const list = (): PendingAction[] => queue.list()

  return { run, undoAll, isPendingEntity, list }
}
