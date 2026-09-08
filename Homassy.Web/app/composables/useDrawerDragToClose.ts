import type { MaybeRefOrGetter, Ref } from 'vue'

interface DrawerDragToCloseOptions {
  /** Called once the drawer should close (drag committed past the threshold). */
  onClose: () => void
  /** When true the gesture is ignored (e.g. while the wizard is submitting). */
  disabled?: MaybeRefOrGetter<boolean>
  /** Commit threshold as a ratio of the content height (default 0.25). */
  thresholdRatio?: number
  /** Minimum commit threshold in pixels (default 120). */
  minThreshold?: number
  /**
   * Resting heights as a fraction of the sheet's own height, ascending, e.g.
   * `[0.5, 1]`. The sheet always *opens* at the last (tallest) point; the lower
   * ones are places a drag can settle instead of closing outright. Omit for the
   * plain binary sheet (full height or gone).
   *
   * Only for footer-less sheets: a footer is pinned to the bottom of the
   * content, so below the top snap it is dragged off-screen with it.
   */
  snapPoints?: MaybeRefOrGetter<number[] | undefined>
}

const AXIS_LOCK_SLOP = 6
// vaul's own open/close easing (its internal `O.EASE`). Reusing it keeps our
// manual commit indistinguishable from the library's native slideToBottom.
const DRAWER_EASE = 'cubic-bezier(0.32, 0.72, 0, 1)'
const RELEASE_DURATION = 350
const SNAP_DURATION = 280
// px/ms at which a release counts as a flick rather than a placement: the sheet
// then moves one snap in the direction thrown, however far the finger actually
// got. Below it, the sheet simply settles wherever it was left.
const FLICK_VELOCITY = 0.5
// …but only once the sheet has actually been moved. Without this a twitchy tap
// on the header reads as a very fast, very short flick and dismisses the sheet.
const FLICK_MIN_DISTANCE = 24
// Samples older than this are stale — a pause mid-drag must read as v ≈ 0.
const VELOCITY_WINDOW_MS = 120
// Peak backdrop blur at full height, ramped down with the drag.
const MAX_BACKDROP_BLUR_PX = 8

const prefersReducedMotion = (): boolean =>
  import.meta.client && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Drag gestures for a bottom `UDrawer` whose native dismiss is disabled
 * (`dismissible: false`, which also keeps outside-tap / Esc from closing it).
 *
 * Two modes:
 *
 * - **Plain (no `snapPoints`)** — dragging the drawer *header* down translates
 *   the content; releasing past the threshold slides it the rest of the way out
 *   and calls `onClose` (the parent then flips `open` and vaul's native
 *   slideToBottom continues seamlessly), otherwise it snaps back.
 * - **Snapping (`snapPoints`)** — the sheet also settles at the intermediate
 *   heights, chosen from the *projected* release position so velocity counts and
 *   not just displacement. Below the top snap the body stops scrolling and a
 *   vertical drag anywhere in the sheet moves it instead (the usual sheet
 *   gesture arbitration); at the top snap the body scrolls again and only the
 *   header drags.
 *
 * The backdrop's opacity and blur follow the drag rather than toggling, the
 * handle swells at the point where releasing would change the snap, and each
 * snap transition ticks the shared haptic vocabulary. Under
 * `prefers-reduced-motion` the settle is instant and the blur ramp is skipped.
 *
 * The content lives in a teleported portal that mounts/unmounts on open/close,
 * so listeners are (re)bound whenever `headerEl` changes rather than only on
 * mount.
 */
