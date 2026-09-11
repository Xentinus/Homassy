/**
 * The pure half of drag-and-drop reordering (#113): how rows are ordered from their `sortOrder`,
 * what an optimistic reorder writes locally, and how a reorder event folds back in.
 *
 * Kept out of `useReorderableList` so it can be tested without a Vue runtime — the ordering rules
 * are the part with edge cases (ties at zero, rows a filter is hiding, an event that names rows the
 * client has never seen), and the composable around them is plumbing.
 *
 * `sortOrder` is a sparse gapped integer assigned by the server, never an array index — see
 * `Homassy.API.Functions.SparseOrdering`. Two consequences that drive everything here: rows nobody
 * has dragged all hold 0 and must fall through to a secondary order, and the gaps between rows are
 * arbitrary, so nothing may assume they are consecutive.
 */

/** The shape a row needs to take part: a stable id and a manual position. */
export interface ManuallyOrdered {
  publicId: string
  sortOrder: number
}

/** One row's new position, as the reorder endpoints and the `*Reordered` events carry it. */
export interface ManualOrderEntry {
  publicId: string
  sortOrder: number
}

/** Spacing used when a list has no distinct positions to redistribute. */
export const MANUAL_ORDER_GAP = 1000

/**
 * Rows in manual order, with `baseSort` deciding ties.
 *
 * The tie-break is not a nicety: every row of a list that has never been dragged holds 0, so
 * without it switching a list into manual order would show it in whatever order the API happened
 * to return. Returns a new array; the input is left alone.
 */
export const sortByManualOrder = <T extends ManuallyOrdered>(
  items: readonly T[],
  baseSort?: (a: T, b: T) => number
): T[] => [...items].sort((a, b) => a.sortOrder - b.sortOrder || (baseSort ? baseSort(a, b) : 0))

/**
 * The positions to write locally the moment a drag is released, before the server has answered.
 *
 * Where the dragged rows already hold distinct positions, those same positions are handed back out
 * in the requested order. That matters when a filter is active: rows the user cannot see keep their
 * stored positions, and reusing the visible rows' own slots leaves the hidden ones exactly where
 * they were relative to them. An index-based renumber would quietly drag them somewhere else.
 *
 * A list nobody has dragged holds all zeros and so has nothing to redistribute; that case falls
 * back to evenly gapped values, which is also what the server does the first time it is asked.
 *
 * @param items every row the caller owns, in any order
 * @param orderedIds the ids that were dragged, in the order the user wants them
 * @returns id → new sort order, covering only the ids that resolve to a known row
 */
export const planOptimisticOrder = <T extends ManuallyOrdered>(
  items: readonly T[],
  orderedIds: readonly string[]
): Map<string, number> => {
  const byId = new Map(items.map(item => [item.publicId, item]))
  const dragged = orderedIds
    .map(id => byId.get(id))
    .filter((item): item is T => !!item)

  const slots = dragged.map(item => item.sortOrder).sort((a, b) => a - b)
  const slotsAreDistinct = slots.every((value, index) => index === 0 || value > slots[index - 1]!)

  const planned = new Map<string, number>()
  dragged.forEach((item, index) => {
    planned.set(item.publicId, slotsAreDistinct ? slots[index]! : (index + 1) * MANUAL_ORDER_GAP)
  })
  return planned
}

/**
 * The positions to write when a reorder lands from elsewhere — the endpoint's own response, or
 * another member's drag arriving over SignalR.
 *
 * Only the rows that actually moved are carried (the server's ordering is sparse, so a single drag
 * usually names one row), and an entry for a row this client does not have is ignored rather than
 * inserted: the event says where a row goes, not that it exists.
 */
export const applyOrderEntries = <T extends ManuallyOrdered>(
  items: readonly T[],
  entries: readonly ManualOrderEntry[]
): Map<string, number> => {
  const known = new Set(items.map(item => item.publicId))
  const applied = new Map<string, number>()
  for (const entry of entries) {
    if (known.has(entry.publicId)) applied.set(entry.publicId, entry.sortOrder)
  }
  return applied
}

/** True when two id sequences are the same list in the same order. */
export const sameOrder = (a: readonly string[], b: readonly string[]): boolean =>
  a.length === b.length && a.every((id, index) => id === b[index])
