<template>
  <Teleport to="body">
    <div
      v-if="open"
      ref="root"
      tabindex="-1"
      class="fixed inset-0 z-[100] flex touch-none select-none flex-col outline-none"
      role="dialog"
      aria-modal="true"
      :aria-label="t('lightbox.title')"
      @keydown="onKeydown"
    >
      <!-- Backdrop. Opacity and blur interpolate with the dismiss drag rather than toggling, so
           the gesture shows how far it still has to go. The blur is skipped under reduced motion;
           the opacity ramp is not, because it is the feedback, not decoration. -->
      <div class="absolute inset-0 bg-black" :style="backdropStyle" />

      <!-- Chrome: counter and close, faded out as the image is dragged away. -->
      <div class="relative flex items-center justify-between gap-2 p-3" :style="{ opacity: chromeOpacity }">
        <span
          v-if="images.length > 1"
          class="rounded-full bg-black/50 px-3 py-1 text-sm tabular-nums text-white"
        >{{ t('lightbox.counter', { current: index + 1, total: images.length }) }}</span>
        <span v-else />

        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          size="lg"
          :aria-label="t('lightbox.close')"
          class="text-white hover:bg-white/10"
          @click="close()"
        />
      </div>

      <!-- Stage: one track holding every image, translated horizontally by page. -->
      <div
        ref="stage"
        class="relative flex-1 overflow-hidden"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
        @wheel.prevent="onWheel"
      >
        <div class="absolute inset-0 flex" :style="trackStyle">
          <div v-for="(image, i) in images" :key="image.full ?? image.thumb ?? i" class="relative h-full w-full shrink-0">
            <div class="absolute inset-0" :style="i === index ? viewStyle : undefined">
              <!-- The thumbnail underneath is the placeholder: it was just on screen, so it is in
                   the browser cache and there is something to look at while the large variant
                   loads. It is also what the open transition appears to grow. -->
              <img
                v-if="image.thumb"
                :src="mediaUrl(image.thumb)"
                alt=""
                aria-hidden="true"
                crossorigin="use-credentials"
                class="absolute inset-0 h-full w-full object-contain"
              >
              <img
                v-if="image.full && i === index"
                :src="mediaUrl(image.full)"
                :alt="image.alt ?? ''"
                crossorigin="use-credentials"
                class="absolute inset-0 h-full w-full object-contain transition-opacity duration-200"
                :class="fullLoaded ? 'opacity-100' : 'opacity-0'"
                @load="fullLoaded = true"
              >
            </div>
          </div>
        </div>
      </div>

      <!-- Paging dots. Only meaningful with more than one image; carried here so a future product
           gallery is a prop change rather than another rewrite. -->
      <div
        v-if="images.length > 1"
        class="relative flex items-center justify-center gap-2 p-4"
        :style="{ opacity: chromeOpacity }"
      >
        <button
          v-for="(image, i) in images"
          :key="`dot-${i}`"
          type="button"
          class="h-2 rounded-full transition-all"
          :class="i === index ? 'w-6 bg-white' : 'w-2 bg-white/40'"
          :aria-label="t('lightbox.goTo', { index: i + 1 })"
          :aria-current="i === index ? 'true' : undefined"
          @click="goTo(i)"
        />
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import type { CSSProperties } from 'vue'

/**
 * The app's one full-screen image viewer.
 *
 * Replaces the overlays the inventory drawer and the shopping-list card each kept for themselves:
 * their own `isImageOverlayOpen`, their own markup, no zoom, no pan, no dismiss gesture.
 *
 * Gestures, and how they are arbitrated:
 *
 * - **Two pointers** pinch to zoom. Always wins — there is no one-finger gesture a second finger
 *   could be continuing.
 * - **One pointer while zoomed in** pans, clamped to the image's own edges.
 * - **One pointer at 1x** locks to an axis after a few pixels: down dismisses, sideways pages
 *   (when there is more than one image). The lock is what keeps a slightly-off vertical flick from
 *   turning into a page turn.
 * - **Double tap** toggles between fit and 2.5x, centred on the tap, so what was tapped stays
 *   under the finger.
 * - **Wheel, and the trackpad pinch that arrives as ctrl+wheel**, zooms about the cursor.
 *
 * Under `prefers-reduced-motion` the shared-element transition and the backdrop blur are skipped.
 */
export interface LightboxImage {
  /** Thumbnail path — the placeholder, and what the open transition grows. */
  thumb?: string | null
  /** Full-size path. Only the visible page's is rendered, so a gallery loads one large image. */
  full?: string | null
  alt?: string | null
}

