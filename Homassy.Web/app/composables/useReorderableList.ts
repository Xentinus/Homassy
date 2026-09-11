/**
 * Turns a list of rows the page already owns into a drag-reorderable one (#113).
 *
 * `useDragReorder` handles the gesture; this handles everything around it that every reorderable
 * list needs the same way: which order to render, the optimistic write, the revert when the write
 * fails, and folding a `*Reordered` realtime event back into the rows.
 *
 * The order is derived from each row's own `sortOrder`, never from array position — the rows arrive
 * from the API, from a socket event and from local upserts, and array position survives none of
 * those. `sortOrder` is a sparse gapped integer (see `Homassy.API.Functions.SparseOrdering`), so
 * rows that have never been dragged all sit at 0 and fall through to `baseSort`.
 */
import type { MaybeRefOrGetter, Ref } from 'vue'
import type { ReorderedEntry } from '~/types/masterData'
import type { ManuallyOrdered } from '~/utils/manualOrder'
import { applyOrderEntries, planOptimisticOrder, sortByManualOrder } from '~/utils/manualOrder'

/** The shape a row must have to take part: a stable id and a manual position. */
export type ReorderableRow = ManuallyOrdered

export interface ReorderableListOptions<T extends ReorderableRow> {
  /** The rows, owned by the caller. Mutated in place by the optimistic write and the revert. */
  items: Ref<T[]>
  /** The element the rows are rendered in; each row carries `data-reorder-key`. */
  container: Ref<HTMLElement | null>
  /** Order for rows that tie on `sortOrder` — which, before anyone drags, is all of them. */
  baseSort?: (a: T, b: T) => number
  /**
   * Which rows are on screen. Rows filtered out still take part in the optimistic write's
   * bookkeeping, but are not draggable and are not sent, so a drag under an active filter reorders
   * exactly what the user can see.
   */
  filter?: (item: T) => boolean
  /** Sends the new order. Resolves falsy `success` (or rejects) to trigger the revert. */
  commit: (orderedIds: string[]) => Promise<{ success: boolean } | undefined>
  /** Drag and keyboard moves are ignored while this is false. */
  enabled?: MaybeRefOrGetter<boolean>
  /** Called after the revert, to tell the user the order did not stick. */
  onFailed?: (error: unknown) => void
}

export const useReorderableList = <T extends ReorderableRow>(options: ReorderableListOptions<T>) => {
  const drag = useDragReorder({
    container: options.container,
    disabled: () => toValue(options.enabled) === false,
    keys: () => orderedItems.value.map(item => item.publicId),
    onReorder: orderedIds => applyOptimisticOrder(orderedIds)
  })

  /**
   * The rows in the order they should be rendered: the live preview while a card is in the air,
   * otherwise manual position with the caller's own ordering as the tie-break.
   */
  const visibleItems = computed<T[]>(() =>
    options.filter ? options.items.value.filter(options.filter) : options.items.value)

  const orderedItems = computed<T[]>(() => {
    const preview = drag.previewKeys.value
    if (preview) {
      const byId = new Map(visibleItems.value.map(item => [item.publicId, item]))
      // Rows that arrived mid-drag (a socket event, a filter change) are not in the preview; they go
      // at the end rather than vanishing for the length of the gesture.
      const shown = preview.map(id => byId.get(id)).filter((item): item is T => !!item)
      const shownIds = new Set(preview)
      return [...shown, ...visibleItems.value.filter(item => !shownIds.has(item.publicId))]
    }

    return sortByManualOrder(visibleItems.value, options.baseSort)
  })

  /**
   * Writes provisional positions locally, sends the order, and puts the old positions back if the
   * write fails. The provisional values only have to sort the way the user just dragged — the
   * server's real values arrive with the response and over the socket a moment later.
   *
   * Where the dragged rows already hold distinct positions, those same positions are handed back out
   * in the new order. That keeps rows the filter is hiding exactly where they were relative to the
   * visible ones, which an index-based renumber would not. A list nobody has dragged yet holds all
   * zeros and has no positions to redistribute, so it falls back to evenly gapped values.
   */
  const applyOptimisticOrder = async (orderedIds: string[]) => {
    const previous = new Map(options.items.value.map(item => [item.publicId, item.sortOrder]))

    const planned = planOptimisticOrder(options.items.value, orderedIds)
    for (const item of options.items.value) {
      const sortOrder = planned.get(item.publicId)
      if (sortOrder !== undefined) item.sortOrder = sortOrder
    }

    try {
      const response = await options.commit(orderedIds)
      if (response && response.success === false) throw new Error('Reorder rejected')
    } catch (error) {
      for (const item of options.items.value) {
        const sortOrder = previous.get(item.publicId)
        if (sortOrder !== undefined) item.sortOrder = sortOrder
      }
      options.onFailed?.(error)
    }
  }

  /**
   * Folds a `*Reordered` event (or a reorder response) into the rows. Only the rows named in the
   * event moved, so rows not mentioned keep the position they have — which is what makes the
   * server's "one row per drag" payload enough to reproduce the order.
   */
  const applyReorderedEntries = (entries: ReorderedEntry[] | null | undefined) => {
    if (!entries?.length) return
    // A drag in progress owns the on-screen order; a concurrent event from another member would
    // otherwise yank the list around under the finger. The drop re-reads sortOrder, and the
    // server's order wins from there.
    if (drag.isDragging.value) return

    const applied = applyOrderEntries(options.items.value, entries)
    for (const item of options.items.value) {
      const sortOrder = applied.get(item.publicId)
      if (sortOrder !== undefined) item.sortOrder = sortOrder
    }
  }

  return {
    orderedItems,
    isDragging: drag.isDragging,
    draggingKey: drag.draggingKey,
    startDrag: drag.startDrag,
    moveByKeyboard: drag.moveByKeyboard,
    applyReorderedEntries
  }
}
