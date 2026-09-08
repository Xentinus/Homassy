/**
 * Optimistic-action queue behind the app's 5-second undo window (see `useUndoableAction.ts` for
 * the composable that wraps this with a timer, a `requestAnimationFrame` clock, and error
 * reporting). Pure and synchronous — no timers, no Vue — so the one non-obvious rule below can be
 * tested without booting the app; anything time- or reactivity-related lives in the composable.
 *
 * NON-OBVIOUS RULE — one action can span several entities, and replacement is overlap-based: a
 * `PendingAction` carries `entityIds`, a *collection* — one entry for an ordinary single-row
 * action, several for a batch (e.g. moving 50 inventory items to a new storage location at once,
 * committed as the one request it always should have been rather than 50). Adding a new pending
 * action that shares **any** entity id with an existing one always removes that existing action
 * from the pending list — it can never stay half-pending, split by entity — but what happens to
 * its `commit` depends on whether the overlap is total or partial (`add()`'s `AddResult`, below):
 *
 * - **Exact entity-set match** (the new action's `entityIds`, as a *set*, equal the old one's):
 *   the newer action wins outright and the old one is simply dropped — its `commit` is never
 *   called, and neither is its `revert`. Safe because the newer `apply` already ran on top of
 *   exactly what the older action's `apply` had done, over the identical entities; nothing the
 *   older action would have written is left undone.
 * - **Partial overlap** (some but not all entity ids in common — e.g. a 3-item batched move later
 *   overlapping a swipe-delete of just one of those items): the old action is *not* simply
 *   dropped. Its `apply` was only superseded for the entities the two actions share — the entities
 *   the new action doesn't touch still need the old action's own `commit` to actually reach the
 *   server, and dropping it would silently lose that write while leaving the optimistic UI
 *   showing it as done. `add()` reports these as `toSettle` instead of discarding them; this
 *   module has no timers or async of its own, so it is `useUndoableAction.ts` that actually fires
 *   each one's `commit` right now rather than waiting out its remaining undo window, keeping its
 *   `revert` wired in case that commit fails. The user loses the ability to undo the *older*
 *   action, which is the right trade: they have visibly moved on, and losing an undo is far better
 *   than losing a write.
 *
 * Two pending mutations touching the same row (e.g. a delete and, a moment later, a purchase on
 * the same shopping-list item, both still inside their undo window) cannot be shown or reverted
 * coherently — there is one row and one Undo button, so it can only be in one pending state at a
 * time; the same reasoning extends to a batch that re-touches a row another pending action already
 * claimed, exact-match or not. Callers building `apply`/`revert` closures for anything but a
 * single, isolated mutation should re-locate each entity by id when they run rather than trusting
 * a position captured earlier, since a replacement — or a settle — can happen between one action
 * being queued and it being undone, committed, or settled early.
 *
 * `has(entityId)` and every socket handler's pending guard (`isPendingEntity`, in
 * `useUndoableAction.ts`) check a *single real* entity id for membership in any pending action's
 * `entityIds` — a synthetic batch id would leave every other member of the batch unguarded during
 * the undo window, so there is no such id; a batch is guarded exactly by listing its real members.
 *
 * Every pending action also shares one queue-wide deadline rather than keeping its own: adding an
 * action resets `expiresAt` on everything already queued to match the new arrival. This is what
 * lets a burst of actions collapse into one toast with one shrinking ring instead of several
 * independently-expiring ones — see `useUndoableAction.ts`, which is the only thing that actually
 * arms a JS timer against this value. `collapseLabel` below counts *entities*, not actions, for the
 * same reason: one batched action can outweigh several ordinary ones.
 *
 * This module also defines the one rule for what counts as a *failed* commit (`CommitResult`,
 * `settleCommit`, near the bottom) — kept here rather than in the composable so it stays testable
 * without booting the app, same as the rules above.
 */

/** How long a queued action waits before it commits for real, in milliseconds. */
export const UNDO_WINDOW_MS = 5000

/** The three kinds of optimistic action the app currently queues. */
export type UndoKind = 'delete' | 'purchase' | 'move'

export interface PendingAction {
  /** Unique per queued action (not per entity — see `nextActionId`). */
  id: string
  /** The row(s)/item(s) this action is about — one entry for a single-row action, several for a
   *  batch. Overlap-based replacement and `has()`'s membership check both key off this. */
  entityIds: string[]
  kind: UndoKind
  /** This action's own label, used verbatim when it is the only one pending. */
  label: string
  /** Epoch ms; shared across the whole queue (see the file header). */
  expiresAt: number
}

/**
 * What `add()` did to the *previously* pending actions it overlapped, if any — see the file
 * header's NON-OBVIOUS RULE for the exact-match-vs-partial-overlap distinction. Both arrays are
 * `PendingAction`s, not the `commit`/`revert` closures for them: this module never holds those (see
 * `useUndoableAction.ts`'s `commits`/`reverts` maps, keyed by `PendingAction.id`), so releasing —
 * and, for `toSettle`, firing — that bookkeeping is entirely the caller's job.
 */
