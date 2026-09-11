/**
 * Pointer-driven drag-and-drop reordering for the app's card lists (#113).
 *
 * Shared by the shopping list and by the master-data lists, so the gesture, the auto-scroll and the
 * haptics are defined once. The composable owns only the *preview* order — the array of keys the
 * list is currently showing while a finger is down. The caller renders from that preview and, on
 * drop, sends the resulting order to its own reorder endpoint.
 *
 * How the lift works, and why it is a clone
 * -----------------------------------------
 * Every reorderable list in this app is wrapped in `AnimatedList`, whose `TransitionGroup` animates
 * moves by writing `transform` on each child. A dragged card that followed the finger through its
 * own `transform` would be fighting that FLIP for the same property, and would be snapped back to a
 * slot every time the order changed under it. So the element that follows the finger is a detached
 * `cloneNode` pinned to the viewport, while the real card stays in the list at reduced opacity as
 * the placeholder. The placeholder and its neighbours then glide with the existing `--bubble-move`
 * transition, which is exactly the motion this feature was supposed to reuse.
 *
 * Touch vs mouse
 * --------------
 * On touch the drag only starts after a long press, because a list that lifted a card on the first
 * pixel of movement could not be scrolled. On mouse the handle is an explicit grab affordance, so
 * the drag starts on press.
 */
import type { MaybeRefOrGetter, Ref } from 'vue'
import { sameOrder } from '~/utils/manualOrder'

export interface DragReorderOptions {
  /** The keys currently on screen, in display order. Read fresh on every lift. */
  keys: () => string[]
  /**
   * Called once on drop with the new order, but only when it actually differs. Rejecting (or
   * throwing) is the caller's signal that the write failed; the composable has already released
   * the preview by then, so reverting is the caller's job.
   */
  onReorder: (orderedKeys: string[]) => void | Promise<void>
  /** The element that contains the rows. Rows are found by their `data-reorder-key` attribute. */
  container: Ref<HTMLElement | null>
  disabled?: MaybeRefOrGetter<boolean>
  /** How long a touch must be held before the card lifts (default 350ms). */
  longPressMs?: number
  /** How close to a viewport edge the pointer must be for auto-scroll (default 72px). */
  scrollEdge?: number
  /** Peak auto-scroll speed in pixels per frame (default 14). */
  scrollSpeed?: number
}

/** Movement that cancels a pending long press — a scroll, not a drag. */
const LONG_PRESS_SLOP = 10
/** How long the clone takes to fly back onto the placeholder after the drop. */
const DROP_MS = 180

