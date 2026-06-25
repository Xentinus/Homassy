import { ref, watchEffect, onScopeDispose } from 'vue'

export interface FabAction {
  /** Visible label — shown in the chooser when there are multiple actions. */
  label: string
  /** Lucide/Heroicons icon name, e.g. 'i-lucide-plus'. Defaults to a plus icon. */
  icon?: string
  /** Optional secondary line shown under the label in the chooser. */
  description?: string
  /** Invoked when the action is selected (or immediately when it is the only action).
   *  Return value is ignored — typed as `unknown` so handlers like `() => navigateTo(...)`
   *  (whose return type is a wide union) assign cleanly. */
  handler: () => unknown
}

// Shared, module-scoped state — the layout's FAB reads it, pages write it.
const actions = ref<FabAction[]>([])
// Visibility is debounced separately from `actions`: showing is instant, but hiding
// is deferred so navigating between two FAB pages (the old page clears `actions`
// before the new one re-registers, especially under an `out-in` page transition)
// doesn't make the "+" flicker out and back in.
const fabVisible = ref(false)
const ownerId = ref(0)
let counter = 0
let hideTimer: ReturnType<typeof setTimeout> | null = null

// Long enough to bridge a page transition / a page whose FAB populates just after
// mount, short enough that genuinely FAB-less pages drop the "+" promptly.
const FAB_HIDE_DELAY = 300

const syncVisibility = () => {
  if (actions.value.length > 0) {
    if (hideTimer) {
      clearTimeout(hideTimer)
      hideTimer = null
    }
    fabVisible.value = true
  } else if (fabVisible.value && !hideTimer) {
    hideTimer = setTimeout(() => {
      fabVisible.value = false
      hideTimer = null
    }, FAB_HIDE_DELAY)
  }
}

/**
 * Read the currently registered FAB actions (chooser content) and the debounced
 * visibility flag (the "+" button and the navbar centre gap). Used by NavFab and
 * the auth layout.
 */
export const useFab = () => {
  return { actions, fabVisible }
}

/**
 * Register the add-actions for the current page. The provider is reactive: it is
 * re-evaluated whenever its dependencies (e.g. the i18n locale) change, and the
 * actions are cleared automatically when the page is unmounted.
 *
 * Behaviour of the resulting FAB:
 * - 0 actions  → the FAB is hidden
 * - 1 action   → tapping the FAB runs it directly, no chooser
 * - 2+ actions → tapping the FAB opens an animated chooser
 */
export const useFabActions = (provider: () => FabAction[]) => {
  const id = ++counter

  watchEffect(() => {
    actions.value = provider()
    ownerId.value = id
    syncVisibility()
  })

  onScopeDispose(() => {
    // Only clear if we are still the owner — avoids wiping the next page's
    // actions when navigation mounts it before this page is disposed.
    if (ownerId.value === id) {
      actions.value = []
      syncVisibility()
    }
  })
}
