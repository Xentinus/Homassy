/**
 * The family chat bubble's shared state (#145).
 *
 * The bubble is a single instance mounted in the authenticated layout, but three other places
 * need a say in it: the settings row that brings a dismissed bubble back, the panel that opens
 * out of it (#146), and the unread badge it carries (#149). So the flags live here, module-scoped,
 * rather than inside the component — a ref on the component instance would be unreachable from
 * a page, and remounting it on navigation would reset the dismissal the user just made.
 *
 * Nothing here is persisted except the bubble's position. A dismissal is deliberately
 * **for the session only**: the bubble is how the chat is reached at all, so "get out of my way
 * while I do this" must not turn into "the feature is gone", and the settings row exists as the
 * second way back rather than the only one.
 */
import { computed, readonly, ref } from 'vue'

/** Where the bubble sits, as a fraction of the viewport, so rotation and resize keep it sensible. */
export interface BubblePosition {
  /** Centre x, 0-1 of viewport width. */
  x: number
  /** Centre y, 0-1 of viewport height. */
  y: number
}

/** Per-device, not per-user: which corner suits your thumb is a property of the phone in your hand. */
const POSITION_STORAGE_KEY = 'homassy_chat_bubble_position'

const dismissedForSession = ref(false)
const panelOpen = ref(false)

/**
 * The bubble's live rectangle in viewport coordinates, published by the component on every
 * settle. The panel (#146) morphs out of exactly this, so it has to be the real one rather than
 * a guess from the stored percentages.
 */
const anchorRect = ref<DOMRect | null>(null)

export const useFamilyChatBubble = () => {
  /** Read the stored position, or null when there is none (or storage is unavailable). */
  const loadPosition = (): BubblePosition | null => {
    if (!import.meta.client) return null

    try {
      const raw = window.localStorage.getItem(POSITION_STORAGE_KEY)
      if (!raw) return null

      const parsed = JSON.parse(raw) as Partial<BubblePosition>
      if (typeof parsed?.x !== 'number' || typeof parsed?.y !== 'number') return null
      if (!Number.isFinite(parsed.x) || !Number.isFinite(parsed.y)) return null

      return { x: parsed.x, y: parsed.y }
    } catch {
      // A private window, cleared site data, or storage the browser refuses outright. The
      // bubble has a perfectly good default position; losing a remembered one is not an error
      // worth surfacing.
      return null
    }
  }

  const savePosition = (position: BubblePosition): void => {
    if (!import.meta.client) return

    try {
      window.localStorage.setItem(POSITION_STORAGE_KEY, JSON.stringify(position))
    } catch {
      // Same as above: the bubble still works, it just will not remember where it was left.
    }
  }

  const dismissForSession = (): void => {
    dismissedForSession.value = true
    panelOpen.value = false
  }

  const restore = (): void => {
    dismissedForSession.value = false
  }

  const openPanel = (): void => {
    panelOpen.value = true
  }

  const closePanel = (): void => {
    panelOpen.value = false
  }

  const togglePanel = (): void => {
    panelOpen.value = !panelOpen.value
  }

  const setAnchorRect = (rect: DOMRect | null): void => {
    anchorRect.value = rect
  }

  return {
    /** True while the user has dropped the bubble on the dismiss target this session. */
    isDismissed: readonly(dismissedForSession),
    /** Whether the chat panel is open (#146). */
    panelOpen,
    /** Where the bubble is right now, for the panel's morph origin. */
    anchorRect: computed(() => anchorRect.value),
    loadPosition,
    savePosition,
    dismissForSession,
    restore,
    openPanel,
    closePanel,
    togglePanel,
    setAnchorRect
  }
}
