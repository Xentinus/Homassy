import { describe, expect, it } from 'vitest'
import { collapseLabel, createUndoQueue, type PendingAction } from '~/utils/undoQueue'

/** Matches the shape `collapseLabel`'s `t` parameter expects, without pulling in real i18n. */
const fakeT = (key: string, params?: Record<string, unknown>): string =>
  `${key}:${params?.count ?? ''}`

const action = (overrides: Partial<PendingAction> = {}): PendingAction => ({
  id: overrides.id ?? 'action-1',
  entityId: overrides.entityId ?? 'entity-1',
  kind: overrides.kind ?? 'delete',
  label: overrides.label ?? 'Item removed',
  expiresAt: overrides.expiresAt ?? 1000
})

describe('createUndoQueue', () => {
  it('adds an action so it is listed and its entity is pending', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityId: 'e1' }))

    expect(queue.list()).toHaveLength(1)
    expect(queue.has('e1')).toBe(true)
  })

  it('cancels a pending action by id, clearing its entity', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityId: 'e1' }))
    queue.cancel('a1')

    expect(queue.list()).toHaveLength(0)
    expect(queue.has('e1')).toBe(false)
  })

  it('drain returns everything due and empties the queue', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityId: 'e1', expiresAt: 1000 }))

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
    queue.add(action({ id: 'a1', entityId: 'e1', kind: 'delete', label: 'Deleted' }))
    queue.add(action({ id: 'a2', entityId: 'e1', kind: 'purchase', label: 'Purchased' }))

    const list = queue.list()
    expect(list).toHaveLength(1)
    expect(list[0]?.id).toBe('a2')
    expect(list[0]?.kind).toBe('purchase')
    expect(queue.has('e1')).toBe(true)
  })

  it('has() is false for an entity that was never added, and after its action is drained', () => {
    const queue = createUndoQueue()
    queue.add(action({ id: 'a1', entityId: 'e1', expiresAt: 1000 }))

    expect(queue.has('never-added')).toBe(false)

    queue.drain(1000)
    expect(queue.has('e1')).toBe(false)
  })
})

describe('collapseLabel', () => {
  it('returns the single action\'s own label verbatim', () => {
    const only = action({ label: 'Milk removed' })
    expect(collapseLabel([only], fakeT)).toBe('Milk removed')
  })

  it('collapses several actions of the same kind to the kind-specific key', () => {
    const actions = [
      action({ id: 'a1', entityId: 'e1', kind: 'delete' }),
      action({ id: 'a2', entityId: 'e2', kind: 'delete' }),
      action({ id: 'a3', entityId: 'e3', kind: 'delete' })
    ]
    expect(collapseLabel(actions, fakeT)).toBe('undo.collapsed.delete:3')
  })

  it('collapses a mix of kinds to the mixed key', () => {
    const actions = [
      action({ id: 'a1', entityId: 'e1', kind: 'delete' }),
      action({ id: 'a2', entityId: 'e2', kind: 'delete' }),
      action({ id: 'a3', entityId: 'e3', kind: 'purchase' })
    ]
    expect(collapseLabel(actions, fakeT)).toBe('undo.collapsed.mixed:3')
  })

  it('returns an empty string for an empty list', () => {
    expect(collapseLabel([], fakeT)).toBe('')
  })
})
