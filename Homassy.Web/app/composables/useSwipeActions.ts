import type { CSSProperties, MaybeRefOrGetter, Ref } from 'vue'

interface SwipeActionsOptions {
  onSwipeLeft?: () => void
  onSwipeRight?: () => void
  disabled?: MaybeRefOrGetter<boolean>
  /** Commit threshold as a ratio of the element width (default 0.4) */
  thresholdRatio?: number
  /** Minimum commit threshold in pixels (default 56) */
  minThreshold?: number
  /** Maximum drag distance as a ratio of the element width (default 0.6) */
  maxDragRatio?: number
}

const AXIS_LOCK_SLOP = 8
const CLICK_SUPPRESS_DISTANCE = 10
const SNAP_BACK_EASE = 'cubic-bezier(0.22, 1, 0.36, 1)'
const COMMIT_EASE = 'cubic-bezier(0.34, 1.56, 0.64, 1)'

export const useSwipeActions = (
  el: Ref<HTMLElement | null>,
  options: SwipeActionsOptions
) => {
  const thresholdRatio = options.thresholdRatio ?? 0.4
  const minThreshold = options.minThreshold ?? 56
  const maxDragRatio = options.maxDragRatio ?? 0.6

  const translateX = ref(0)
  const isSwiping = ref(false)
  const suppressClick = ref(false)
  const isReleasing = ref(false)
  const committed = ref(false)

  let activePointerId: number | null = null
  let startX = 0
  let startY = 0
  let axisLocked: 'horizontal' | 'vertical' | null = null
  let hapticFired = false

  const threshold = (): number => {
    const width = el.value?.offsetWidth ?? 0
    return Math.max(width * thresholdRatio, minThreshold)
  }

  const direction = computed<'left' | 'right' | null>(() => {
    if (translateX.value < 0) return 'left'
    if (translateX.value > 0) return 'right'
    return null
  })

  const progress = computed(() => {
    const t = threshold()
    if (t === 0) return 0
    return Math.min(Math.abs(translateX.value) / t, 1.2)
  })

  const cardStyle = computed<CSSProperties>(() => {
    if (isSwiping.value) {
      return {
        transform: `translate3d(${translateX.value}px, 0, 0)`,
        transition: 'none'
      }
    }
    if (isReleasing.value) {
      return {
        transform: 'translate3d(0, 0, 0)',
        transition: `transform 340ms ${committed.value ? COMMIT_EASE : SNAP_BACK_EASE}`
      }
    }
    return {}
  })

  const resetGesture = () => {
    activePointerId = null
    axisLocked = null
    hapticFired = false
  }

  const onPointerDown = (event: PointerEvent) => {
    if (toValue(options.disabled)) return
    // Mouse is limited to the primary button; touch/pen always allowed
    if (event.pointerType === 'mouse' && event.button !== 0) return
    if (activePointerId !== null) return

    activePointerId = event.pointerId
    startX = event.clientX
    startY = event.clientY
    axisLocked = null
    hapticFired = false
    isReleasing.value = false
    committed.value = false
  }

  const onPointerMove = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId) return
    if (axisLocked === 'vertical') return

    const dx = event.clientX - startX
    const dy = event.clientY - startY

    if (axisLocked === null) {
      if (Math.abs(dx) < AXIS_LOCK_SLOP && Math.abs(dy) < AXIS_LOCK_SLOP) return
      if (Math.abs(dy) >= Math.abs(dx)) {
        axisLocked = 'vertical'
        return
      }
      axisLocked = 'horizontal'
      el.value?.setPointerCapture(event.pointerId)
      isSwiping.value = true
    }

    const t = threshold()
    const width = el.value?.offsetWidth ?? 0
    const maxDrag = width * maxDragRatio

    let offset = dx
    if (Math.abs(dx) > t) {
      const overshoot = Math.abs(dx) - t
      offset = Math.sign(dx) * (t + overshoot * 0.25)
    }
    offset = Math.max(-maxDrag, Math.min(maxDrag, offset))
    translateX.value = offset

    if (Math.abs(dx) > CLICK_SUPPRESS_DISTANCE) {
      suppressClick.value = true
    }

    if (!hapticFired && Math.abs(offset) >= t) {
      hapticFired = true
      navigator.vibrate?.(10)
    }
  }

  const finishGesture = (cancelled: boolean) => {
    const wasHorizontal = axisLocked === 'horizontal'
    const offset = translateX.value
    const t = threshold()

    resetGesture()

    if (!wasHorizontal) return

    committed.value = !cancelled && Math.abs(offset) >= t
    isSwiping.value = false
    isReleasing.value = true
    translateX.value = 0

    if (committed.value) {
      if (offset < 0) options.onSwipeLeft?.()
      else options.onSwipeRight?.()
    }

    // Clear the click-suppress flag after the browser's click event has fired
    setTimeout(() => {
      suppressClick.value = false
    }, 0)
  }

  const onPointerUp = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId) return
    el.value?.releasePointerCapture?.(event.pointerId)
    finishGesture(false)
  }

  const onPointerCancel = (event: PointerEvent) => {
    if (event.pointerId !== activePointerId) return
    el.value?.releasePointerCapture?.(event.pointerId)
    finishGesture(true)
  }

  const onTransitionEnd = (event: TransitionEvent) => {
    // Other transitions (e.g. box-shadow) also bubble here; only react to our snap-back
    if (event.propertyName !== 'transform') return
    isReleasing.value = false
    committed.value = false
  }

  onMounted(() => {
    const element = el.value
    if (!element) return
    element.addEventListener('pointerdown', onPointerDown, { passive: true })
    element.addEventListener('pointermove', onPointerMove, { passive: true })
    element.addEventListener('pointerup', onPointerUp, { passive: true })
    element.addEventListener('pointercancel', onPointerCancel, { passive: true })
    element.addEventListener('transitionend', onTransitionEnd, { passive: true })
  })

  onBeforeUnmount(() => {
    const element = el.value
    if (!element) return
    element.removeEventListener('pointerdown', onPointerDown)
    element.removeEventListener('pointermove', onPointerMove)
    element.removeEventListener('pointerup', onPointerUp)
    element.removeEventListener('pointercancel', onPointerCancel)
    element.removeEventListener('transitionend', onTransitionEnd)
  })

  return {
    translateX,
    isSwiping,
    direction,
    progress,
    suppressClick,
    cardStyle
  }
}
