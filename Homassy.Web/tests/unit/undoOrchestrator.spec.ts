import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createUndoOrchestrator } from '~/utils/undoOrchestrator'
import { UNDO_WINDOW_MS, type CommitResult } from '~/utils/undoQueue'

// undoQueue.ts's drain() (what onExpire calls) checks real elapsed time (`expiresAt <= now`), not
// merely whether the fake scheduler's callback was invoked — firing it early, without also moving
// the clock, would leave the action not yet "due" and the assertions below seeing zero calls. Fake
// timers only ever control Date.now() in this file (the fake scheduler below bypasses
// setTimeout/clearTimeout entirely), so faking just 'date' is enough.
beforeEach(() => { vi.useFakeTimers({ toFake: ['Date'] }) })
afterEach(() => { vi.useRealTimers() })

/** Moves the virtual clock far enough that anything queued at "now" is due. */
const advanceBeyondUndoWindow = (): void => {
  vi.setSystemTime(new Date(Date.now() + UNDO_WINDOW_MS + 1))
}

/**
 * `useUndoableAction.ts` has never had a spec of its own — precisely the code that broke in two
 * consecutive fix rounds (the settle-then-replace revert bug this same milestone's Fix 2 closes,
 * and the presence/timeline bugs elsewhere in this round). It could not be tested directly: it is
 * a Vue composable that calls Nuxt auto-imports (`useToast`, `useI18n`) which only resolve inside
 * a running Nuxt app, and this project deliberately carries no `@vue/test-utils` or `happy-dom` to
 * boot one. `undoOrchestrator.ts` is the orchestration extracted out of it into a plain,
 * framework-free module for exactly this reason — this spec exercises that module directly.
 *
 * A fake scheduler stands in for the composable's real `setTimeout`-backed one: it just remembers
 * whatever `scheduleExpiry` last armed, so a test can fire it deliberately instead of reaching for
 * fake timers or waiting out the real 5-second window. `fire()` is deliberately synchronous and
 * does not await the async work it kicks off — that mirrors how `useUndoableAction.ts` itself
 * calls `onExpire` from inside the timer callback (`() => void onExpire()`, fire-and-forget) — so
 * every spec that fires expiry or triggers a settle-early asserts through `vi.waitFor` rather than
 * a fixed number of awaited microtask ticks.
 */
const createFakeScheduler = () => {
  let armed: (() => void) | null = null
  return {
    scheduleExpiry: vi.fn((_expiresAt: number, fire: () => void) => { armed = fire }),
    cancelScheduledExpiry: vi.fn(() => { armed = null }),
    fire: (): void => {
      const toFire = armed
      armed = null
      toFire?.()
    },
    isArmed: (): boolean => armed !== null
  }
}

const createHarness = () => {
  const scheduler = createFakeScheduler()
  const onChange = vi.fn()
  const onSettleFailure = vi.fn()
  const orchestrator = createUndoOrchestrator({
    onChange,
    scheduleExpiry: scheduler.scheduleExpiry,
    cancelScheduledExpiry: scheduler.cancelScheduledExpiry,
    onSettleFailure
  })
  return { orchestrator, scheduler, onChange, onSettleFailure }
}

const succeeds = async (): Promise<CommitResult> => ({ success: true })
const fails = async (): Promise<CommitResult> => ({ success: false })