export const useDragReorder = (options: DragReorderOptions) => {
  const haptics = useHaptics()

  const longPressMs = options.longPressMs ?? 350
  const scrollEdge = options.scrollEdge ?? 72
  const scrollSpeed = options.scrollSpeed ?? 14

  /** The key being dragged, or null. Also what the caller dims / marks as the placeholder. */
  const draggingKey = ref<string | null>(null)
  /**
   * The order the list should render while a drag is in progress, or null when it should use its
   * own ordering. Null rather than an empty array so "no drag" and "an empty list" stay distinct.
   */
  const previewKeys = ref<string[] | null>(null)

  const isDragging = computed(() => draggingKey.value !== null)

  let pointerId: number | null = null
  let startX = 0
  let startY = 0
  let lastX = 0
  let lastY = 0
  let longPressTimer: ReturnType<typeof setTimeout> | null = null
  let lifted = false
  let clone: HTMLElement | null = null
  let cloneOffsetX = 0
  let cloneOffsetY = 0
  let scrollFrame: number | null = null
  let originalKeys: string[] = []
  let sourceEl: HTMLElement | null = null

  const isDisabled = () => toValue(options.disabled) === true

  const rowElements = (): HTMLElement[] => {
    const root = options.container.value
    if (!root) return []
    return Array.from(root.querySelectorAll<HTMLElement>('[data-reorder-key]'))
  }

  const rowFor = (key: string): HTMLElement | null =>
    rowElements().find(el => el.dataset.reorderKey === key) ?? null

  // --- The floating clone ----------------------------------------------------

  const createClone = (element: HTMLElement) => {
    const rect = element.getBoundingClientRect()
    const copy = element.cloneNode(true) as HTMLElement

    copy.removeAttribute('data-reorder-key')
    copy.setAttribute('aria-hidden', 'true')
    copy.style.position = 'fixed'
    copy.style.left = `${rect.left}px`
    copy.style.top = `${rect.top}px`
    copy.style.width = `${rect.width}px`
    copy.style.height = `${rect.height}px`
    copy.style.margin = '0'
    copy.style.pointerEvents = 'none'
    copy.style.zIndex = '60'
    copy.style.transformOrigin = 'center'
    copy.style.transform = 'scale(1.04)'
    copy.style.boxShadow = '0 18px 40px -12px rgb(0 0 0 / 0.45)'
    copy.style.transition = 'transform 120ms ease-out, box-shadow 120ms ease-out'
    copy.style.willChange = 'transform'

    document.body.appendChild(copy)
    clone = copy
    cloneOffsetX = rect.left
    cloneOffsetY = rect.top
  }

  const moveClone = () => {
    if (!clone) return
    const dx = lastX - startX
    const dy = lastY - startY
    clone.style.transform = `translate3d(${dx}px, ${dy}px, 0) scale(1.04)`
  }

  const dropClone = () => {
    const copy = clone
    clone = null
    if (!copy) return

    // Land on wherever the placeholder ended up, so the card does not teleport on release.
    const target = draggingKey.value ? rowFor(draggingKey.value) : null
    if (target) {
      const rect = target.getBoundingClientRect()
      copy.style.transition = `transform ${DROP_MS}ms cubic-bezier(0.22, 1, 0.36, 1), box-shadow ${DROP_MS}ms ease-out`
      copy.style.transform = `translate3d(${rect.left - cloneOffsetX}px, ${rect.top - cloneOffsetY}px, 0) scale(1)`
      copy.style.boxShadow = 'none'
      setTimeout(() => copy.remove(), DROP_MS)
    } else {
      copy.remove()
    }
  }

  // --- Auto-scroll -----------------------------------------------------------

  const stopAutoScroll = () => {
    if (scrollFrame !== null) {
      cancelAnimationFrame(scrollFrame)
      scrollFrame = null
    }
  }

  /**
   * Scrolls the page while the pointer sits near the top or bottom edge, ramping from nothing at
   * the edge threshold to full speed at the edge itself — a constant speed makes a long list either
   * unusable or uncontrollable.
   */
  const autoScrollStep = () => {
    scrollFrame = null
    if (!lifted) return

    const height = window.innerHeight
    let delta = 0
    if (lastY < scrollEdge) {
      delta = -scrollSpeed * (1 - lastY / scrollEdge)
    } else if (lastY > height - scrollEdge) {
      delta = scrollSpeed * (1 - (height - lastY) / scrollEdge)
    }

    if (delta !== 0) {
      window.scrollBy(0, delta)
      // The list moved under a stationary finger, so the drop target may have changed.
      updateTargetIndex()
      moveClone()
    }

    scrollFrame = requestAnimationFrame(autoScrollStep)
  }

  // --- Where the card would land ---------------------------------------------

  /**
   * The row the pointer is currently over: the one whose box contains it, or failing that the
   * nearest by centre distance. Distance rather than "the first one below" because these lists are
   * grids on wider screens, where "below" is not a single direction.
   */
  const rowUnderPointer = (): HTMLElement | null => {
    let nearest: HTMLElement | null = null
    let nearestDistance = Number.POSITIVE_INFINITY

    for (const element of rowElements()) {
      const rect = element.getBoundingClientRect()
      if (lastX >= rect.left && lastX <= rect.right && lastY >= rect.top && lastY <= rect.bottom) {
        return element
      }
      const dx = lastX - (rect.left + rect.width / 2)
      const dy = lastY - (rect.top + rect.height / 2)
      const distance = dx * dx + dy * dy
      if (distance < nearestDistance) {
        nearestDistance = distance
        nearest = element
      }
    }

    return nearest
  }

  const updateTargetIndex = () => {
    const key = draggingKey.value
    const order = previewKeys.value
    if (!key || !order) return

    const target = rowUnderPointer()
    const targetKey = target?.dataset.reorderKey
    if (!targetKey || targetKey === key) return

    const from = order.indexOf(key)
    const to = order.indexOf(targetKey)
    if (from < 0 || to < 0) return

    const next = [...order]
    next.splice(from, 1)
    next.splice(to, 0, key)
    previewKeys.value = next
    haptics.select()
  }

  // --- The gesture -----------------------------------------------------------

  const lift = (key: string) => {
    const element = rowFor(key)
    if (!element) return

    lifted = true
    sourceEl = element
    originalKeys = options.keys()
    previewKeys.value = [...originalKeys]
    draggingKey.value = key

    createClone(element)
    haptics.impact()
    // Stop the page scrolling under the drag on browsers that would otherwise pan.
    document.body.style.userSelect = 'none'
    scrollFrame = requestAnimationFrame(autoScrollStep)
  }

  const clearLongPress = () => {
    if (longPressTimer !== null) {
      clearTimeout(longPressTimer)
      longPressTimer = null
    }
  }

  const reset = () => {
    clearLongPress()
    stopAutoScroll()
    lifted = false
    pointerId = null
    sourceEl = null
    document.body.style.removeProperty('user-select')
  }

  const finish = async (cancelled: boolean) => {
    const order = previewKeys.value
    const key = draggingKey.value

    // Measured against the placeholder's final slot, so both still need the preview order.
    dropClone()
    reset()

    const unchanged = !order || !key || sameOrder(order, originalKeys)

    if (cancelled || unchanged) {
      draggingKey.value = null
      previewKeys.value = null
      return
    }

    haptics.success()
    // Started before the preview is released: callers apply their optimistic reorder synchronously,
    // so handing over this way means the list never renders its pre-drag order for a frame.
    const write = options.onReorder(order!)
    draggingKey.value = null
    previewKeys.value = null
    await write
  }

  const onPointerMove = (event: PointerEvent) => {
    if (event.pointerId !== pointerId) return
    lastX = event.clientX
    lastY = event.clientY

    if (!lifted) {
      // Still waiting on the long press: any real movement means the user meant to scroll.
      if (Math.abs(lastX - startX) > LONG_PRESS_SLOP || Math.abs(lastY - startY) > LONG_PRESS_SLOP) {
        clearLongPress()
        detach()
      }
      return
    }

    event.preventDefault()
    moveClone()
    updateTargetIndex()
  }

  const onPointerUp = (event: PointerEvent) => {
    if (event.pointerId !== pointerId) return
    const wasLifted = lifted
    detach()
    if (wasLifted) void finish(false)
  }

  const onPointerCancel = (event: PointerEvent) => {
    if (event.pointerId !== pointerId) return
    const wasLifted = lifted
    detach()
    if (wasLifted) void finish(true)
  }

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key !== 'Escape' || !lifted) return
    detach()
    void finish(true)
  }

  /** Removes the window listeners. Split from `reset` so a cancelled long press can bail early. */
  const detach = () => {
    window.removeEventListener('pointermove', onPointerMove)
    window.removeEventListener('pointerup', onPointerUp)
    window.removeEventListener('pointercancel', onPointerCancel)
    window.removeEventListener('keydown', onKeyDown)
    if (!lifted) reset()
  }

  /**
   * Starts a drag from a row's handle. Bind to the handle's `pointerdown`, never to the card: the
   * card's own tap and swipe gestures have to keep working.
   */
  const startDrag = (event: PointerEvent, key: string) => {
    if (isDisabled() || draggingKey.value !== null || !import.meta.client) return
    if (event.button !== undefined && event.button !== 0) return

    pointerId = event.pointerId
    startX = event.clientX
    startY = event.clientY
    lastX = startX
    lastY = startY
    lifted = false

    window.addEventListener('pointermove', onPointerMove, { passive: false })
    window.addEventListener('pointerup', onPointerUp)
    window.addEventListener('pointercancel', onPointerCancel)
    window.addEventListener('keydown', onKeyDown)

    if (event.pointerType === 'mouse') {
      event.preventDefault()
      lift(key)
    } else {
      longPressTimer = setTimeout(() => {
        longPressTimer = null
        lift(key)
      }, longPressMs)
    }
  }

  /**
   * Moves one row by one position without a pointer, for the handle's arrow keys. Keeps the whole
   * feature usable from a keyboard, where a drag gesture has no equivalent at all.
   */
  const moveByKeyboard = async (key: string, direction: -1 | 1) => {
    if (isDisabled() || draggingKey.value !== null) return

    const order = options.keys()
    const from = order.indexOf(key)
    const to = from + direction
    if (from < 0 || to < 0 || to >= order.length) return

    const next = [...order]
    next.splice(from, 1)
    next.splice(to, 0, key)

    haptics.select()
    await options.onReorder(next)
  }

  onBeforeUnmount(() => {
    detach()
    stopAutoScroll()
    clearLongPress()
    clone?.remove()
    clone = null
    if (import.meta.client) document.body.style.removeProperty('user-select')
  })

  return {
    isDragging,
    draggingKey,
    previewKeys,
    startDrag,
    moveByKeyboard,
    /** The row currently lifted, exposed so a caller can style its placeholder. */
    sourceElement: () => sourceEl
  }
}
