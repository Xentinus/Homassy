import { describe, expect, it, vi } from 'vitest'
import { collapseLabel, createUndoQueue, settleCommit, type PendingAction } from '~/utils/undoQueue'

/** Matches the shape `collapseLabel`'s `t` parameter expects, without pulling in real i18n. */
const fakeT = (key: string, params?: Record<string, unknown>): string =>
  `${key}:${params?.count ?? ''}`

const action = (overrides: Partial<PendingAction> = {}): PendingAction => ({
  id: overrides.id ?? 'action-1',
  entityIds: overrides.entityIds ?? ['entity-1'],
  kind: overrides.kind ?? 'delete',
  label: overrides.label ?? 'Item removed',
  expiresAt: overrides.expiresAt ?? 1000
})

describe('createUndoQueue', () => {
  it('adds an action so it is listed and its entity is pending', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityIds: ['e1'] }))

    expect(queue.list()).toHaveLength(1)
    expect(queue.has('e1')).toBe(true)
  })

  it('cancels a pending action by id, clearing its entity', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityIds: ['e1'] }))
    queue.cancel('a1')

    expect(queue.list()).toHaveLength(0)
    expect(queue.has('e1')).toBe(false)
  })

  it('drain returns everything due and empties the queue', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityIds: ['e1'], expiresAt: 1000 }))

    // Not due yet: nothing is drained, and it is still pending.
    expect(queue.drain(500)).toHaveLength(0)
    expect(queue.has('e1')).toBe(true)

    // Due: drained and removed.
    const due = queue.drain(1000)
    expect(due.map(a => a.id)).toEqual(['a1'])
    expect(queue.list()).toHaveLength(0)
    expect(queue.has('e1')).toBe(false)
  })

  it('replaces rather than stacks a second action for the same entity', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityIds: ['e1'], kind: 'delete', label: 'Deleted' }))
    queue.add(action({ id: 'a2', entityIds: ['e1'], kind: 'purchase', label: 'Purchased' }))

    const list = queue.list()
    expect(list).toHaveLength(1)
    expect(list[0]?.id).toBe('a2')
    expect(list[0]?.kind).toBe('purchase')
    expect(queue.has('e1')).toBe(true)
  })

  it('has() is false for an entity that was never added, and after its action is drained', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityIds: ['e1'], expiresAt: 1000 }))

    expect(queue.has('never-added')).toBe(false)

    queue.drain(1000)
    expect(queue.has('e1')).toBe(false)
  })

  // A batched action (e.g. moving 50 inventory items to a new storage location in one request)
  // spans several entities. Every rule above must hold for every member id, not just the first —
  // a synthetic batch id would leave the rest unguarded during the undo window (see the file
  // header comment).
  it('has() is true for every member id of a multi-entity action', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityIds: ['e1', 'e2', 'e3'], kind: 'move' }))

    expect(queue.has('e1')).toBe(true)
    expect(queue.has('e2')).toBe(true)
    expect(queue.has('e3')).toBe(true)
    expect(queue.has('e4')).toBe(false)
  })

  it('replaces a pending action when any one of its member ids overlaps a new action', () => {
    const queue = createUndoQueue()
    // e2 is claimed by a pending single-item action...
    queue.add(action({ id: 'a1', entityIds: ['e2'], kind: 'delete', label: 'Deleted' }))
    // ...and a new batch touching e1/e2/e3 overlaps it on e2 alone.
    queue.add(action({ id: 'a2', entityIds: ['e1', 'e2', 'e3'], kind: 'move', label: 'Moved' }))

    const list = queue.list()
    expect(list).toHaveLength(1)
    expect(list[0]?.id).toBe('a2')
    // The whole earlier action is dropped, not just its one overlapping id — every member of the
    // new batch is pending, e1 and e3 included, even though neither was in the old action.
    expect(queue.has('e1')).toBe(true)
    expect(queue.has('e2')).toBe(true)
    expect(queue.has('e3')).toBe(true)
  })

  // The regression this fix closes: a partial overlap (new action's entity set isn't exactly the
  // old one's) used to drop the whole old action outright, discarding its commit — for a batch,
  // that silently lost the write for every entity the new action didn't touch (e1 and e3 below),
  // while the optimistic UI kept showing them as moved. `add()` now reports these via `toSettle`
  // instead of just dropping them, so the caller (`useUndoableAction.ts`) can fire the old
  // action's commit right now rather than lose it. See the file header's NON-OBVIOUS RULE.
  describe('AddResult — settling a partial overlap instead of silently dropping it', () => {
    it('reports a partially-overlapping action to settle, and still queues the new one', () => {
      const queue = createUndoQueue()
      // A 3-item batched move is pending...
      queue.add(action({ id: 'a1', entityIds: ['e1', 'e2', 'e3'], kind: 'move', label: 'Moved' }))
      // ...then, inside the same window, a solo delete touches just one of those entities.
      const result = queue.add(action({ id: 'a2', entityIds: ['e2'], kind: 'delete', label: 'Deleted' }))

      expect(result.replaced.map(a => a.id)).toEqual(['a1'])
      expect(result.toSettle.map(a => a.id)).toEqual(['a1'])

      // a1 is gone from the pending list either way (it is being settled, not left pending), and
      // a2 is now what's pending — including e1/e3, which a2 doesn't even touch, staying clear.
      expect(queue.list().map(a => a.id)).toEqual(['a2'])
      expect(queue.has('e1')).toBe(false)
      expect(queue.has('e2')).toBe(true)
      expect(queue.has('e3')).toBe(false)
    })

    it('does not report an exact entity-set match to settle — it is replaced outright, same as before', () => {
      const queue = createUndoQueue()
      queue.add(action({ id: 'a1', entityIds: ['e1', 'e2', 'e3'], kind: 'move', label: 'Moved once' }))
      // Same three entities as a *set*, just reordered — still an exact match, not a partial one.
      const result = queue.add(action({ id: 'a2', entityIds: ['e3', 'e1', 'e2'], kind: 'move', label: 'Moved again' }))

      expect(result.replaced.map(a => a.id)).toEqual(['a1'])
      expect(result.toSettle).toEqual([])
      expect(queue.list().map(a => a.id)).toEqual(['a2'])
    })

    it('reports nothing when the new action does not overlap anything pending', () => {
      const queue = createUndoQueue()
      queue.add(action({ id: 'a1', entityIds: ['e1'], kind: 'delete', label: 'Deleted' }))
      const result = queue.add(action({ id: 'a2', entityIds: ['e9'], kind: 'delete', label: 'Also deleted' }))

      expect(result.replaced).toEqual([])
      expect(result.toSettle).toEqual([])
      expect(queue.list()).toHaveLength(2)
    })

    // What useUndoableAction.ts's run() actually does with each action in `toSettle`: fire its
    // own commit right now via the same settleCommit() the normal 5-second expiry uses (see the
    // settleCommit describe block below for its other cases), instead of discarding it.
    it('settling a reported action runs its commit and leaves its revert untouched on success', async () => {
      const queue = createUndoQueue()
      queue.add(action({ id: 'a1', entityIds: ['e1', 'e2', 'e3'], kind: 'move', label: 'Moved' }))
      const { toSettle } = queue.add(action({ id: 'a2', entityIds: ['e2'], kind: 'delete', label: 'Deleted' }))
      expect(toSettle.map(a => a.id)).toEqual(['a1'])

      const oldCommit = vi.fn(async () => ({ success: true }))
      const oldRevert = vi.fn()
      const succeeded = await settleCommit(oldCommit, oldRevert)

      expect(succeeded).toBe(true)
      expect(oldCommit).toHaveBeenCalledOnce()
      expect(oldRevert).not.toHaveBeenCalled()
    })

    // The failure case: settling early is still subject to the same rule as a normal expiry — a
    // rejection or a resolved `success: false` both revert (and, in useUndoableAction.ts, still
    // report through the one shared toast).
    it('settling a reported action still reverts when its commit resolves { success: false }', async () => {
      const queue = createUndoQueue()
      queue.add(action({ id: 'a1', entityIds: ['e1', 'e2', 'e3'], kind: 'move', label: 'Moved' }))
      const { toSettle } = queue.add(action({ id: 'a2', entityIds: ['e2'], kind: 'delete', label: 'Deleted' }))
      expect(toSettle.map(a => a.id)).toEqual(['a1'])

      const oldRevert = vi.fn()
      const succeeded = await settleCommit(async () => ({ success: false }), oldRevert)

      expect(succeeded).toBe(false)
      expect(oldRevert).toHaveBeenCalledOnce()
    })
  })
})

