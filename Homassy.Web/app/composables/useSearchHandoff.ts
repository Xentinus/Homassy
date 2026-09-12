import { nextTick, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

/**
 * Receives what the command palette (#111) hands a list page: a term to pre-fill its filter
 * with (`?search=`), or one entity to open (`?select=`).
 *
 * The same shape as `useDeepLinkAction`, and for the same reasons — the parameter is stripped
 * with `router.replace` before the handler runs, so a back-navigation or a reload does not
 * re-apply a filter the reader has since cleared, and the handler runs after a tick so it acts
 * on a mounted page.
 *
 * ```ts
 * useSearchHandoff({
 *   search: term => { searchQuery.value = term },
 *   select: publicId => { openOverview(publicId) },
 *   ready: () => hasLoaded.value
 * })
 * ```
 *
 * `ready` exists because opening something is not the same as filtering for it: a page's rows
 * arrive from a fetch, and `select` on an empty list would resolve to nothing and silently do
 * nothing at all. When it is given, the selection waits for it; `search` never does, because a
 * filter applies just as well to a list that has not arrived yet.
 *
 * An unknown id is the page's own problem: a list deleted between the search and the tap should
 * leave the page on its default selection rather than in an error state.
 */
export interface SearchHandoffHandlers {
  /** Called with a non-empty search term from `?search=`. */
  search?: (term: string) => void
  /** Called with the public id from `?select=`, once `ready` (if given) is true. */
  select?: (publicId: string) => void
  /** Optional gate: the selection is held until this reads true, and fires once. */
  ready?: () => boolean
}

const firstValue = (raw: unknown): string | undefined => {
  const value = Array.isArray(raw) ? raw[0] : raw
  return typeof value === 'string' && value.length > 0 ? value : undefined
}

export const useSearchHandoff = (handlers: SearchHandoffHandlers) => {
  const route = useRoute()
  const router = useRouter()

  onMounted(async () => {
    const term = firstValue(route.query.search)
    const select = firstValue(route.query.select)
    if (!term && !select) return

    const query = { ...route.query }
    delete query.search
    delete query.select
    await router.replace({ path: route.path, query, hash: route.hash })

    await nextTick()
    if (term) handlers.search?.(term)
    if (!select || !handlers.select) return

    // Checked before the watcher rather than through `immediate: true`: an immediate callback
    // runs before `watch` has returned its stop handle, so the handle would not exist yet.
    if (!handlers.ready || handlers.ready()) {
      handlers.select(select)
      return
    }

    const stop = watch(handlers.ready, (ready) => {
      if (!ready) return
      stop()
      handlers.select?.(select)
    })
  })
}
