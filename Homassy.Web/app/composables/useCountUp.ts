/**
 * Counts a number up to its target on `requestAnimationFrame`, formatted in the
 * app's locale on every frame.
 *
 * Only the arithmetic and the formatting live here. *When* to start is the
 * caller's decision — StatNumber.vue waits for the element to enter the
 * viewport — because a counter that runs while it is scrolled out of sight has
 * animated for nobody.
 *
 * The animation always starts from wherever the display currently is, so the
 * first run counts up from zero and a later update (a refetch, a socket patch)
 * tweens from the value already on screen rather than snapping back to zero.
 */

/** Long enough to read as counting, short enough not to hold up the page. */
const DEFAULT_DURATION_MS = 1100

/** Decelerating, so the last digits settle instead of slamming into place. */
const easeOut = (t: number) => 1 - (1 - t) ** 3

export interface UseCountUpOptions {
  /** Milliseconds for a full run. */
  duration?: number
}

export function useCountUp(options: UseCountUpOptions = {}) {
  const { duration = DEFAULT_DURATION_MS } = options
  const { locale } = useI18n()

  const current = ref(0)
  let frame: number | null = null

  // One formatter per locale rather than one per frame: `Intl.NumberFormat` is
  // expensive to construct and this runs sixty times a second.
  const formatter = computed(() => new Intl.NumberFormat(locale.value))
  const displayed = computed(() => formatter.value.format(Math.round(current.value)))

  const cancel = () => {
    if (frame === null) return
    cancelAnimationFrame(frame)
    frame = null
  }

  /** Jumps straight to `to`, cancelling any run in progress. */
  const settle = (to: number) => {
    cancel()
    current.value = to
  }

  /** Tweens from the value on screen to `to`. */
  const animate = (to: number) => {
    // No animation frames on the server, and none worth spending on a no-op.
    if (!import.meta.client || to === current.value) return settle(to)

    cancel()
    const from = current.value
    const startedAt = performance.now()

    const step = (now: number) => {
      const progress = Math.min(1, (now - startedAt) / duration)
      if (progress >= 1) {
        settle(to)
        return
      }
      current.value = from + (to - from) * easeOut(progress)
      frame = requestAnimationFrame(step)
    }

    frame = requestAnimationFrame(step)
  }

  onScopeDispose(cancel)

  return { displayed, animate, settle }
}