describe('createUndoOrchestrator', () => {
  it('applies immediately and marks the entity pending', () => {
    const { orchestrator, scheduler } = createHarness()
    const apply = vi.fn()

    orchestrator.run({ entityIds: ['e1'], kind: 'delete', label: 'Deleted', apply, revert: vi.fn(), commit: vi.fn(succeeds) })

    expect(apply).toHaveBeenCalledOnce()
    expect(orchestrator.isPendingEntity('e1')).toBe(true)
    expect(scheduler.isArmed()).toBe(true)
  })

  it('undo before expiry fires no commit', () => {
    const { orchestrator, scheduler } = createHarness()
    const commit = vi.fn(succeeds)
    const revert = vi.fn()

    orchestrator.run({ entityIds: ['e1'], kind: 'delete', label: 'Deleted', apply: vi.fn(), revert, commit })
    orchestrator.undoAll()

    expect(commit).not.toHaveBeenCalled()
    expect(revert).toHaveBeenCalledOnce()
    expect(revert).toHaveBeenCalledWith(['e1'])
    expect(orchestrator.isPendingEntity('e1')).toBe(false)
    expect(scheduler.cancelScheduledExpiry).toHaveBeenCalled()
  })

  it('expiry fires the commit', async () => {
    const { orchestrator, scheduler } = createHarness()
    const commit = vi.fn(succeeds)
    const revert = vi.fn()

    orchestrator.run({ entityIds: ['e1'], kind: 'delete', label: 'Deleted', apply: vi.fn(), revert, commit })
    expect(scheduler.isArmed()).toBe(true)

    advanceBeyondUndoWindow()
    scheduler.fire()

    await vi.waitFor(() => expect(commit).toHaveBeenCalledOnce())
    expect(revert).not.toHaveBeenCalled()
    expect(orchestrator.isPendingEntity('e1')).toBe(false)
  })

  it('a commit resolving { success: false } reverts and reports', async () => {
    const { orchestrator, scheduler, onSettleFailure } = createHarness()
    const revert = vi.fn()

    orchestrator.run({ entityIds: ['e1'], kind: 'delete', label: 'Deleted', apply: vi.fn(), revert, commit: fails })
    advanceBeyondUndoWindow()
    scheduler.fire()

    await vi.waitFor(() => expect(revert).toHaveBeenCalledOnce())
    expect(revert).toHaveBeenCalledWith(['e1'])
    expect(onSettleFailure).toHaveBeenCalledOnce()
  })

  it('a rejected commit is treated the same as { success: false } — reverts and reports', async () => {
    const { orchestrator, scheduler, onSettleFailure } = createHarness()
    const revert = vi.fn()

    orchestrator.run({
      entityIds: ['e1'],
      kind: 'delete',
      label: 'Deleted',
      apply: vi.fn(),
      revert,
      commit: async () => { throw new Error('network down') }
    })
    advanceBeyondUndoWindow()
    scheduler.fire()

    await vi.waitFor(() => expect(revert).toHaveBeenCalledOnce())
    expect(onSettleFailure).toHaveBeenCalledOnce()
  })

  // A 3-item batched move is pending; within its window, a solo action touching just one of those
  // entities partially overlaps it. The batch must settle its own commit right now rather than
  // wait out its remaining undo window (see undoQueue.ts's file header) — the entities the new
  // action doesn't touch would otherwise never reach the server.
  it('a partial overlap settles the older action early instead of waiting for its own expiry', async () => {
    const { orchestrator } = createHarness()
    const batchCommit = vi.fn(succeeds)
    const batchRevert = vi.fn()

    orchestrator.run({
      entityIds: ['e1', 'e2', 'e3'],
      kind: 'move',
      label: 'Moved',
      apply: vi.fn(),
      revert: batchRevert,
      commit: batchCommit
    })

    orchestrator.run({
      entityIds: ['e2'],
      kind: 'delete',
      label: 'Deleted',
      apply: vi.fn(),
      revert: vi.fn(),
      commit: vi.fn(succeeds)
    })

    await vi.waitFor(() => expect(batchCommit).toHaveBeenCalledOnce())
    expect(batchRevert).not.toHaveBeenCalled()
    // The batch is gone from the pending list the instant it is settled, not once its commit
    // resolves — e1/e3 were never reachable through the new (single-entity) action either.
    expect(orchestrator.isPendingEntity('e1')).toBe(false)
    expect(orchestrator.isPendingEntity('e3')).toBe(false)
  })

  // The revert-ownership rule from Fix 2: a settled-early batch whose commit then fails must
  // revert only the entities it still owns — not the one a newer action has since claimed and
  // already committed (or is about to). See undoQueue.ts's fourth NON-OBVIOUS RULE.
  it('scopes a settled-early revert to the entities not claimed by the newer action', async () => {
    const { orchestrator } = createHarness()
    const batchRevert = vi.fn()
    const soloRevert = vi.fn()

    orchestrator.run({
      entityIds: ['e1', 'e2', 'e3'],
      kind: 'move',
      label: 'Moved',
      apply: vi.fn(),
      revert: batchRevert,
      commit: fails
    })

    orchestrator.run({
      entityIds: ['e2'],
      kind: 'delete',
      label: 'Deleted',
      apply: vi.fn(),
      revert: soloRevert,
      commit: succeeds
    })

    await vi.waitFor(() => expect(batchRevert).toHaveBeenCalledOnce())
    expect(batchRevert).toHaveBeenCalledWith(['e1', 'e3'])
    // The newer action's own commit succeeded, so it has nothing to revert — and nothing else
    // should have touched e2 on its behalf.
    expect(soloRevert).not.toHaveBeenCalled()
  })

  it('an exact entity-set match replaces the old action outright — neither its commit nor its revert ever fire', () => {
    const { orchestrator } = createHarness()
    const oldCommit = vi.fn(succeeds)
    const oldRevert = vi.fn()

    orchestrator.run({ entityIds: ['e1'], kind: 'delete', label: 'Deleted', apply: vi.fn(), revert: oldRevert, commit: oldCommit })
    orchestrator.run({ entityIds: ['e1'], kind: 'purchase', label: 'Purchased', apply: vi.fn(), revert: vi.fn(), commit: vi.fn(succeeds) })

    orchestrator.undoAll()

    expect(oldCommit).not.toHaveBeenCalled()
    expect(oldRevert).not.toHaveBeenCalled()
  })
})
