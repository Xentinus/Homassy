/**
 * Turns an `?action=` query parameter into a one-shot call on the page that owns it
 * (#118) — the mechanism behind the manifest's app shortcuts, which have to land on a
 * view *with the relevant drawer already open*.
 *
 * ```ts
 * useDeepLinkAction({
 *   add: () => { isAddInventoryOpen.value = true },
 *   scan: () => openScanner()
 * })
 * ```
 *
 * Three things it does that a bare `route.query.action` read in `onMounted` does not:
 *
 * - **It strips the parameter afterwards**, with `router.replace`, so the action does
 *   not fire again when the user navigates back to the page, pulls to refresh, or
 *   reloads. A shortcut is an instruction, not a piece of page state.
 * - **It waits for the route to actually be the page's own.** A cold start through the
 *   auth gate lands on `/auth/login?return_to=…` first, and the target page only
 *   mounts after the redirect back; keying off the mounted page's `onMounted` alone is
 *   fine, but the query has to be re-read at that point rather than captured earlier.
 * - **It runs after the next tick**, so a handler that opens a drawer does so against
 *   a mounted, measured page rather than mid-setup.
 *
 * An unknown action value is ignored (and still stripped): shortcuts live in the
 * manifest of an already-installed app, so an older install can ask for an action a
 * newer build has renamed, and landing on the right page with nothing open is a far
 * better outcome than an error.
 */
import { nextTick, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

export type DeepLinkActionMap = Record<string, () => unknown>

export const useDeepLinkAction = (actions: DeepLinkActionMap) => {
  const route = useRoute()
  const router = useRouter()

  onMounted(async () => {
    const raw = route.query.action
    const action = Array.isArray(raw) ? raw[0] : raw
    if (typeof action !== 'string' || !action) return

    // Strip first. If the handler throws, the parameter is still gone — a shortcut
    // that reliably reopens a failing drawer on every visit is worse than one that
    // fails once.
    const query = { ...route.query }
    delete query.action
    await router.replace({ path: route.path, query, hash: route.hash })

    await nextTick()
    actions[action]?.()
  })
}
