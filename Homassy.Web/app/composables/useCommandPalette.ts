import { ref, computed, readonly } from 'vue'
import type { GlobalSearchResponse } from '~/types/search'

/**
 * The command palette's state (#111): whether it is open, what has been typed, what came
 * back, and this device's recent searches.
 *
 * Module-scoped rather than per-component, because the palette is opened from three places
 * (the header's search button, `Ctrl/Cmd+K`, and the desktop sidebar) and is mounted once,
 * in the authenticated layout.
 */

/** Shortest term the server answers — mirrors `SearchFunctions.MinQueryLength`. */
export const MIN_QUERY_LENGTH = 2

/** How long typing has to stop before a request goes out. */
const DEBOUNCE_MS = 220

/** Recent searches kept per device. Six is about a screenful above the results. */
const MAX_RECENT = 6

const RECENT_STORAGE_KEY = 'homassy_recent_searches'

const isOpen = ref(false)
const query = ref('')
const results = ref<GlobalSearchResponse | null>(null)
const loading = ref(false)
const recent = ref<string[]>([])

let recentLoaded = false
let debounceTimer: ReturnType<typeof setTimeout> | null = null
// Every request carries a sequence number; only the newest one is allowed to write the
// results. Debouncing alone does not prevent a slow early request landing after a fast late
// one and putting stale rows under a newer query.
let requestSeq = 0

const loadRecent = () => {
  if (recentLoaded || !import.meta.client) return
  recentLoaded = true

  try {
    const raw = localStorage.getItem(RECENT_STORAGE_KEY)
    if (!raw) return
    const parsed: unknown = JSON.parse(raw)
    if (Array.isArray(parsed)) {
      recent.value = parsed.filter((entry): entry is string => typeof entry === 'string').slice(0, MAX_RECENT)
    }
  } catch {
    // A private window, cleared site data, or a value some older build wrote in another
    // shape. Recent searches are a convenience — start empty rather than fail to open.
    recent.value = []
  }
}

const persistRecent = () => {
  if (!import.meta.client) return
  try {
    localStorage.setItem(RECENT_STORAGE_KEY, JSON.stringify(recent.value))
  } catch {
    // Storage full or blocked. Nothing to do — the list still works for this session.
  }
}

export const useCommandPalette = () => {
  const { search } = useSearchApi()

  const hasResults = computed(() => (results.value?.totalCount ?? 0) > 0)

  /** True while the term is long enough to search but nothing has come back yet. */
  const isSearching = computed(() => loading.value && query.value.trim().length >= MIN_QUERY_LENGTH)

  const runSearch = async (term: string) => {
    const seq = ++requestSeq

    try {
      const response = await search(term)
      // A newer keystroke has already been sent — its answer is the one that counts.
      if (seq !== requestSeq) return

      results.value = response.success && response.data ? response.data : null
    } catch {
      if (seq !== requestSeq) return
      // A failed keystroke shows the no-results state rather than an error screen: the next
      // keystroke retries by itself, and a palette that goes red mid-typing is worse than one
      // that briefly says it found nothing.
      results.value = null
    } finally {
      if (seq === requestSeq) loading.value = false
    }
  }

  /** Type into the palette. Debounced; a term under the minimum clears the results. */
  const setQuery = (value: string) => {
    query.value = value

    if (debounceTimer) {
      clearTimeout(debounceTimer)
      debounceTimer = null
    }

    const term = value.trim()
    if (term.length < MIN_QUERY_LENGTH) {
      // Bump the sequence so an in-flight request cannot land on the cleared state.
      requestSeq++
      results.value = null
      loading.value = false
      return
    }

    loading.value = true
    debounceTimer = setTimeout(() => runSearch(term), DEBOUNCE_MS)
  }

  const open = () => {
    loadRecent()
    isOpen.value = true
  }

  const close = () => {
    isOpen.value = false
  }

  const toggle = () => {
    if (isOpen.value) close()
    else open()
  }

  /**
   * Remember a term this device searched. Called when a result is chosen, not on every
   * keystroke — a recent list of half-typed prefixes is noise.
   */
  const rememberSearch = (value: string) => {
    const term = value.trim()
    if (term.length < MIN_QUERY_LENGTH) return

    loadRecent()
    recent.value = [term, ...recent.value.filter(entry => entry.toLowerCase() !== term.toLowerCase())]
      .slice(0, MAX_RECENT)
    persistRecent()
  }

  const clearRecent = () => {
    recent.value = []
    persistRecent()
  }

  /** Drop the term and the results, keeping the palette open. */
  const reset = () => {
    setQuery('')
  }

  return {
    isOpen: readonly(isOpen),
    query: readonly(query),
    results: readonly(results),
    recent: readonly(recent),
    hasResults,
    isSearching,
    open,
    close,
    toggle,
    setQuery,
    reset,
    rememberSearch,
    clearRecent
  }
}
