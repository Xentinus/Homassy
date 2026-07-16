import { ref, watchEffect, onScopeDispose } from 'vue'
import { useRoute } from 'vue-router'

export interface PageHeaderConfig {
  /** Main title. Leave empty/undefined while loading — the header shows a skeleton. */
  title?: string
  /** Optional secondary line under the title. */
  subtitle?: string
  /** Optional colour dot rendered before the subtitle (e.g. the active shopping list colour). */
  subtitleColor?: string
  /** Optional icon rendered after the subtitle (e.g. `i-lucide-users` for a shared list). */
  subtitleIcon?: string
  /** Leading icon shown before the title. */
  icon?: string
  /** When set, the header shows a back arrow navigating to this route. */
  backTo?: string
  /** When set, the header shows an info popover with this text next to the title. */
  info?: string
  /** Force the skeleton even after the header is committed (async title/subtitle). */
  loading?: boolean
  /** Reserve the subtitle line (so its skeleton shows while the subtitle loads). */
  hasSubtitle?: boolean
}

interface HeaderState extends PageHeaderConfig {
  /** Route path this header was registered for. The layout shows a skeleton while
   *  this differs from the current route (i.e. during navigation, before the
   *  incoming page has registered its own header). */
  path?: string
}

// Shared, module-scoped state — the auth layout's AppHeader reads it, pages write it.
// Mirrors useFabActions.ts.
const header = ref<HeaderState>({})
const ownerId = ref(0)
let counter = 0

/**
 * Read the current page-header state. Used by the auth layout's AppHeader.
 */
export const usePageHeaderState = () => header

/**
 * Register the header (icon + title + subtitle …) for the current page. The
 * provider is reactive: it is re-evaluated whenever its dependencies (the i18n
 * locale, a loaded entity, …) change, and the header is cleared automatically
 * when the page is unmounted.
 *
 * The current route path is stamped onto the state (captured once at setup, so
 * the outgoing page does NOT re-stamp it during navigation). The layout renders
 * a skeleton whenever the stamped path differs from the live route — giving the
 * "skeleton, then load" effect across the `out-in` page transition — and while a
 * field is still empty or `loading` is set.
 */
export const usePageHeader = (provider: () => PageHeaderConfig) => {
  const route = useRoute()
  // Snapshot the path at setup (non-reactive read): the incoming page stamps its
  // own path once; the outgoing page keeps its old path so the skeleton shows.
  const path = route.path
  const id = ++counter

  watchEffect(() => {
    header.value = { ...provider(), path }
    ownerId.value = id
  })

  onScopeDispose(() => {
    // Only clear if we are still the owner — avoids wiping the next page's header
    // when navigation mounts it before this page is disposed.
    if (ownerId.value === id) {
      header.value = {}
    }
  })
}