export const useDrawerDragToClose = (
  headerEl: Ref<HTMLElement | null>,
  options: DrawerDragToCloseOptions
) => {
  const thresholdRatio = options.thresholdRatio ?? 0.25
  const minThreshold = options.minThreshold ?? 120
  const haptics = useHaptics()

  let contentEl: HTMLElement | null = null
  let overlayEl: HTMLElement | null = null
  let handleEl: HTMLElement | null = null
  let bodyEl: HTMLElement | null = null

  let activePointerId: number | null = null
  let startX = 0
  let startY = 0
  let axis: 'vertical' | 'ignore' | null = null
  let engaged = false
  let currentOffset = 0
  // Where the sheet rests between gestures — 0 at the top snap, larger further down.
  let restOffset = 0
  let restIndex = -1
  // The snap the sheet would land on if released right now; -1 means "close".
  let previewIndex = -1
  let velocitySamples: { y: number, t: number }[] = []

  const contentHeight = (): number => contentEl?.getBoundingClientRect().height ?? 0

  const threshold = (): number => Math.max(contentHeight() * thresholdRatio, minThreshold)

  /**
   * Snap offsets in pixels, ascending (top snap first, i.e. offset 0 first).
   * A sheet without snap points has a single one: fully open.
   */
  const snapOffsets = (): number[] => {
    const points = toValue(options.snapPoints)
    const height = contentHeight()
    if (!points?.length || height === 0) return [0]
    return [...points]
      .filter(p => p > 0 && p <= 1)
      .sort((a, b) => b - a)
      .map(p => height * (1 - p))
  }

  /** Index of the snap nearest to `offset`, ignoring whether it is low enough to close. */
  const nearestIndex = (offset: number): number => {
    let best = 0
    let bestDistance = Number.POSITIVE_INFINITY
    snapOffsets().forEach((snap, index) => {
      const distance = Math.abs(offset - snap)
      if (distance < bestDistance) {
        bestDistance = distance
        best = index
      }
    })
    return best
  }

  /** Index of the snap nearest to `offset`, or -1 when it is far enough below the lowest to close. */
  const targetIndexFor = (offset: number): number => {
    const offsets = snapOffsets()
    const lowest = offsets[offsets.length - 1]!
    if (offset > lowest + threshold()) return -1
    return nearestIndex(offset)
  }

  /** px/ms over the tail of the drag; positive means moving down. */
  const velocity = (): number => {
    const now = performance.now()
    const recent = velocitySamples.filter(sample => now - sample.t <= VELOCITY_WINDOW_MS)
    const first = recent[0]
    const last = recent[recent.length - 1]
    if (!first || !last || last.t === first.t) return 0
    return (last.y - first.y) / (last.t - first.t)
  }

  /**
   * Backdrop follows the sheet: fully opaque (and blurred) at the top snap,
   * fading to nothing as the sheet leaves. Cleared at the top snap so vaul's own
   * open/close animation governs the overlay again.
   */
  const paintOverlay = (offset: number, atTopSnap: boolean) => {
    if (!overlayEl) return
    if (atTopSnap) {
      overlayEl.style.opacity = ''
      overlayEl.style.removeProperty('backdrop-filter')
      overlayEl.style.removeProperty('-webkit-backdrop-filter')
      return
    }
    const height = contentHeight()
    const progress = height === 0 ? 1 : Math.max(0, Math.min(1, 1 - offset / height))
    overlayEl.style.opacity = String(progress)
    if (prefersReducedMotion()) return
    const blur = `blur(${(progress * MAX_BACKDROP_BLUR_PX).toFixed(2)}px)`
    overlayEl.style.setProperty('backdrop-filter', blur)
    overlayEl.style.setProperty('-webkit-backdrop-filter', blur)
  }

  /** The handle swells and cants over while releasing would move the sheet somewhere new. */
  const paintHandle = (armed: boolean) => {
    if (!handleEl) return
    handleEl.style.transition = prefersReducedMotion() ? '' : 'transform 180ms ease-out'
    handleEl.style.transform = armed ? 'scale(1.3, 1.6) rotate(3deg)' : ''
  }

  /**
   * Below the top snap the body must not scroll — a vertical drag there moves the
   * sheet instead. At the top snap it scrolls as usual and only the header drags.
   */
  const applyScrollLock = (atTopSnap: boolean) => {
    if (!bodyEl) return
    bodyEl.style.overflowY = atTopSnap ? '' : 'hidden'
    bodyEl.style.touchAction = atTopSnap ? '' : 'none'
  }

  const resetGesture = () => {
    activePointerId = null
    axis = null
    engaged = false
    previewIndex = -1
    velocitySamples = []
    paintHandle(false)
  }

  /** Park the sheet at `index` (or slide it out and close when -1). */
  const settleTo = (index: number) => {
    const el = contentEl
    if (!el) return

    const reduced = prefersReducedMotion()
    const duration = index < 0 ? RELEASE_DURATION : SNAP_DURATION
    // The backdrop tracked the finger exactly; on release it has to travel with
    // the sheet rather than jumping to its new value.
    if (overlayEl) {
      overlayEl.style.transition = reduced
        ? ''
        : `opacity ${duration}ms ${DRAWER_EASE}, backdrop-filter ${duration}ms ${DRAWER_EASE}`
    }

    if (index < 0) {
      el.style.transition = reduced ? '' : `transform ${RELEASE_DURATION}ms ${DRAWER_EASE}`
      // Continue sliding out; the parent flips `open`, and vaul's slideToBottom
      // takes over from this offset with the same easing (no visible jump).
      el.style.transform = 'translate3d(0, 100%, 0)'
      paintOverlay(contentHeight(), false)
      options.onClose()
      return
    }

    const offsets = snapOffsets()
    const offset = offsets[index] ?? 0
    const atTopSnap = index === 0

    restIndex = index
    restOffset = offset
    applyScrollLock(atTopSnap)

    el.style.transition = reduced ? '' : `transform ${SNAP_DURATION}ms ${DRAWER_EASE}`

    if (atTopSnap) {
      // Snap back to fully open, then drop the inline styles so vaul's base
      // transform rule governs the element again.
      el.style.transform = 'translate3d(0, 0, 0)'
      paintOverlay(0, true)
      const onEnd = (event: TransitionEvent) => {
        if (event.propertyName !== 'transform') return
        el.removeEventListener('transitionend', onEnd)
        el.style.transform = ''
        el.style.transition = ''
      }
      if (reduced) {
        el.style.transform = ''
        el.style.transition = ''
      } else {
        el.addEventListener('transitionend', onEnd)
      }
      return
    }

    el.style.transform = `translate3d(0, ${offset}px, 0)`
    paintOverlay(offset, false)
  }

  /**
   * Where a release lands. On a snapping sheet a deliberate flick moves it one
   * snap in the direction it was thrown — past the lowest snap that means
   * dismiss — so a short fast throw is not judged on how far the finger
   * travelled. Anything gentler is a placement: the sheet settles on whichever
   * snap it was left nearest, or closes if it was dragged well below the lowest.
   *
   * A plain sheet has only one resting place, so velocity would only ever be an
   * extra way to dismiss it. It keeps the displacement rule it has always had —
   * every drawer in the app uses this, and a flick-to-close would be a surprise
   * on all of them.
   */
  const releaseTarget = (): number => {
    const offsets = snapOffsets()
    const positional = targetIndexFor(currentOffset)

    // Dragged clean past the lowest snap: it closes however it was let go —
    // velocity cannot pull a sheet back that is already most of the way out.
    if (positional === -1) return -1

    const speed = velocity()
    const moved = Math.abs(currentOffset - restOffset)

    if (offsets.length > 1 && Math.abs(speed) >= FLICK_VELOCITY && moved >= FLICK_MIN_DISTANCE) {
      // One snap per flick, counted from where the gesture began — so a hard
      // throw down from full lands on the half snap and takes a second throw to
      // dismiss, rather than depending on exactly where the finger stopped.
      const next = Math.max(0, restIndex) + (speed > 0 ? 1 : -1)
      if (next >= offsets.length) return -1
      return Math.max(0, next)
    }

    return positional
  }

  const settle = (cancelled: boolean) => {
    if (!engaged || !contentEl) {
      resetGesture()
      return
    }

    const target = cancelled ? restIndex : releaseTarget()

    // The drag already ticked every detent it crossed; a flick can still land
    // somewhere the finger never reached, so tick for that one too.
    if (target !== previewIndex) haptics.select()

    settleTo(target)
    resetGesture()
  }

  /** Whether a pointerdown at `target` may start a sheet drag. */
  const canDragFrom = (target: HTMLElement | null): boolean => {
    // Let interactive controls (the close button, etc.) keep their own pointer
    // handling — don't start a drag on top of them.
    if (target?.closest('button, a, input, select, textarea, [role="button"]')) return false
    if (target?.closest('[data-slot="header"]')) return true
    // Anywhere else in the sheet only drags once the body has stopped scrolling.
    return restIndex > 0
  }

  const onPointerDown = (event: PointerEvent) => {
    if (toValue(options.disabled)) return
    // Mouse is limited to the primary button; touch/pen always allowed.
    if (event.pointerType === 'mouse' && event.button !== 0) return
    if (activePointerId !== null) return
    if (!canDragFrom(event.target as HTMLElement | null)) return

    activePointerId = event.pointerId
    startX = event.clientX
    startY = event.clientY
    axis = null
    engaged = false
    currentOffset = restOffset
    previewIndex = restIndex
    velocitySamples = [{ y: event.clientY, t: performance.now() }]
  }

  const onPointerMove = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId || !contentEl) return
    if (axis === 'ignore') return

    const dx = event.clientX - startX
    const dy = event.clientY - startY

    if (axis === null) {
      if (Math.abs(dx) < AXIS_LOCK_SLOP && Math.abs(dy) < AXIS_LOCK_SLOP) return
      // A vertically-dominant drag moves the sheet. At the top snap only a
      // downward one does (there is nowhere above to go); from a lower snap an
      // upward drag pulls it back up.
      const verticallyDominant = Math.abs(dy) >= Math.abs(dx)
      if (verticallyDominant && (dy > 0 || restIndex > 0)) {
        axis = 'vertical'
        engaged = true
        try {
          contentEl.setPointerCapture(event.pointerId)
        } catch {
          // setPointerCapture can throw if the pointer is no longer active
          // (e.g. Safari edge cases) — the gesture still works without capture.
        }
        contentEl.style.transition = 'none'
        // Both follow the finger 1:1 for the duration of the drag; settleTo puts
        // the transitions back for the release.
        if (overlayEl) overlayEl.style.transition = 'none'
      } else {
        axis = 'ignore'
        return
      }
    }

    velocitySamples.push({ y: event.clientY, t: performance.now() })
    if (velocitySamples.length > 8) velocitySamples.shift()

    // Never above the top snap: there is no sheet left to reveal up there.
    currentOffset = Math.max(0, restOffset + dy)
    contentEl.style.transform = `translate3d(0, ${currentOffset}px, 0)`
    paintOverlay(currentOffset, false)

    const nextPreview = targetIndexFor(currentOffset)
    if (nextPreview !== previewIndex) {
      previewIndex = nextPreview
      haptics.select()
      paintHandle(previewIndex !== restIndex)
    }
  }

  const releaseCapture = (pointerId: number) => {
    try {
      contentEl?.releasePointerCapture?.(pointerId)
    } catch {
      // Ignore — capture may already be released.
    }
  }

  const onPointerUp = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId) return
    releaseCapture(event.pointerId)
    settle(false)
  }

  const onPointerCancel = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId) return
    releaseCapture(event.pointerId)
    settle(true)
  }

  let boundEl: HTMLElement | null = null

  const bind = (element: HTMLElement) => {
    element.addEventListener('pointerdown', onPointerDown, { passive: true })
    element.addEventListener('pointermove', onPointerMove, { passive: true })
    element.addEventListener('pointerup', onPointerUp, { passive: true })
    element.addEventListener('pointercancel', onPointerCancel, { passive: true })
    boundEl = element
  }

  const unbind = (element: HTMLElement) => {
    element.removeEventListener('pointerdown', onPointerDown)
    element.removeEventListener('pointermove', onPointerMove)
    element.removeEventListener('pointerup', onPointerUp)
    element.removeEventListener('pointercancel', onPointerCancel)
    if (boundEl === element) boundEl = null
  }

  // The header element is inside a teleported portal that mounts/unmounts each
  // time the drawer opens/closes, so rebind on every change (not just onMounted).
  // Listeners go on the *content* so a drag can also start in the body once the
  // sheet is below its top snap.
  watch(headerEl, (element) => {
    if (boundEl) unbind(boundEl)

    // The previous sheet is gone; drop any inline styling we left on it.
    if (bodyEl) applyScrollLock(true)
    contentEl = null
    overlayEl = null
    handleEl = null
    bodyEl = null
    restOffset = 0
    restIndex = -1
    resetGesture()

    if (!element) return

    contentEl = element.closest<HTMLElement>('[data-slot="content"]')
    if (!contentEl) return

    // The overlay is the content's sibling inside the same portal.
    overlayEl = contentEl.parentElement?.querySelector<HTMLElement>('[data-slot="overlay"]') ?? null
    handleEl = contentEl.querySelector<HTMLElement>('[data-slot="handle"]')
    bodyEl = contentEl.querySelector<HTMLElement>('[data-slot="body"]')

    // Every sheet opens at its top snap.
    restIndex = 0
    bind(contentEl)
  }, { immediate: true })

  onBeforeUnmount(() => {
    if (boundEl) unbind(boundEl)
  })
}