const props = withDefaults(defineProps<{
  open: boolean
  images: LightboxImage[]
  /** Which image to show first. */
  startIndex?: number
  /**
   * The thumbnail this was opened from. Its on-screen box is where the open transition starts and
   * where the close transition returns to; with no origin the lightbox simply fades.
   */
  origin?: HTMLElement | null
}>(), {
  startIndex: 0,
  origin: null
})

const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { t } = useI18n()
const { mediaUrl } = useMediaUrl()
const haptics = useHaptics()

const MAX_SCALE = 5
const DOUBLE_TAP_SCALE = 2.5
const DOUBLE_TAP_MS = 300
const AXIS_LOCK_SLOP = 8
const DISMISS_DISTANCE = 120
const PAGE_FRACTION = 0.25
const TRANSITION_MS = 260
const EASE = 'cubic-bezier(0.22, 1, 0.36, 1)'

const root = ref<HTMLElement | null>(null)
const stage = ref<HTMLElement | null>(null)

const index = ref(props.startIndex)
const scale = ref(1)
const panX = ref(0)
const panY = ref(0)
/** Vertical drag at 1x — the dismiss gesture. */
const dismissY = ref(0)
/** Horizontal drag at 1x — the paging gesture. */
const pageX = ref(0)
const isDragging = ref(false)
const isClosing = ref(false)
const fullLoaded = ref(false)
const reducedMotion = ref(false)

/**
 * While set, the visible page is pinned to `originTransform` at zero opacity — the state the
 * shared-element transition runs from on open and to on close. Null before the stage has been
 * measured, which is a frame the dialog spends invisible rather than showing the image full-size
 * and then jumping.
 */
const transitioning = ref(false)
const originTransform = ref<string | null>(null)

let pointers = new Map<number, { x: number, y: number }>()
let axis: 'horizontal' | 'vertical' | null = null
let startX = 0
let startY = 0
let startPanX = 0
let startPanY = 0
let startScale = 1
let startDistance = 0
let lastTapAt = 0
let closeTimer: ReturnType<typeof setTimeout> | null = null
let releaseTimer: ReturnType<typeof setTimeout> | null = null

const dismissProgress = computed(() => Math.min(Math.abs(dismissY.value) / DISMISS_DISTANCE, 1))
const chromeOpacity = computed(() => String(1 - dismissProgress.value))

const backdropStyle = computed<CSSProperties>(() => ({
  opacity: String(transitioning.value ? 0 : 1 - dismissProgress.value * 0.6),
  backdropFilter: reducedMotion.value ? undefined : 'blur(12px)',
  transition: isDragging.value ? 'none' : `opacity ${TRANSITION_MS}ms ease`
}))

const trackStyle = computed<CSSProperties>(() => {
  const width = stage.value?.clientWidth ?? 0
  return {
    transform: `translate3d(${-index.value * width + pageX.value}px, 0, 0)`,
    transition: isDragging.value ? 'none' : `transform ${TRANSITION_MS}ms ${EASE}`
  }
})

const viewStyle = computed<CSSProperties>(() => {
  const motion = reducedMotion.value
    ? 'none'
    : `transform ${TRANSITION_MS}ms ${EASE}, opacity ${TRANSITION_MS}ms ease`

  if (transitioning.value) {
    return {
      // `scale(0.92)` is the pre-measurement state: invisible anyway, and it means a lightbox with
      // no origin still gets a plain fade instead of a snap.
      transform: originTransform.value ?? 'scale(0.92)',
      opacity: '0',
      transition: motion
    }
  }

  return {
    transform: `translate3d(${panX.value}px, ${panY.value + dismissY.value}px, 0) scale(${scale.value})`,
    opacity: '1',
    transition: isDragging.value ? 'none' : motion
  }
})

// --- Open / close ------------------------------------------------------------

watch(() => props.open, (isOpen) => {
  if (isOpen) onOpened()
  else onClosed()
})

function onOpened() {
  index.value = Math.min(Math.max(props.startIndex, 0), Math.max(props.images.length - 1, 0))
  resetView()
  fullLoaded.value = false
  isClosing.value = false
  document.body.style.overflow = 'hidden'

  originTransform.value = null
  transitioning.value = !reducedMotion.value

  nextTick(() => {
    if (transitioning.value) {
      // The stage only has a box once it is in the document, so the transform cannot be computed
      // before this point.
      originTransform.value = flipTransform()
      release()
    }

    focusDialog()
  })
}

function onClosed() {
  document.body.style.overflow = ''
  transitioning.value = false
  originTransform.value = null
  isClosing.value = false
  clearTimers()
}