describe('collapseLabel', () => {
  it('returns the single action\'s own label verbatim', () => {
    const only = action({ label: 'Milk removed' })
    expect(collapseLabel([only], fakeT)).toBe('Milk removed')
  })

  it('returns a batched action\'s own label verbatim even though it spans several entities', () => {
    const batch = action({ entityIds: ['e1', 'e2', 'e3'], label: '3 items moved' })
    expect(collapseLabel([batch], fakeT)).toBe('3 items moved')
  })

  it('collapses several actions of the same kind to the kind-specific key', () => {
    const actions = [
      action({ id: 'a1', entityIds: ['e1'], kind: 'delete' }),
      action({ id: 'a2', entityIds: ['e2'], kind: 'delete' }),
      action({ id: 'a3', entityIds: ['e3'], kind: 'delete' })
    ]
    expect(collapseLabel(actions, fakeT)).toBe('undo.collapsed.delete:3')
  })

  it('collapses a mix of kinds to the mixed key', () => {
    const actions = [
      action({ id: 'a1', entityIds: ['e1'], kind: 'delete' }),
      action({ id: 'a2', entityIds: ['e2'], kind: 'delete' }),
      action({ id: 'a3', entityIds: ['e3'], kind: 'purchase' })
    ]
    expect(collapseLabel(actions, fakeT)).toBe('undo.collapsed.mixed:3')
  })

  // The regression this fix closes: a batched action must count every entity it spans once it
  // collapses alongside another action, not count as a single action itself — else a 50-item
  // batched move collapsing with one unrelated delete would undercount as "2 changes".
  it('counts entities rather than actions once a batch collapses with another action', () => {
    const actions = [
      action({ id: 'a1', entityIds: ['e1', 'e2', 'e3'], kind: 'move' }), // a 3-item batch...
      action({ id: 'a2', entityIds: ['e4'], kind: 'delete' }) // ...plus one unrelated delete.
    ]
    // 4 entities total, not 2 actions — and mixed, since move and delete are both present.
    expect(collapseLabel(actions, fakeT)).toBe('undo.collapsed.mixed:4')
  })

  it('returns an empty string for an empty list', () => {
    expect(collapseLabel([], fakeT)).toBe('')
  })
})

// This is the regression that must never come back: a `commit()` that resolves normally with
// `{ success: false }` (the shape an HTTP error status actually takes — see useApiClient.ts's
// request() — not a rejection) used to be silently treated as a success, leaving the optimistic
// mutation permanently wrong on screen with no revert and no error surfaced.
describe('settleCommit', () => {
  it('returns true and never reverts when the commit resolves successfully', async () => {
    const revert = vi.fn()

    const succeeded = await settleCommit(async () => ({ success: true }), revert)

    expect(succeeded).toBe(true)
    expect(revert).not.toHaveBeenCalled()
  })

  it('reverts and returns false when the commit resolves with success: false', async () => {
    const revert = vi.fn()

    const succeeded = await settleCommit(async () => ({ success: false }), revert)

    expect(succeeded).toBe(false)
    expect(revert).toHaveBeenCalledOnce()
  })

  it('reverts and returns false when the commit rejects (a transport failure)', async () => {
    const revert = vi.fn()

    const succeeded = await settleCommit(async () => { throw new Error('network down') }, revert)

    expect(succeeded).toBe(false)
    expect(revert).toHaveBeenCalledOnce()
  })
})
