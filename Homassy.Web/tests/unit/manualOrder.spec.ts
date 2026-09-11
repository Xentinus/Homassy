import { describe, expect, it } from 'vitest'
import {
  MANUAL_ORDER_GAP,
  applyOrderEntries,
  planOptimisticOrder,
  sameOrder,
  sortByManualOrder
} from '~/utils/manualOrder'

/** A row, as the ordering helpers see one. */
const row = (publicId: string, sortOrder: number, name = publicId) => ({ publicId, sortOrder, name })

const byName = (a: { name: string }, b: { name: string }) => a.name.localeCompare(b.name)

describe('sortByManualOrder', () => {
  it('orders by the stored manual position', () => {
    const items = [row('c', 3000), row('a', 1000), row('b', 2000)]

    expect(sortByManualOrder(items).map(i => i.publicId)).toEqual(['a', 'b', 'c'])
  })

  it('falls back to the base order for a list nobody has dragged', () => {
    // Every row of an untouched list holds 0, which is the case the tie-break exists for: without
    // it the list would render in whatever order the API happened to return.
    const items = [row('x', 0, 'Zebra'), row('y', 0, 'Apple'), row('z', 0, 'Mango')]

    expect(sortByManualOrder(items, byName).map(i => i.name)).toEqual(['Apple', 'Mango', 'Zebra'])
  })

  it('puts a dragged row ahead of untouched ones regardless of the base order', () => {
    const items = [row('a', 0, 'Apple'), row('z', -1000, 'Zebra')]

    expect(sortByManualOrder(items, byName).map(i => i.name)).toEqual(['Zebra', 'Apple'])
  })

  it('leaves the input array alone', () => {
    const items = [row('b', 2000), row('a', 1000)]
    sortByManualOrder(items)

    expect(items.map(i => i.publicId)).toEqual(['b', 'a'])
  })
})

describe('planOptimisticOrder', () => {
  it('hands the dragged rows their own positions back in the new order', () => {
    const items = [row('a', 1000), row('b', 2000), row('c', 3000)]

    const planned = planOptimisticOrder(items, ['c', 'a', 'b'])

    expect(planned.get('c')).toBe(1000)
    expect(planned.get('a')).toBe(2000)
    expect(planned.get('b')).toBe(3000)
  })

  it('leaves a filtered-out row where it was relative to the visible ones', () => {
    // `hidden` sits at 1500, between a and b. Reusing only the visible rows' own slots keeps it
    // there; renumbering the visible rows by index would have moved it to the end of the list.
    const items = [row('a', 1000), row('hidden', 1500), row('b', 2000), row('c', 3000)]

    const planned = planOptimisticOrder(items, ['c', 'a', 'b'])
    const next = items.map(item => ({ ...item, sortOrder: planned.get(item.publicId) ?? item.sortOrder }))

    expect(sortByManualOrder(next).map(i => i.publicId)).toEqual(['c', 'hidden', 'a', 'b'])
    expect(planned.has('hidden')).toBe(false)
  })

  it('numbers a list that has never been dragged from scratch', () => {
    const items = [row('a', 0), row('b', 0), row('c', 0)]

    const planned = planOptimisticOrder(items, ['b', 'c', 'a'])

    expect(planned.get('b')).toBe(MANUAL_ORDER_GAP)
    expect(planned.get('c')).toBe(2 * MANUAL_ORDER_GAP)
    expect(planned.get('a')).toBe(3 * MANUAL_ORDER_GAP)
  })

  it('ignores an id that resolves to no row', () => {
    const items = [row('a', 1000), row('b', 2000)]

    const planned = planOptimisticOrder(items, ['b', 'gone', 'a'])

    expect([...planned.keys()].sort()).toEqual(['a', 'b'])
    expect(planned.get('b')).toBe(1000)
    expect(planned.get('a')).toBe(2000)
  })

  it('plans nothing for an empty request', () => {
    expect(planOptimisticOrder([row('a', 1000)], []).size).toBe(0)
  })
})

describe('applyOrderEntries', () => {
  it('takes the positions of the rows an event names', () => {
    const items = [row('a', 1000), row('b', 2000)]

    const applied = applyOrderEntries(items, [{ publicId: 'b', sortOrder: 500 }])

    expect(applied.get('b')).toBe(500)
    // The event carries only what moved, so an unmentioned row keeps what it had.
    expect(applied.has('a')).toBe(false)
  })

  it('ignores an entry for a row this client does not have', () => {
    // The event says where a row goes, never that it exists — inserting one from a reorder would
    // put a row on screen with nothing but an id.
    const applied = applyOrderEntries([row('a', 1000)], [{ publicId: 'elsewhere', sortOrder: 10 }])

    expect(applied.size).toBe(0)
  })
})

describe('sameOrder', () => {
  it('is true for the same ids in the same order', () => {
    expect(sameOrder(['a', 'b'], ['a', 'b'])).toBe(true)
  })

  it('is false when the order differs', () => {
    expect(sameOrder(['a', 'b'], ['b', 'a'])).toBe(false)
  })

  it('is false when the lengths differ', () => {
    expect(sameOrder(['a'], ['a', 'b'])).toBe(false)
  })
})