/**
 * Lets go of the origin state, so the CSS transition has somewhere to run to.
 *
 * Two frames, because a single one gets coalesced with the style that set the origin state and
 * then no transition runs at all. The timeout is not belt-and-braces: `requestAnimationFrame`
 * does not fire in a hidden tab, and without it a lightbox opened in one would stay pinned at
 * zero opacity for as long as the tab stayed hidden.
 */
function release() {
  requestAnimationFrame(() => requestAnimationFrame(() => {
    transitioning.value = false
  }))

  releaseTimer = setTimeout(() => {
    transitioning.value = false
  }, 120)
}

function clearTimers() {
  if (closeTimer) {
    clearTimeout(closeTimer)
    closeTimer = null
  }
  if (releaseTimer) {
    clearTimeout(releaseTimer)
    releaseTimer = null
  }
}

/**
 * Closes, running the shared-element transition back to the thumbnail first.
 *
 * `immediate` skips it, for the cases with nothing to return to: a dismiss flick (the image is
 * already on its way off screen) and reduced motion.
 */
function close(immediate = false) {
  if (isClosing.value) return

  const back = immediate || reducedMotion.value ? null : flipTransform()
  if (!back) {
    emit('update:open', false)
    return
  }

  isClosing.value = true
  originTransform.value = back
  transitioning.value = true

  closeTimer = setTimeout(() => emit('update:open', false), TRANSITION_MS)
}

/**
 * The transform that maps the stage onto the origin thumbnail's box.
 *
 * Uniform scale, matched on the wider ratio and centre-aligned: the image and the thumbnail both
 * render `object-contain`, so a non-uniform scale would squash the picture mid-flight to match a
 * box whose edges are not visible anyway.
 */
function flipTransform(): string | null {
  const originEl = props.origin
  const stageEl = stage.value
  if (!originEl || !stageEl) return null

  const from = originEl.getBoundingClientRect()
  const to = stageEl.getBoundingClientRect()
  if (from.width === 0 || to.width === 0) return null

  const ratio = Math.max(from.width / to.width, from.height / to.height)
  const dx = (from.left + from.width / 2) - (to.left + to.width / 2)
  const dy = (from.top + from.height / 2) - (to.top + to.height / 2)

  return `translate3d(${dx}px, ${dy}px, 0) scale(${ratio})`
}

function resetView() {
  scale.value = 1
  panX.value = 0
  panY.value = 0
  dismissY.value = 0
  pageX.value = 0
  axis = null
}

// --- Paging ------------------------------------------------------------------

function goTo(next: number) {
  const clamped = Math.min(Math.max(next, 0), props.images.length - 1)
  if (clamped === index.value) return

  index.value = clamped
  resetView()
  fullLoaded.value = false
  haptics.select()
}

// --- Zoom --------------------------------------------------------------------

/** Zooms about a point in viewport coordinates, keeping what is under it under it. */
function zoomTo(next: number, clientX: number, clientY: number) {
  const stageEl = stage.value
  if (!stageEl) return

  const target = Math.min(Math.max(next, 1), MAX_SCALE)
  if (target === scale.value) return

  const box = stageEl.getBoundingClientRect()
  // The point in the image's own space: relative to the stage centre, before the current pan.
  const px = clientX - box.left - box.width / 2 - panX.value
  const py = clientY - box.top - box.height / 2 - panY.value
  const factor = target / scale.value

  scale.value = target
  panX.value += px - px * factor
  panY.value += py - py * factor

  if (target === 1) {
    panX.value = 0
    panY.value = 0
  } else {
    clampPan()
  }
}

/** Keeps the stage's edges from being dragged inside the viewport. */
function clampPan() {
  const stageEl = stage.value
  if (!stageEl) return

  const box = stageEl.getBoundingClientRect()
  const maxX = Math.max(0, (box.width * scale.value - box.width) / 2)
  const maxY = Math.max(0, (box.height * scale.value - box.height) / 2)

  panX.value = Math.min(Math.max(panX.value, -maxX), maxX)
  panY.value = Math.min(Math.max(panY.value, -maxY), maxY)
}

function onWheel(event: WheelEvent) {
  zoomTo(scale.value * (event.deltaY > 0 ? 0.85 : 1.18), event.clientX, event.clientY)
}

// --- Pointer gestures --------------------------------------------------------

