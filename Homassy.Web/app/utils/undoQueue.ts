/**
 * Optimistic-action queue behind the app's 5-second undo window (see `useUndoableAction.ts` for
 * the composable that wraps this with a timer, a `requestAnimationFrame` clock, and error
 * reporting). Pure and synchronous — no timers, no Vue — so the one non-obvious rule below can be
 * tested without booting the app; anything time- or reactivity-related lives in the composable.
 *
 * NON-OBVIOUS RULE — same-entity replacement: adding a second pending action for an `entityId`
 * that already has one *replaces* it instead of stacking. Two pending mutations of one row (e.g.
 * a delete and, a moment later, a purchase on the same shopping-list item, both still inside
 * their undo window) cannot be shown or reverted coherently — there is one row and one Undo
 * button, so it can only be in one pending state at a time. The newer action wins outright: its
 * own `apply` already ran on top of whatever the older action's `apply` had done, so the older
 * action's `revert` would no longer make sense and is simply dropped, never called. Callers
 * building `apply`/`revert` closures for anything but a single, isolated mutation should re-locate
 * the entity by id when they run rather than trusting a position captured earlier, since a
 * replacement can happen between one action being queued and it being undone or committed.
 *
 * Every pending action also shares one queue-wide deadline rather than keeping its own: adding an
 * action resets `expiresAt` on everything already queued to match the new arrival. This is what
 * lets a burst of actions collapse into one toast with one shrinking ring instead of several
 * independently-expiring ones — see `useUndoableAction.ts`, which is the only thing that actually
 * arms a JS timer against this value.
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
  /** The row/item this action is about. Same-entity replacement keys off this. */
  entityId: string
  kind: UndoKind
  /** This action's own label, used verbatim when it is the only one pending. */
  label: string
  /** Epoch ms; shared across the whole queue (see the file header). */
  expiresAt: number
}

export interface UndoQueue {
  add: (action: PendingAction) => void
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
 * One action returns its own label, verbatim. Several collapse to a kind-specific translation key
 * (`undo.collapsed.delete` / `.purchase` / `.move`) carrying the count; a mix of kinds collapses
 * to `undo.collapsed.mixed`. An empty list has nothing to say.
 */
export const collapseLabel = (
  actions: PendingAction[],
  t: (key: string, params?: Record<string, unknown>) => string
): string => {
  if (actions.length === 0) return ''
  if (actions.length === 1) return actions[0]!.label

  const count = actions.length
  const kinds = new Set(actions.map(a => a.kind))
  const singleKind = kinds.size === 1 ? [...kinds][0] : null

  return singleKind
    ? t(`undo.collapsed.${singleKind}`, { count })
    : t('undo.collapsed.mixed', { count })
}

export const createUndoQueue = (): UndoQueue => {
  let actions: PendingAction[] = []

  const add = (action: PendingAction): void => {
    const others = actions.filter(a => a.entityId !== action.entityId)
    // Shared queue-wide deadline — see the file header comment.
    actions = [...others.map(a => ({ ...a, expiresAt: action.expiresAt })), action]
  }

  const cancel = (id: string): void => {
    actions = actions.filter(a => a.id !== id)
  }

  const drain = (now: number = Date.now()): PendingAction[] => {
    const due = actions.filter(a => a.expiresAt <= now)
    if (due.length > 0) actions = actions.filter(a => a.expiresAt > now)
    return due
  }

  const has = (entityId: string): boolean => actions.some(a => a.entityId === entityId)

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
