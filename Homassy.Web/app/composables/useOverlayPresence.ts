/**
 * Is a drawer or a modal open right now?
 *
 * One question, asked by the floating surfaces that must not paint over a sheet — today the
 * family chat bubble (#145). It is answered by looking at the DOM rather than by a flag every
 * drawer would have to remember to set: there are ~30 drawers and a dozen modals in this app,
 * spread across pages, components and wizards, and a register that one of them forgets to join is
 * a bubble floating over that one sheet. The DOM cannot forget.
 *
 * What counts as open:
 *
 * - `[data-vaul-drawer]` — vaul only renders the drawer element while the sheet is mounted, so
 *   its presence *is* the open state (see `AppDrawer`, which wraps `UDrawer`).
 * - `[role="dialog"][data-state="open"]` / `[role="alertdialog"][data-state="open"]` — Reka UI's
 *   dialog primitive behind `UModal` and `USlideover`, which stamps `data-state` itself.
 *
 * Module-scoped: one observer for the whole app however many callers there are, started on the
 * first subscriber and stopped when the last one goes away. Client-only — during SSR there is no
 * document and the answer is a flat `false`, which is also the correct pre-hydration render.
 */
import { onScopeDispose, ref, type Ref } from 'vue'

const OPEN_OVERLAY_SELECTOR =
  '[data-vaul-drawer],[role="dialog"][data-state="open"],[role="alertdialog"][data-state="open"]'

const overlayOpen = ref(false)

let observer: MutationObserver | null = null
let subscribers = 0

const measure = (): void => {
  overlayOpen.value = document.querySelector(OPEN_OVERLAY_SELECTOR) !== null
}

const start = (): void => {
  if (observer) return

  measure()
  observer = new MutationObserver(measure)
  observer.observe(document.body, {
    childList: true,
    subtree: true,
    // `data-state` flips in place on a dialog that is already mounted, so watching the tree
    // shape alone would miss a modal closing.
    attributes: true,
    attributeFilter: ['data-state']
  })
}

const stop = (): void => {
  observer?.disconnect()
  observer = null
  overlayOpen.value = false
}

export const useOverlayPresence = (): { overlayOpen: Ref<boolean> } => {
  if (import.meta.client) {
    subscribers++
    start()

    onScopeDispose(() => {
      subscribers--
      if (subscribers <= 0) {
        subscribers = 0
        stop()
      }
    })
  }

  return { overlayOpen }
}
