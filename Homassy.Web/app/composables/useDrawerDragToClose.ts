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
}

const AXIS_LOCK_SLOP = 6
// vaul's own open/close easing (its internal `O.EASE`). Reusing it keeps our
// manual commit indistinguishable from the library's native slideToBottom.
const DRAWER_EASE = 'cubic-bezier(0.32, 0.72, 0, 1)'
const RELEASE_DURATION = 350

/**
 * Drag-down-to-close for a bottom `UDrawer` whose native dismiss is disabled
 * (`dismissible: false`, which also keeps outside-tap / Esc from closing it).
 *
 * Attach it to the drawer *header* element: dragging the header down translates
 * the whole drawer content; releasing past the threshold slides it the rest of
 * the way out and calls `onClose` (the parent then flips `open` and vaul's
 * native slideToBottom continues seamlessly), otherwise it snaps back.
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

  let contentEl: HTMLElement | null = null
  let activePointerId: number | null = null
  let startX = 0
  let startY = 0
  let axis: 'vertical' | 'ignore' | null = null
  let engaged = false
  let hapticFired = false
  let currentOffset = 0

  const threshold = (): number => {
    const height = contentEl?.getBoundingClientRect().height ?? 0
    return Math.max(height * thresholdRatio, minThreshold)
  }

  const resetGesture = () => {
    activePointerId = null
    axis = null
    engaged = false
    hapticFired = false
    currentOffset = 0
  }

  const settle = (cancelled: boolean) => {
    const el = contentEl
    if (!engaged || !el) {
      resetGesture()
      return
    }

    const committed = !cancelled && currentOffset >= threshold()
    el.style.transition = `transform ${RELEASE_DURATION}ms ${DRAWER_EASE}`

    if (committed) {
      // Continue sliding out; the parent flips `open`, and vaul's slideToBottom
      // takes over from this offset with the same easing (no visible jump).
      el.style.transform = 'translate3d(0, 100%, 0)'
      options.onClose()
    } else {
      // Snap back to fully open, then drop the inline styles so vaul's base
      // transform rule governs the element again.
      el.style.transform = 'translate3d(0, 0, 0)'
      const onEnd = (event: TransitionEvent) => {
        if (event.propertyName !== 'transform') return
        el.removeEventListener('transitionend', onEnd)
        el.style.transform = ''
        el.style.transition = ''
      }
      el.addEventListener('transitionend', onEnd)
    }

    resetGesture()
  }

  const onPointerDown = (event: PointerEvent) => {
    if (toValue(options.disabled)) return
    // Mouse is limited to the primary button; touch/pen always allowed.
    if (event.pointerType === 'mouse' && event.button !== 0) return
    if (activePointerId !== null) return
    // Let interactive header controls (the close button, etc.) keep their own
    // pointer handling — don't start a drag on top of them.
    if ((event.target as HTMLElement | null)?.closest('button, a, input, select, textarea, [role="button"]')) return

    contentEl = headerEl.value?.closest<HTMLElement>('[data-slot="content"]') ?? null
    if (!contentEl) return

    activePointerId = event.pointerId
    startX = event.clientX
    startY = event.clientY
    axis = null
    engaged = false
    hapticFired = false
    currentOffset = 0
  }

  const onPointerMove = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId || !contentEl) return
    if (axis === 'ignore') return

    const dx = event.clientX - startX
    const dy = event.clientY - startY

    if (axis === null) {
      if (Math.abs(dx) < AXIS_LOCK_SLOP && Math.abs(dy) < AXIS_LOCK_SLOP) return
      // Only a downward, vertically-dominant drag closes the drawer; anything
      // else (upward, or mostly horizontal) is left alone.
      if (dy > 0 && dy >= Math.abs(dx)) {
        axis = 'vertical'
        engaged = true
        try {
          headerEl.value?.setPointerCapture(event.pointerId)
        } catch {
          // setPointerCapture can throw if the pointer is no longer active
          // (e.g. Safari edge cases) — the gesture still works without capture.
        }
        contentEl.style.transition = 'none'
      } else {
        axis = 'ignore'
        return
      }
    }

    currentOffset = Math.max(0, dy)
    contentEl.style.transform = `translate3d(0, ${currentOffset}px, 0)`

    if (!hapticFired && currentOffset >= threshold()) {
      hapticFired = true
      navigator.vibrate?.(10)
    }
  }

  const releaseCapture = (pointerId: number) => {
    try {
      headerEl.value?.releasePointerCapture?.(pointerId)
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
  watch(headerEl, (element) => {
    if (boundEl) unbind(boundEl)
    if (element) bind(element)
    resetGesture()
    contentEl = null
  }, { immediate: true })

  onBeforeUnmount(() => {
    if (boundEl) unbind(boundEl)
  })
}
