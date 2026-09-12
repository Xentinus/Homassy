import { ref, computed, onMounted, onUnmounted } from 'vue'

/**
 * Where the app stops being a phone UI: Tailwind's `lg`, the same 1024px the layout's
 * `lg:` classes switch at. One number, so the CSS breakpoint and the JS one cannot disagree.
 */
export const DESKTOP_BREAKPOINT_PX = 1024

// Module-scoped, so every caller shares one listener instead of one per component.
const matches = ref(false)
// False until the first client measurement — SSR has no viewport, and an SSR'd `true`
// would hydrate into a mismatch on every phone.
const measured = ref(false)

let listeners = 0
let query: MediaQueryList | null = null

const onChange = (event: MediaQueryListEvent | MediaQueryList) => {
  matches.value = event.matches
  measured.value = true
}

/**
 * Reactive "is this a desktop-width window".
 *
 * `isDesktop` is false during SSR and until the first client measurement, so the mobile
 * layout is what hydrates and the desktop chrome switches on immediately afterwards. Backed
 * by a `matchMedia` listener rather than `resize`: it fires once at the crossing instead of
 * on every pixel of a drag.
 *
 * This is for behaviour that cannot be expressed in CSS — which component a drawer renders
 * as, whether a keyboard shortcut is offered. Anything that is only layout (column counts,
 * padding, what is hidden) stays a `lg:` class.
 */
export const useBreakpoint = () => {
  onMounted(() => {
    if (!import.meta.client) return

    listeners++
    if (!query) {
      query = window.matchMedia(`(min-width: ${DESKTOP_BREAKPOINT_PX}px)`)
      query.addEventListener('change', onChange)
    }
    onChange(query)
  })

  onUnmounted(() => {
    if (!import.meta.client) return

    listeners = Math.max(0, listeners - 1)
    if (listeners === 0 && query) {
      query.removeEventListener('change', onChange)
      query = null
    }
  })

  return {
    isDesktop: computed(() => matches.value),
    /** True once the client has measured the viewport — gate hydration-sensitive chrome on it. */
    measured: computed(() => measured.value)
  }
}
