import { describe, expect, it } from 'vitest'
import {
  buildAtHomeItems,
  buildShoppingRows,
  buildTodayAgenda,
  countAwaitingPutAway,
  dayKey,
  filterPinnedNotes,
  pickHomeContext
} from '~/utils/homeFocus'
import { CalendarEventType, type CalendarEventInfo, type CalendarNoteInfo } from '~/types/calendar'
import { Unit } from '~/types/enums'
import type { DetailedProductInfo } from '~/types/product'
import type { LoadableShoppingListItemInfo, ShoppingListInfo } from '~/types/shoppingList'

/**
 * The home screen's derivations. Every expiry assertion is written relative to *today* rather than
 * to a frozen literal, because the ramp these go through reads the real clock: a fixture dated
 * 2026-09-21 is "expired" for exactly one day and then silently stops testing what it meant to.
 */

const DAY_MS = 86_400_000
const at = (offsetDays: number) => new Date(Date.now() + offsetDays * DAY_MS).toISOString()

const product = (name: string, items: Array<{ id: string, expirationAt?: string, location?: string }>): DetailedProductInfo => ({
  publicId: `p-${name}`,
  name,
  brand: 'Brand',
  isEatable: true,
  isFavorite: false,
  inventoryItems: items.map(item => ({
    publicId: item.id,
    currentQuantity: 1,
    unit: Unit.Piece,
    isSharedWithFamily: true,
    expirationAt: item.expirationAt,
    storageLocation: item.location ? { publicId: `loc-${item.location}`, name: item.location } : undefined,
    consumptionLogs: []
  }))
} as DetailedProductInfo)

describe('dayKey', () => {
  it('formats a local calendar day, not a UTC instant', () => {
    // 23:30 local on the 21st is the 21st, whatever that is in UTC.
    expect(dayKey(new Date(2026, 8, 21, 23, 30))).toBe('2026-09-21')
  })

  it('returns an empty string for a date it cannot read', () => {
    expect(dayKey('not-a-date')).toBe('')
  })
})

describe('buildAtHomeItems', () => {
  it('keeps only expired and critical stock, soonest first', () => {
    const rows = buildAtHomeItems([
      product('Milk', [{ id: 'milk-1', expirationAt: at(1), location: 'Fridge door' }]),
      product('Chicken', [{ id: 'chicken-1', expirationAt: at(-2), location: 'Fridge' }]),
      // 9 days out is `soon` on the ramp: amber in the grid, not a decision for tonight.
      product('Cheese', [{ id: 'cheese-1', expirationAt: at(9) }])
    ])

    expect(rows.map(row => row.publicId)).toEqual(['chicken-1', 'milk-1'])
    expect(rows[0]?.level).toBe('expired')
    expect(rows[1]?.level).toBe('critical')
  })

  it('flattens to one row per inventory item, not per product', () => {
    const rows = buildAtHomeItems([
      product('Milk', [
        { id: 'milk-1', expirationAt: at(-1) },
        { id: 'milk-2', expirationAt: at(2) }
      ])
    ])

    expect(rows).toHaveLength(2)
    expect(rows.map(row => row.productName)).toEqual(['Milk', 'Milk'])
  })

  it('carries the storage location when the item has one, and copes when it has none', () => {
    const rows = buildAtHomeItems([
      product('Milk', [{ id: 'milk-1', expirationAt: at(0), location: 'Fridge door' }]),
      product('Rice', [{ id: 'rice-1', expirationAt: at(0) }])
    ])

    expect(rows.find(row => row.publicId === 'milk-1')?.storageLocationName).toBe('Fridge door')
    expect(rows.find(row => row.publicId === 'rice-1')?.storageLocationName).toBeUndefined()
  })

  it('ignores stock with no expiry date at all', () => {
    expect(buildAtHomeItems([product('Salt', [{ id: 'salt-1' }])])).toEqual([])
  })

  it('survives a product with no inventory array', () => {
    expect(buildAtHomeItems([{ publicId: 'p', name: 'Ghost' } as DetailedShoppingLessProduct])).toEqual([])
  })
})

// The grid DTO always carries `inventoryItems`, but a hand-rolled fallback mapping can drop it;
// the row builder must not be the thing that explodes when it does.
type DetailedShoppingLessProduct = DetailedProductInfo

describe('countAwaitingPutAway', () => {
  const loadable = (publicId: string, purchasedAt: string | null): LoadableShoppingListItemInfo => ({
    publicId,
    shoppingListPublicId: 'list-1',
    shoppingListName: 'Aldi',
    isSharedWithFamily: true,
    productPublicId: 'prod-1',
    productName: 'Milk',
    quantity: 1,
    unit: Unit.Piece,
    purchasedAt
  } as LoadableShoppingListItemInfo)

  it('counts only what was actually bought', () => {
    expect(countAwaitingPutAway([
      loadable('a', at(-1)),
      loadable('b', null),
      loadable('c', at(-2))
    ])).toBe(2)
  })

  it('is zero for an empty or missing list', () => {
    expect(countAwaitingPutAway([])).toBe(0)
    expect(countAwaitingPutAway(undefined as unknown as LoadableShoppingListItemInfo[])).toBe(0)
  })
})

