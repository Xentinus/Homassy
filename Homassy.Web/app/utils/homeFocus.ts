/**
 * What the home screen puts in front of you, derived from data the app already had.
 *
 * The screen asks two questions — *what waits for me at home* and *what waits for me in a shop* —
 * and both answers are assembled here rather than in the page, for the same reason
 * `activityTimeline.ts` and `awayGap.ts` exist: this is the part with the edge cases (an item with
 * no date, a deadline for a list that was deleted, a note pinned to a day that has passed), and it
 * is testable as plain functions without booting Nuxt.
 *
 * Nothing here formats anything for a human. Dates come back as raw ISO strings and levels as
 * ramp levels; the components turn those into "holnap lejár" in the viewer's locale.
 */
import { expirationLevel, type ExpirationLevel } from '~/composables/useExpirationStatus'
import { CalendarEventType, type CalendarEventInfo, type CalendarNoteInfo } from '~/types/calendar'
import type { Unit } from '~/types/enums'
import type { DetailedProductInfo } from '~/types/product'
import type { LoadableShoppingListItemInfo, ShoppingListInfo } from '~/types/shoppingList'

/** One row of "Ha hazaérsz": a piece of stock that wants a decision today. */
export interface AtHomeItem {
  /** The inventory item's public id — the entity every action on this row addresses. */
  publicId: string
  productPublicId: string
  productName: string
  brand?: string
  /** Where it is, when the item has a storage location. */
  storageLocationName?: string
  expirationAt: string
  level: ExpirationLevel
  quantity: number
  unit: Unit
}

/** One row of "Ha boltba mégy": a list with a deadline close enough to act on. */
export interface ShoppingRow {
  listPublicId: string
  name: string
  color?: string
  pendingItemCount: number
  /** The earliest deadline among the list's items, as an ISO instant. */
  deadlineAt: string
  overdue: boolean
}

/**
 * Stock this close to going off is what the screen leads with. Deliberately tighter than the
 * 14-day `soon` ramp: the two-week window is the right net for the inventory grid's amber tint,
 * and far too wide for a list that claims everything on it needs doing before bedtime.
 */
export const AT_HOME_LEVELS: readonly ExpirationLevel[] = ['expired', 'critical']

/** How far ahead a shopping deadline is worth mentioning before you have even left the house. */
export const SHOPPING_HORIZON_DAYS = 14

/** Which of the home screen's two contexts a block is about. */
export type HomeContextKey = 'atHome' | 'shopping'

/** What the context block shows: nothing, one context, or both behind a switcher. */
export interface HomeContextChoice {
  /** The context to show, or null when neither has anything in it. */
  active: HomeContextKey | null
  /** True only when both have something — one filled context needs no switcher. */
  showSwitcher: boolean
}

/**
 * Picks what the context block shows.
 *
 * Three outcomes, and the two degenerate ones are the point: a switcher with one useful position
 * is a control that does nothing, and two panels each announcing that nothing is waiting are worth
 * less than the space they cost. So one filled context is shown on its own, and none at all means
 * the block is not there.
 *
 * `preferred` is what the reader last picked. It is honoured only while it still has something in
 * it — somebody sitting on the shopping side whose last deadline is met should land on whatever is
 * left rather than on an empty panel.
 */
export const pickHomeContext = (
  atHomeCount: number,
  shoppingCount: number,
  preferred: HomeContextKey
): HomeContextChoice => {
  const filled: HomeContextKey[] = []
  if (atHomeCount > 0) filled.push('atHome')
  if (shoppingCount > 0) filled.push('shopping')

  if (filled.length === 0) {
    return { active: null, showSwitcher: false }
  }

  return {
    active: filled.includes(preferred) ? preferred : filled[0]!,
    showSwitcher: filled.length > 1
  }
}

