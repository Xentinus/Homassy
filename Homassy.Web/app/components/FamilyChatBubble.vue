<template>
  <Teleport to="body">
    <!-- The dismiss target, only while a long-press has armed the gesture. Painted under the
         bubble so the bubble is still the thing being dragged over it. -->
    <Transition name="chat-bubble-fade">
      <div
        v-if="dismissArmed"
        class="fixed left-1/2 z-[54] flex h-20 w-20 -translate-x-1/2 items-center justify-center rounded-full border border-default bg-default/90 shadow-lg backdrop-blur transition-transform"
        :class="overDropTarget ? 'scale-125 text-error-500 border-error-400' : 'text-gray-400'"
        :style="{ bottom: `calc(2rem + ${safeAreaBottom}px)` }"
        aria-hidden="true"
      >
        <UIcon name="i-lucide-eye-off" class="h-8 w-8" />
      </div>
    </Transition>

    <Transition name="chat-bubble-fade">
      <button
        v-if="visible"
        ref="bubbleEl"
        data-chat-bubble
        type="button"
        :aria-label="bubbleLabel"
        class="fixed left-0 top-0 z-[55] flex h-14 w-14 items-center justify-center overflow-hidden rounded-full bg-primary-500 text-white shadow-xl ring-2 ring-white/80 dark:ring-gray-900/80 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary-300"
        :style="bubbleStyle"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerCancel"
        @keydown="onKeyDown"
        @focus="wake"
        @blur="scheduleIdle"
      >
        <img
          v-if="familyPicture"
          :src="familyPicture"
          :alt="familyName"
          class="h-full w-full object-cover"
          draggable="false"
        >
        <span v-else class="text-lg font-semibold leading-none">{{ initials }}</span>

        <!-- Somebody is typing while the panel is closed (#148). A pulse rather than a name list:
             the bubble is 56px, and "who" is what opening it answers. -->
        <span
          v-if="showTypingPulse"
          class="chat-bubble-pulse pointer-events-none absolute inset-0 rounded-full ring-2 ring-primary-300"
          aria-hidden="true"
        />

        <!-- How many other members have the chat open right now. Bottom-left, opposite the unread
             badge: the two answer different questions ("something happened" vs "somebody is here")
             and stacking them in one corner would read as one number. -->
        <!-- Emerald, not `success`: this app aliases the success colour to its mocha primary
             (`app.config.ts`), which is the bubble's own background - a "somebody is here" dot
             painted in it would be invisible. There is no semantic token for presence green. -->
        <span
          v-if="activeCount > 0"
          class="absolute -bottom-0.5 -left-0.5 flex h-[18px] min-w-[18px] items-center justify-center gap-0.5 rounded-full bg-emerald-500 px-1 shadow-md"
        >
          <span class="h-1.5 w-1.5 rounded-full bg-white/90" aria-hidden="true" />
          <span class="text-[10px] font-bold leading-none text-white tabular-nums">{{ activeCount }}</span>
        </span>

        <!-- Unread badge (#149), in the same visual language the bottom nav uses for the
             expiration and deadline counts - one badge vocabulary across the app. -->
        <span
          v-if="unreadCount > 0"
          class="absolute -right-0.5 -top-0.5 flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-error-500 px-1 shadow-md"
        >
          <!-- tabular-nums: the count changes under the mounted badge as messages arrive, and
               proportional digits would resize the pill on every change. -->
          <span class="text-[10px] font-bold leading-none text-white tabular-nums">
            {{ unreadCount > 99 ? '99+' : unreadCount }}
          </span>
        </span>
      </button>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

/**
 * The family chat head (#145): a draggable circle that floats over the app and stays where it is
 * dropped.
 *
 * It is mounted once, in `layouts/auth.vue`, as a sibling of the header and the bottom nav, so it
 * survives navigation — and it is teleported to `<body>` for the same reason `NavFab` is not:
 * `UApp` sets `isolation: isolate`, so a body-level element paints above the app's own stacking
 * context, which is what a floating surface needs.
 *
 * Gesture conventions are `useDrawerDragToClose`'s, deliberately: `touch-action: none`, a slop
 * threshold before the drag commits (so a tap is still a tap), live `transform` while dragging and
 * an explicit release easing rather than a CSS transition fighting the pointer. The `--bubble-*`
 * tokens supply the timings.
 *
 * Why it hides itself:
 *
 * - **No family** — there is no conversation to open, so there is nothing to float. The family
 *   fetch answers both questions at once: the picture to draw, and whether to draw at all.
 * - **A drawer or modal is open** — a chat head painted over a sheet is the one place a floating
 *   surface is always wrong. See `useOverlayPresence`.
 * - **Dismissed this session** — long-press, drag to the target at the bottom, drop. It comes back
 *   from the settings row, and it always comes back on the next launch: the bubble is how the chat
 *   is reached, so it must not be losable for good.
 */

const { t } = useI18n()
const { getFamily } = useFamilyApi()
const haptics = useHaptics()
const { overlayOpen } = useOverlayPresence()
// Only the typing set is read here; the bubble never joins the hub itself. While the panel has
// never been opened this is simply empty, which is the correct "nothing to pulse about".
const {
  typingMembers,
  activeMembers,
  unreadCount,
  refreshUnreadCount,
  join: joinChat,
  leave: leaveChat
} = useFamilyChat()
const {
  isDismissed,
  panelOpen,
  loadPosition,
  savePosition,
  dismissForSession,
  togglePanel,
  setAnchorRect
} = useFamilyChatBubble()

/** Diameter in px. Mirrors the `h-14 w-14` above; the clamp needs it as a number. */
const BUBBLE_SIZE = 56
/** Smallest gap between the bubble and the viewport edge, on top of any safe-area inset. */
const EDGE_MARGIN = 12
/** Keeps the bubble clear of the bottom nav when it is parked low. */
const NAV_CLEARANCE = 96
/**
 * Keeps it clear of the persistent header when parked high.
 *
 * The bubble is teleported above everything, so without this it can be dropped on top of the
 * header's own controls - and the corner it snaps to when the panel opens is a top corner half the
 * time.
 */
const HEADER_CLEARANCE = 96
/** Movement before a press becomes a drag. Below it the gesture is still a tap. */
const DRAG_SLOP = 6
/** How long a press has to be held before the dismiss target appears. */
const LONG_PRESS_MS = 450
/** Quiet time before the bubble fades back and tucks itself against the edge. */
const IDLE_MS = 4000
/** How far the idle bubble slides off the edge. */
const IDLE_TUCK = 14
/** Opacity of the idle bubble — still visible, no longer competing with the page. */
const IDLE_OPACITY = 0.6
/** Radius around the drop target's centre that counts as "over it". */
const DROP_RADIUS = 72
/** How far one arrow key moves the bubble. */
const KEY_STEP = 24

const bubbleEl = ref<HTMLElement | null>(null)

const familyName = ref('')
const familyPicture = ref<string | null>(null)
const hasFamily = ref(false)

// Top-left corner in viewport pixels. Seeded off-screen so the first paint cannot flash at 0,0
// before the real position is measured in onMounted.
const x = ref(-9999)
const y = ref(-9999)
const placed = ref(false)

const dragging = ref(false)
const settling = ref(false)
const idle = ref(false)
const dismissArmed = ref(false)
const overDropTarget = ref(false)
const safeAreaBottom = ref(0)

let activePointerId: number | null = null
let pointerStartX = 0
let pointerStartY = 0
let grabOffsetX = 0
let grabOffsetY = 0
let engaged = false
let longPressTimer: ReturnType<typeof setTimeout> | null = null
let idleTimer: ReturnType<typeof setTimeout> | null = null

const prefersReducedMotion = (): boolean =>
  import.meta.client && window.matchMedia('(prefers-reduced-motion: reduce)').matches

const visible = computed(() =>
  hasFamily.value
  && placed.value
  && !isDismissed.value
  // A drawer or modal is open - unless it is the chat panel itself, which on mobile *is* a
  // drawer. The bubble is that panel's handle and the anchor its morph runs out of, so hiding
  // it there would hide the thing the panel came from (#146).
  && (!overlayOpen.value || panelOpen.value)
)

/**
 * The typing pulse is only for the closed panel: with the panel open the dots above the composer
 * say the same thing, with names attached.
 */
const showTypingPulse = computed(() => !panelOpen.value && typingMembers.value.length > 0)

/** How many *other* members have the chat open. Your own reading is not news to you. */
const activeCount = computed(() => activeMembers.value.length)

/**
 * The label carries the count, because the badge is a coloured dot to a screen reader otherwise.
 */
const bubbleLabel = computed(() => activeCount.value > 0
  ? t('familyChat.bubble.openWithActive', { family: familyName.value, count: activeCount.value })
  : t('familyChat.bubble.open', { family: familyName.value }))

const initials = computed(() => {
  const words = familyName.value.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '?'
  return words.slice(0, 2).map(w => w[0]!.toUpperCase()).join('')
})

/**
 * Which edge the bubble is nearer, which is both where it snaps on release and which way it
 * tucks when idle.
 */
const nearestEdge = computed<'left' | 'right'>(() => {
  if (!import.meta.client) return 'right'
  return x.value + BUBBLE_SIZE / 2 < window.innerWidth / 2 ? 'left' : 'right'
})

const idleShift = computed(() => {
  if (!idle.value || dragging.value || prefersReducedMotion()) return 0
  return nearestEdge.value === 'left' ? -IDLE_TUCK : IDLE_TUCK
})

const bubbleStyle = computed(() => ({
  transform: `translate3d(${x.value + idleShift.value}px, ${y.value}px, 0)`,
  opacity: idle.value && !dragging.value ? IDLE_OPACITY : 1,
  touchAction: 'none',
  transition: dragging.value
    ? 'opacity var(--bubble-out) ease'
    : `transform ${settling.value ? 'var(--bubble-move)' : 'var(--bubble-in)'} var(--bubble-ease-out), opacity var(--bubble-in) ease`
}))

// --- Geometry ---

/** The safe-area insets, read off a probe so `env()` is resolved by the browser rather than guessed. */
const readSafeAreaInsets = (): { top: number, right: number, bottom: number, left: number } => {
  const probe = document.createElement('div')
  probe.style.cssText = [
    'position:fixed',
    'visibility:hidden',
    'pointer-events:none',
    'top:0;left:0;width:0;height:0',
    'padding-top:env(safe-area-inset-top, 0px)',
    'padding-right:env(safe-area-inset-right, 0px)',
    'padding-bottom:env(safe-area-inset-bottom, 0px)',
    'padding-left:env(safe-area-inset-left, 0px)'
  ].join(';')

  document.body.appendChild(probe)
  const style = window.getComputedStyle(probe)
  const insets = {
    top: Number.parseFloat(style.paddingTop) || 0,
    right: Number.parseFloat(style.paddingRight) || 0,
    bottom: Number.parseFloat(style.paddingBottom) || 0,
    left: Number.parseFloat(style.paddingLeft) || 0
  }
  probe.remove()

  return insets
}

/** Pins a position inside the viewport, safe areas and the bottom nav included. */
const clamp = (nextX: number, nextY: number): { x: number, y: number } => {
  const insets = readSafeAreaInsets()
  const minX = EDGE_MARGIN + insets.left
  const maxX = window.innerWidth - BUBBLE_SIZE - EDGE_MARGIN - insets.right
  // The header is sticky at the top and the nav is fixed at the bottom; the bubble is allowed
  // over neither, so its travel is the strip between them.
  const minY = HEADER_CLEARANCE + insets.top
  const maxY = window.innerHeight - BUBBLE_SIZE - NAV_CLEARANCE - insets.bottom

  return {
    x: Math.min(Math.max(nextX, minX), Math.max(minX, maxX)),
    y: Math.min(Math.max(nextY, minY), Math.max(minY, maxY))
  }
}

/** Stored as fractions of the viewport, so a rotation or a resized window keeps it sensible. */
const toFraction = (px: number, py: number) => ({
  x: (px + BUBBLE_SIZE / 2) / window.innerWidth,
  y: (py + BUBBLE_SIZE / 2) / window.innerHeight
})

const fromFraction = (fx: number, fy: number) => ({
  x: fx * window.innerWidth - BUBBLE_SIZE / 2,
  y: fy * window.innerHeight - BUBBLE_SIZE / 2
})

const publishAnchor = (): void => {
  setAnchorRect(bubbleEl.value?.getBoundingClientRect() ?? null)
}



// --- Idle ---

const scheduleIdle = (): void => {
  if (idleTimer) clearTimeout(idleTimer)
  idleTimer = setTimeout(() => { idle.value = true }, IDLE_MS)
}

/** Back to full opacity and full position — any interaction at all resets the idle timer. */
const wake = (): void => {
  idle.value = false
  scheduleIdle()
}



// --- Pointer gesture ---

const onPointerDown = (event: PointerEvent): void => {
  if (activePointerId !== null) return

  activePointerId = event.pointerId
  pointerStartX = event.clientX
  pointerStartY = event.clientY
  grabOffsetX = event.clientX - x.value
  grabOffsetY = event.clientY - y.value
  engaged = false
  settling.value = false
  wake()

  try {
    bubbleEl.value?.setPointerCapture(event.pointerId)
  } catch {
    // The pointer can already be gone by the time we ask for it; the move/up handlers still
    // arrive on the element, so this is not worth failing the gesture over.
  }

  longPressTimer = setTimeout(() => {
    dismissArmed.value = true
    haptics.select()
  }, LONG_PRESS_MS)
}

const onPointerMove = (event: PointerEvent): void => {
  if (event.pointerId !== activePointerId) return

  const dx = event.clientX - pointerStartX
  const dy = event.clientY - pointerStartY

  if (!engaged) {
    if (Math.hypot(dx, dy) < DRAG_SLOP) return
    engaged = true
    dragging.value = true
    // Moving is not holding still: a drag that started as a press should not also arm the
    // dismiss target unless the press had already earned it.
    if (longPressTimer) {
      clearTimeout(longPressTimer)
      longPressTimer = null
    }
  }

  const next = clamp(event.clientX - grabOffsetX, event.clientY - grabOffsetY)
  x.value = next.x
  y.value = next.y

  if (dismissArmed.value) {
    const wasOver = overDropTarget.value
    overDropTarget.value = isOverDropTarget()
    if (overDropTarget.value && !wasOver) haptics.select()
  }
}

const onPointerUp = (event: PointerEvent): void => {
  if (event.pointerId !== activePointerId) return

  const wasEngaged = engaged
  const wasArmed = dismissArmed.value
  const droppedOnTarget = wasArmed && overDropTarget.value

  releasePointer(event.pointerId)

  if (droppedOnTarget) {
    haptics.warning()
    dismissForSession()
    return
  }

  if (!wasEngaged) {
    // A press that never became a drag is a tap - unless it was held long enough to arm the
    // dismiss gesture, in which case letting go is how you back out of it.
    if (!wasArmed) {
      haptics.tap()
      togglePanel()
    }
    return
  }

  snapToEdge()
}

const onPointerCancel = (event: PointerEvent): void => {
  if (event.pointerId !== activePointerId) return

  const wasEngaged = engaged
  releasePointer(event.pointerId)
  if (wasEngaged) snapToEdge()
}

const releasePointer = (pointerId: number): void => {
  if (longPressTimer) {
    clearTimeout(longPressTimer)
    longPressTimer = null
  }

  try {
    bubbleEl.value?.releasePointerCapture(pointerId)
  } catch {
    // Already released with the pointer itself.
  }

  activePointerId = null
  engaged = false
  dragging.value = false
  dismissArmed.value = false
  overDropTarget.value = false
  scheduleIdle()
}

const isOverDropTarget = (): boolean => {
  const targetX = window.innerWidth / 2
  const targetY = window.innerHeight - safeAreaBottom.value - 32 - 40
  const centreX = x.value + BUBBLE_SIZE / 2
  const centreY = y.value + BUBBLE_SIZE / 2

  return Math.hypot(centreX - targetX, centreY - targetY) < DROP_RADIUS
}

/**
 * Moves the bubble to its nearest corner, which is where the panel hangs off it.
 *
 * Run as the panel opens. A panel anchored to a bubble parked halfway down the screen would have
 * room for a conversation neither above nor below it; from a corner there is one long side to grow
 * into, and the direction is decided by which half the bubble is in. The position is saved, so the
 * bubble stays where the chat left it rather than springing back.
 */
const snapToCorner = (): void => {
  const insets = readSafeAreaInsets()
  const inTopHalf = y.value + BUBBLE_SIZE / 2 < window.innerHeight / 2

  const targetX = nearestEdge.value === 'left'
    ? EDGE_MARGIN + insets.left
    : window.innerWidth - BUBBLE_SIZE - EDGE_MARGIN - insets.right

  const targetY = inTopHalf
    ? HEADER_CLEARANCE + insets.top
    : window.innerHeight - BUBBLE_SIZE - NAV_CLEARANCE - insets.bottom

  const settled = clamp(targetX, targetY)
  const alreadyThere = Math.abs(settled.x - x.value) < 1 && Math.abs(settled.y - y.value) < 1

  settling.value = !alreadyThere && !prefersReducedMotion()
  x.value = settled.x
  y.value = settled.y
  wake()

  savePosition(toFraction(settled.x, settled.y))

  // The anchor is published as the corner the bubble is *going* to, not as where it is now.
  // `publishAnchor` measures the element, and during the slide that measurement is a moving
  // target - the panel would open against the old position and then jump. Both arrive at the same
  // place within the same transition, so the panel grows out of the corner while the bubble slides
  // into it.
  setAnchorRect(new DOMRect(settled.x, settled.y, BUBBLE_SIZE, BUBBLE_SIZE))

  if (settling.value) {
    window.setTimeout(() => { settling.value = false; publishAnchor() }, 300)
  }
}

/** Settles the bubble against whichever side it was left nearer, and remembers where that is. */
const snapToEdge = (): void => {
  const insets = readSafeAreaInsets()
  const targetX = nearestEdge.value === 'left'
    ? EDGE_MARGIN + insets.left
    : window.innerWidth - BUBBLE_SIZE - EDGE_MARGIN - insets.right

  const settled = clamp(targetX, y.value)

  if (prefersReducedMotion()) {
    // No easing to run: the bubble is simply where it now is.
    settling.value = false
    x.value = settled.x
    y.value = settled.y
  } else {
    settling.value = true
    x.value = settled.x
    y.value = settled.y
    window.setTimeout(() => { settling.value = false; publishAnchor() }, 300)
  }

  haptics.select()
  savePosition(toFraction(settled.x, settled.y))
  publishAnchor()
}



// --- Keyboard ---

/**
 * Arrow keys nudge the focused bubble, so a keyboard user can move it out of the way of whatever
 * it is covering. Enter and Space are left to the button itself.
 */
const onKeyDown = (event: KeyboardEvent): void => {
  const deltas: Record<string, [number, number]> = {
    ArrowLeft: [-KEY_STEP, 0],
    ArrowRight: [KEY_STEP, 0],
    ArrowUp: [0, -KEY_STEP],
    ArrowDown: [0, KEY_STEP]
  }

  const delta = deltas[event.key]
  if (!delta) return

  event.preventDefault()
  wake()

  const next = clamp(x.value + delta[0], y.value + delta[1])
  x.value = next.x
  y.value = next.y
  savePosition(toFraction(next.x, next.y))
  publishAnchor()
}



// --- Lifecycle ---

/** Restores the stored position, or parks the bubble above the nav on the right. */
const place = (): void => {
  const stored = loadPosition()
  const target = stored
    ? fromFraction(stored.x, stored.y)
    : {
        x: window.innerWidth - BUBBLE_SIZE - EDGE_MARGIN,
        y: window.innerHeight - BUBBLE_SIZE - NAV_CLEARANCE - 24
      }

  const clamped = clamp(target.x, target.y)
  x.value = clamped.x
  y.value = clamped.y
  placed.value = true
}

/**
 * A resize, a rotation, or the on-screen keyboard opening can all leave a stored position
 * outside the viewport, so the position is re-clamped rather than trusted.
 */
const onViewportChange = (): void => {
  safeAreaBottom.value = readSafeAreaInsets().bottom
  const next = clamp(x.value, y.value)
  x.value = next.x
  y.value = next.y
  publishAnchor()
}

const loadFamily = async (): Promise<void> => {
  try {
    const response = await getFamily()
    if (response.success && response.data) {
      hasFamily.value = true
      familyName.value = response.data.name
      familyPicture.value = response.data.familyPictureBase64
        ? `data:image/jpeg;base64,${response.data.familyPictureBase64}`
        : null
    } else {
      // No family is the ordinary case for a new account, not a failure: there is simply no
      // conversation to float a bubble for. `getFamily` already opts out of the error toast.
      hasFamily.value = false
    }
  } catch {
    hasFamily.value = false
  }
}

onMounted(async () => {
  safeAreaBottom.value = readSafeAreaInsets().bottom
  place()
  scheduleIdle()

  window.addEventListener('resize', onViewportChange)
  window.addEventListener('orientationchange', onViewportChange)

  await loadFamily()
  publishAnchor()

  // The badge is the only thing that says anything happened before the panel is ever opened, so
  // the count is fetched as soon as there is a family to have one (#149).
  if (!hasFamily.value) return

  await refreshUnreadCount()

  // Join the hub group from the bubble, not from the panel. Everything the bubble shows about the
  // conversation - who is watching, whether somebody is typing, a message arriving - is a live
  // event, and a client outside the group receives none of them. Joining is not "watching": the
  // attention flag that suppresses notifications is still only set while the panel is open and
  // visible.
  await joinChat()
})

onBeforeUnmount(() => {
  // The bubble owns the group membership, so it is what gives it up - on logout, or when the
  // authenticated layout goes away.
  void leaveChat()

  if (idleTimer) clearTimeout(idleTimer)
  if (longPressTimer) clearTimeout(longPressTimer)
  window.removeEventListener('resize', onViewportChange)
  window.removeEventListener('orientationchange', onViewportChange)
  setAnchorRect(null)
})

// The panel morphs out of the bubble's live position (#146), so the anchor has to be current the
// moment it becomes visible again rather than whatever it was when it was hidden.
watch(visible, (isVisible) => {
  if (isVisible) requestAnimationFrame(publishAnchor)
})

// Opening the chat takes the bubble to its nearest corner, which is where the panel hangs off it.
watch(panelOpen, (isOpen) => {
  if (isOpen && placed.value) snapToCorner()
})


</script>

<style scoped>
.chat-bubble-fade-enter-active,
.chat-bubble-fade-leave-active {
  transition: opacity var(--bubble-out) ease, transform var(--bubble-out) var(--bubble-ease-pop);
}

.chat-bubble-fade-enter-from,
.chat-bubble-fade-leave-to {
  opacity: 0;
  transform: scale(0.7);
}

/* Somebody is typing, panel closed (#148). */
.chat-bubble-pulse {
  animation: chat-bubble-pulse 1.6s ease-in-out infinite;
}

@keyframes chat-bubble-pulse {
  0%, 100% {
    opacity: 0.15;
    transform: scale(1);
  }
  50% {
    opacity: 0.8;
    transform: scale(1.08);
  }
}

@media (prefers-reduced-motion: reduce) {
  .chat-bubble-fade-enter-active,
  .chat-bubble-fade-leave-active {
    transition: opacity var(--bubble-out) ease;
  }

  .chat-bubble-fade-enter-from,
  .chat-bubble-fade-leave-to {
    transform: none;
  }

  /* A steady ring rather than a pulse: the ring still says "something is happening", without the
     movement the user asked not to see. */
  .chat-bubble-pulse {
    animation: none;
    opacity: 0.6;
  }
}
</style>