describe('buildShoppingRows', () => {
  const list = (publicId: string, name: string, pending: number): ShoppingListInfo => ({
    publicId,
    name,
    isSharedWithFamily: true,
    pendingItemCount: pending
  } as ShoppingListInfo)

  const deadline = (listPublicId: string, start: string): CalendarEventInfo => ({
    publicId: `ev-${listPublicId}-${start}`,
    title: 'Deadline',
    eventType: CalendarEventType.ShoppingListDeadline,
    start,
    end: null,
    detail: null,
    relatedEntityPublicId: listPublicId,
    color: null,
    isAllDay: false
  })

  it('takes the earliest deadline per list and orders lists by it', () => {
    // Captured once: `at()` reads the clock, so comparing against a second call would compare
    // two instants a millisecond apart.
    const soonest = at(0.2)

    const rows = buildShoppingRows(
      [list('aldi', 'Aldi', 9), list('market', 'Market', 3)],
      [
        deadline('market', at(2)),
        deadline('aldi', at(5)),
        deadline('aldi', soonest)
      ]
    )

    expect(rows.map(row => row.listPublicId)).toEqual(['aldi', 'market'])
    expect(rows[0]?.deadlineAt).toBe(soonest)
    expect(rows[0]?.pendingItemCount).toBe(9)
  })

  it('marks a deadline in the past as overdue', () => {
    const rows = buildShoppingRows([list('aldi', 'Aldi', 2)], [deadline('aldi', at(-0.5))])
    expect(rows[0]?.overdue).toBe(true)
  })

  it('drops a deadline whose list is gone', () => {
    expect(buildShoppingRows([], [deadline('deleted', at(1))])).toEqual([])
  })

  it('ignores calendar entries that are not shopping deadlines', () => {
    const expiry: CalendarEventInfo = {
      ...deadline('aldi', at(1)),
      eventType: CalendarEventType.InventoryExpiration
    }

    expect(buildShoppingRows([list('aldi', 'Aldi', 1)], [expiry])).toEqual([])
  })
})

describe('buildTodayAgenda', () => {
  const now = new Date(2026, 8, 21, 9, 0)
  const event = (eventType: CalendarEventType, start: Date): CalendarEventInfo => ({
    publicId: `ev-${eventType}-${start.toISOString()}`,
    title: 'Something',
    eventType,
    start: start.toISOString(),
    end: null,
    detail: null,
    relatedEntityPublicId: null,
    color: null,
    isAllDay: false
  })

  it('keeps appointments and automations, in time order', () => {
    const agenda = buildTodayAgenda([
      event(CalendarEventType.AutomationExecution, new Date(2026, 8, 21, 20, 0)),
      event(CalendarEventType.ExternalCalendar, new Date(2026, 8, 21, 15, 30))
    ], now)

    expect(agenda.map(e => e.eventType)).toEqual([
      CalendarEventType.ExternalCalendar,
      CalendarEventType.AutomationExecution
    ])
  })

  it('leaves out what the other blocks already show', () => {
    const agenda = buildTodayAgenda([
      event(CalendarEventType.InventoryExpiration, new Date(2026, 8, 21, 10, 0)),
      event(CalendarEventType.ShoppingListDeadline, new Date(2026, 8, 21, 18, 0)),
      event(CalendarEventType.DayNote, new Date(2026, 8, 21, 0, 0))
    ], now)

    expect(agenda).toEqual([])
  })

  it('is today only, not the rest of the week', () => {
    const agenda = buildTodayAgenda([
      event(CalendarEventType.ExternalCalendar, new Date(2026, 8, 22, 9, 0))
    ], now)

    expect(agenda).toEqual([])
  })
})

describe('pickHomeContext', () => {
  it('shows nothing at all when neither context has anything', () => {
    expect(pickHomeContext(0, 0, 'atHome')).toEqual({ active: null, showSwitcher: false })
  })

  it('shows the one filled context without a switcher', () => {
    expect(pickHomeContext(3, 0, 'atHome')).toEqual({ active: 'atHome', showSwitcher: false })
    expect(pickHomeContext(0, 2, 'shopping')).toEqual({ active: 'shopping', showSwitcher: false })
  })

  it('shows the filled one even when the reader last picked the empty one', () => {
    // Somebody sitting on the shopping side whose last deadline is met lands on what is left,
    // not on an empty panel with a switcher that has one useful position.
    expect(pickHomeContext(3, 0, 'shopping')).toEqual({ active: 'atHome', showSwitcher: false })
    expect(pickHomeContext(0, 1, 'atHome')).toEqual({ active: 'shopping', showSwitcher: false })
  })

  it('honours the reader choice once both are filled', () => {
    expect(pickHomeContext(3, 2, 'shopping')).toEqual({ active: 'shopping', showSwitcher: true })
    expect(pickHomeContext(3, 2, 'atHome')).toEqual({ active: 'atHome', showSwitcher: true })
  })
})

describe('filterPinnedNotes', () => {
  const now = new Date(2026, 8, 21, 17, 5)
  const note = (date: string, title: string): CalendarNoteInfo => ({
    publicId: `n-${date}-${title}`,
    date,
    title,
    content: null,
    reminderAt: null,
    reminderSent: false,
    createdByPublicId: 'u1',
    createdByName: 'Béla',
    lastEditedByPublicId: null,
    lastEditedByName: null,
    createdAt: date
  })

  it('keeps today and the future, soonest first', () => {
    const pinned = filterPinnedNotes([
      note('2026-09-23', 'Plumber'),
      note('2026-09-20', 'Yesterday'),
      note('2026-09-21', 'Dinner')
    ], now)

    expect(pinned.map(n => n.title)).toEqual(['Dinner', 'Plumber'])
  })

  it('keeps today even late in the evening — a note is pinned to a day, not to an instant', () => {
    const pinned = filterPinnedNotes([note('2026-09-21', 'Dinner')], new Date(2026, 8, 21, 23, 59))
    expect(pinned).toHaveLength(1)
  })

  it('orders two notes on the same day by title', () => {
    const pinned = filterPinnedNotes([
      note('2026-09-21', 'Zebra'),
      note('2026-09-21', 'Apple')
    ], now)

    expect(pinned.map(n => n.title)).toEqual(['Apple', 'Zebra'])
  })
})
