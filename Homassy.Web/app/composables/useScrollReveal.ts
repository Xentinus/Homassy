import { onMounted, onBeforeUnmount, ref, type Ref } from 'vue'

/**
 * Reveals an element the first time it scrolls into view (#123).
 *
 * An `IntersectionObserver` per element, disconnected the moment it has fired: a marketing page
 * should not keep a scroll listener alive for the life of the tab, and a reveal that replays
 * every time you scroll past is a distraction rather than an effect.
 *
 * The element starts hidden and is revealed on mount — immediately when the reader asked for
 * reduced motion or the browser has no `IntersectionObserver`, otherwise when it comes into
 * view. Hidden means transparent and offset, never `display: none`: the section is in the
 * document and in the layout from the first paint, so revealing it moves nothing.
 */
export const useScrollReveal = (
  el: Ref<HTMLElement | null>,
  options: { threshold?: number, rootMargin?: string } = {}
) => {
  const revealed = ref(false)

  let observer: IntersectionObserver | null = null

  onMounted(() => {
    if (!import.meta.client) return

    if (typeof IntersectionObserver === 'undefined'
      || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      revealed.value = true
      return
    }

    observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue
        revealed.value = true
        observer?.disconnect()
        observer = null
      }
    }, {
      threshold: options.threshold ?? 0.15,
      // Fire a little before the element's top edge arrives, so the reveal is finishing rather
      // than starting by the time it is properly on screen.
      rootMargin: options.rootMargin ?? '0px 0px -10% 0px'
    })

    if (el.value) observer.observe(el.value)
    else revealed.value = true
  })

  onBeforeUnmount(() => {
    observer?.disconnect()
    observer = null
  })

  return { revealed }
}