/** Local calendar day key (`YYYY-MM-DD`) — the same shape `CalendarNoteInfo.date` uses. */
export const dayKey = (value: Date | string): string => {
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

/**
 * Every inventory item that has expired or is about to, most urgent first.
 *
 * Flattened across products because the row is about the *item* — one carton of milk with its own
 * date and its own shelf — not about the product it belongs to. Two cartons bought a week apart
 * are two decisions.
 */
export const buildAtHomeItems = (products: DetailedProductInfo[]): AtHomeItem[] => {
  const rows: AtHomeItem[] = []

  for (const product of products ?? []) {
    for (const item of product.inventoryItems ?? []) {
      if (!item.expirationAt) continue

      const level = expirationLevel(item.expirationAt)
      if (!AT_HOME_LEVELS.includes(level)) continue

      rows.push({
        publicId: item.publicId,
        productPublicId: product.publicId,
        productName: product.name,
        brand: product.brand,
        storageLocationName: item.storageLocation?.name,
        expirationAt: item.expirationAt,
        level,
        quantity: item.currentQuantity,
        unit: item.unit
      })
    }
  }

  // Soonest first, which puts the already-expired above the merely urgent without a second
  // comparison; same-day items fall back to the name so the order is stable between loads.
  return rows.sort((a, b) => {
    const diff = new Date(a.expirationAt).getTime() - new Date(b.expirationAt).getTime()
    return diff !== 0 ? diff : a.productName.localeCompare(b.productName)
  })
}

/**
 * How many things were bought but never put away — shopping-list items already turned into a
 * purchase yet still not loaded into stock (`InventoryLoadedAt == null`, which is what the
 * loadable-inventory endpoint returns).
 *
 * The endpoint also returns items that are merely *on* a list and not bought at all, which are
 * not waiting for anybody; only the purchased ones are a chore with a shelf at the end of it.
 */
export const countAwaitingPutAway = (loadable: LoadableShoppingListItemInfo[]): number =>
  (loadable ?? []).filter(item => !!item.purchasedAt).length

/**
 * The shopping lists worth a trip, earliest deadline first.
 *
 * Deadlines are read off the calendar feed rather than by opening every list: the feed already
 * aggregates `deadlineAt`/`dueAt` across items into one event per deadline, so one call answers
 * for every list at once. The list's own record supplies the name, colour and outstanding count —
 * a deadline whose list has since been deleted has nothing to show and is dropped.
 */
export const buildShoppingRows = (
  lists: ShoppingListInfo[],
  events: CalendarEventInfo[],
  now: Date = new Date()
): ShoppingRow[] => {
  const byId = new Map((lists ?? []).map(list => [list.publicId, list]))
  const earliest = new Map<string, string>()

  for (const event of events ?? []) {
    if (event.eventType !== CalendarEventType.ShoppingListDeadline) continue
    if (!event.relatedEntityPublicId || !event.start) continue

    const current = earliest.get(event.relatedEntityPublicId)
    if (!current || new Date(event.start).getTime() < new Date(current).getTime()) {
      earliest.set(event.relatedEntityPublicId, event.start)
    }
  }

  const rows: ShoppingRow[] = []

  for (const [listPublicId, deadlineAt] of earliest) {
    const list = byId.get(listPublicId)
    if (!list) continue

    rows.push({
      listPublicId,
      name: list.name,
      color: list.color,
      pendingItemCount: list.pendingItemCount,
      deadlineAt,
      overdue: new Date(deadlineAt).getTime() < now.getTime()
    })
  }

  return rows.sort((a, b) => new Date(a.deadlineAt).getTime() - new Date(b.deadlineAt).getTime())
}

/**
 * Today's agenda: the events that are genuinely *appointments*, in time order.
 *
 * Expirations and shopping deadlines are left out on purpose — they are the two blocks above this
 * one, and a screen that lists the same milk twice teaches people to stop reading it. Day notes
 * are left out for the same reason: they are the pinned-notes block.
 */
export const AGENDA_EVENT_TYPES: readonly CalendarEventType[] = [
  CalendarEventType.ExternalCalendar,
  CalendarEventType.AutomationExecution
]

export const buildTodayAgenda = (events: CalendarEventInfo[], now: Date = new Date()): CalendarEventInfo[] => {
  const today = dayKey(now)

  return (events ?? [])
    .filter(event => AGENDA_EVENT_TYPES.includes(event.eventType) && dayKey(event.start) === today)
    .sort((a, b) => new Date(a.start).getTime() - new Date(b.start).getTime())
}

/**
 * The notes worth pinning to the wall: today's and everything still ahead, soonest first.
 *
 * Yesterday's note is not a reminder, it is history — and a wall that never clears itself is one
 * nobody looks at. The comparison is on the note's own `YYYY-MM-DD` day, never on an instant:
 * a note is pinned to a calendar day, not to a moment in it.
 */
export const filterPinnedNotes = (notes: CalendarNoteInfo[], now: Date = new Date()): CalendarNoteInfo[] => {
  const today = dayKey(now)

  return (notes ?? [])
    .filter(note => !!note.date && note.date >= today)
    .sort((a, b) => a.date.localeCompare(b.date) || a.title.localeCompare(b.title))
}