function onPointerDown(event: PointerEvent) {
  if (isClosing.value) return

  // Capture so a drag that leaves the stage still reports moves. Guarded because the call throws
  // when the pointer is not active any more, and a throw here would abandon the gesture entirely.
  try {
    stage.value?.setPointerCapture(event.pointerId)
  } catch {
    // Not capturable: the gesture still works while the pointer stays over the stage.
  }

  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY })

  if (pointers.size === 2) {
    startDistance = pointerDistance()
    startScale = scale.value
    isDragging.value = true
    axis = null
    return
  }

  if (pointers.size > 2) return

  const now = Date.now()
  if (now - lastTapAt < DOUBLE_TAP_MS) {
    lastTapAt = 0
    zoomTo(scale.value > 1 ? 1 : DOUBLE_TAP_SCALE, event.clientX, event.clientY)
    haptics.select()
    return
  }
  lastTapAt = now

  startX = event.clientX
  startY = event.clientY
  startPanX = panX.value
  startPanY = panY.value
  axis = null
  isDragging.value = true
}

function onPointerMove(event: PointerEvent) {
  if (!isDragging.value || !pointers.has(event.pointerId)) return
  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY })

  if (pointers.size >= 2) {
    if (startDistance > 0) {
      const centre = pointerCentre()
      zoomTo(startScale * (pointerDistance() / startDistance), centre.x, centre.y)
    }
    return
  }

  const dx = event.clientX - startX
  const dy = event.clientY - startY

  if (scale.value > 1) {
    panX.value = startPanX + dx
    panY.value = startPanY + dy
    clampPan()
    return
  }

  if (!axis && Math.abs(dx) + Math.abs(dy) > AXIS_LOCK_SLOP) {
    axis = Math.abs(dx) > Math.abs(dy) ? 'horizontal' : 'vertical'
  }

  if (axis === 'vertical') {
    dismissY.value = dy
  } else if (axis === 'horizontal' && props.images.length > 1) {
    pageX.value = dx
  }
}

function onPointerUp(event: PointerEvent) {
  pointers.delete(event.pointerId)

  if (pointers.size >= 1) {
    // A finger lifted out of a pinch: re-anchor the remaining one so the image does not jump.
    const remaining = pointers.values().next().value
    if (remaining) {
      startX = remaining.x
      startY = remaining.y
      startPanX = panX.value
      startPanY = panY.value
    }
    return
  }

  isDragging.value = false

  if (axis === 'vertical' && Math.abs(dismissY.value) > DISMISS_DISTANCE) {
    close(true)
    return
  }

  if (axis === 'horizontal' && props.images.length > 1) {
    const width = stage.value?.clientWidth ?? 0
    if (Math.abs(pageX.value) > width * PAGE_FRACTION) {
      goTo(index.value + (pageX.value < 0 ? 1 : -1))
    }
  }

  dismissY.value = 0
  pageX.value = 0
  axis = null
}

function pointerDistance(): number {
  const [a, b] = [...pointers.values()]
  if (!a || !b) return 0
  return Math.hypot(a.x - b.x, a.y - b.y)
}

function pointerCentre(): { x: number, y: number } {
  const [a, b] = [...pointers.values()]
  if (!a || !b) return { x: 0, y: 0 }
  return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }
}

// --- Keyboard and focus ------------------------------------------------------

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    close()
    return
  }

  if (event.key === 'ArrowRight') {
    goTo(index.value + 1)
    return
  }

  if (event.key === 'ArrowLeft') {
    goTo(index.value - 1)
    return
  }

  if (event.key !== 'Tab') return

  // Focus trap: the lightbox covers the app, so Tab must not walk into what is behind it.
  const focusables = root.value?.querySelectorAll<HTMLElement>(
    'button:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
  )
  if (!focusables || focusables.length === 0) return

  const first = focusables[0]!
  const last = focusables[focusables.length - 1]!

  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

/**
 * Puts focus on the close button, or on the dialog itself if the chrome is not rendered.
 *
 * Found through the DOM rather than a component ref: a Nuxt UI `UButton`'s `$el` is not reliably
 * an element — its root is a `Primitive`, so it can be a comment anchor — and calling
 * `.querySelector` on one threw, which aborted the rest of the open sequence and left the
 * lightbox pinned invisible. The container is `tabindex="-1"` so there is always somewhere for
 * focus to land, and the keydown handler only fires while focus is inside.
 */
function focusDialog() {
  const dialog = root.value
  if (!dialog) return

  const button = dialog.querySelector<HTMLElement>('button')
  ;(button ?? dialog).focus()
}

onMounted(() => {
  const query = window.matchMedia('(prefers-reduced-motion: reduce)')
  reducedMotion.value = query.matches
  query.addEventListener('change', event => { reducedMotion.value = event.matches })
})

onBeforeUnmount(() => {
  document.body.style.overflow = ''
  clearTimers()
  pointers = new Map()
})
</script>