export interface AddResult {
  /** Every action `add()` just removed from the pending list because it shared at least one
   *  entity id with the new one — the union of an exact-match drop and a `toSettle` entry. The
   *  caller must release its own per-id `commit`/`revert` bookkeeping for each of these. */
  replaced: PendingAction[]
  /** The subset of `replaced` whose entity set was *not* an exact match for the new action's —
   *  these must be settled, not merely dropped: the caller should fire each one's `commit` right
   *  now (cutting its remaining undo window short) rather than discard it, keeping its `revert`
   *  wired so a failed commit still reverts and reports. An exact entity-set match is never in
   *  here — dropping it outright, without ever calling its commit, remains correct (file header). */
  toSettle: PendingAction[]
}

export interface UndoQueue {
  add: (action: PendingAction) => AddResult
  cancel: (id: string) => void
  drain: (now?: number) => PendingAction[]
  has: (entityId: string) => boolean
  list: () => PendingAction[]
  collapseLabel: (actions: PendingAction[], t: (key: string, params?: Record<string, unknown>) => string) => string
}

let nextId = 0

/** A per-module id generator so callers of `run()` never have to invent their own action ids. */
export const nextActionId = (): string => `undo-${++nextId}`

/**
 * One action returns its own label, verbatim — including a batch, whose own label already reads
 * like "N items moved" (see the call site). Several *actions* collapse to a kind-specific
 * translation key (`undo.collapsed.delete` / `.purchase` / `.move`) carrying the **entity** count
 * — a 50-item batched move collapsing alongside one unrelated delete is 51 changes, not 2 — with a
 * mix of kinds collapsing to `undo.collapsed.mixed`. An empty list has nothing to say.
 */
export const collapseLabel = (
  actions: PendingAction[],
  t: (key: string, params?: Record<string, unknown>) => string
): string => {
  if (actions.length === 0) return ''
  if (actions.length === 1) return actions[0]!.label

  const count = actions.reduce((sum, a) => sum + a.entityIds.length, 0)
  const kinds = new Set(actions.map(a => a.kind))
  const singleKind = kinds.size === 1 ? [...kinds][0] : null

  return singleKind
    ? t(`undo.collapsed.${singleKind}`, { count })
    : t('undo.collapsed.mixed', { count })
}

export const createUndoQueue = (): UndoQueue => {
  let actions: PendingAction[] = []

  const add = (action: PendingAction): AddResult => {
    const newIds = new Set(action.entityIds)
    const overlaps = (a: PendingAction): boolean => a.entityIds.some(id => newIds.has(id))
    // An exact match: the same entities, as a *set* — order never matters. This, and only this,
    // is what still drops the old action's commit outright rather than settling it (file header).
    const isExactMatch = (a: PendingAction): boolean =>
      a.entityIds.length === newIds.size && a.entityIds.every(id => newIds.has(id))

    const kept: PendingAction[] = []
    const replaced: PendingAction[] = []
    const toSettle: PendingAction[] = []

    for (const existing of actions) {
      if (!overlaps(existing)) {
        kept.push(existing)
        continue
      }
      replaced.push(existing)
      if (!isExactMatch(existing)) toSettle.push(existing)
    }

    // Shared queue-wide deadline — see the file header comment.
    actions = [...kept.map(a => ({ ...a, expiresAt: action.expiresAt })), action]

    return { replaced, toSettle }
  }

  const cancel = (id: string): void => {
    actions = actions.filter(a => a.id !== id)
  }

  const drain = (now: number = Date.now()): PendingAction[] => {
    const due = actions.filter(a => a.expiresAt <= now)
    if (due.length > 0) actions = actions.filter(a => a.expiresAt > now)
    return due
  }

  const has = (entityId: string): boolean => actions.some(a => a.entityIds.includes(entityId))

  const list = (): PendingAction[] => [...actions]

  return { add, cancel, drain, has, list, collapseLabel }
}

/**
 * The shape every real `commit()` resolves to (see `useApiClient.ts`'s `request()`, and this
 * repo's `Homassy.Web/CLAUDE.md` under "Who reports a failed request"): an HTTP error status — a
 * business-rule rejection, a 404, a 409 — resolves the promise normally with `success: false`
 * rather than throwing. Only a transport failure (the request never reaching the API) rejects.
 * A `commit()` that only reacts to a rejection and never inspects `success` treats the far more
 * common failure shape as a success, leaving the optimistic mutation permanently wrong on screen.
 */
export interface CommitResult {
  success: boolean
}

/**
 * Settles one due action's commit: a rejection and a resolved `success: false` are the same
 * failure (see `CommitResult` above), and both revert. Returns whether the commit actually
 * succeeded, so the caller can decide how to report a failure — deliberately not this function's
 * job, since reporting it is a toast that needs `useToast`/`useI18n`, and pulling those in here
 * would drag the Nuxt runtime into otherwise plain logic this file's tests exercise without
 * booting the app (see undoQueue.spec.ts).
 */
export async function settleCommit(commit: () => Promise<CommitResult>, revert: () => void): Promise<boolean> {
  try {
    const result = await commit()
    if (result.success) return true
  } catch {
    // A transport failure never reached the API at all — the same outcome as `success: false`.
  }
  revert()
  return false
}
