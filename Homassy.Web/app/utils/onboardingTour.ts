/**
 * The first-run spotlight tour's step list and its two pieces of pure logic (#98):
 * which step comes next when a target is not on screen, and where the cut-out and the
 * tooltip card go. Both live here rather than in the composable so they can be unit
 * tested without a DOM — see `tests/unit/onboardingTour.spec.ts`.
 */

export interface TourStep {
  /**
   * Suffix under `onboarding.steps` in the locale files: `<key>.title` and
   * `<key>.description`.
   */
  key: string
  /**
   * The `data-tour` attribute value of the element to spotlight. An attribute, not a
   * CSS path, so the tour does not break the next time a wrapper `div` is added or a
   * class is renamed.
   */
  target: string
  /**
   * Navigate here before showing the step, when not already there. Only the two
   * page-specific steps need it; the rest point at the persistent bottom-nav chrome,
   * which is on screen wherever the user happens to be.
   */
  route?: string
  /** Extra room around the cut-out, in px. Defaults to `DEFAULT_PADDING`. */
  padding?: number
}

/** Breathing room between the highlighted element's box and the edge of the hole. */
export const DEFAULT_PADDING = 8

/** Gap between the hole and the tooltip card. */
export const CARD_GAP = 16

/** Smallest margin the card keeps from the viewport edges. */
export const VIEWPORT_MARGIN = 12

/**
 * The tour, in order.
 *
 * It walks the app's own chrome rather than a slideshow: everything a new user has to
 * be told about is either in the persistent bottom nav (which is on screen everywhere)
 * or on the inventory page, so exactly one navigation happens mid-tour. A step whose
 * target is not in the DOM is skipped rather than shown pointing at nothing — the
 * camera button, for one, only exists on a device that has a camera.
 */
export const TOUR_STEPS: readonly TourStep[] = [
  // The whole bar first, so the four destinations are named before any single one of
  // them is pointed at.
  { key: 'nav', target: 'nav-bar', padding: 6 },
  { key: 'inventory', target: 'nav-products' },
  // The FAB is context-dependent and does not exist on the calendar, so this is the
  // step that moves the user - and it moves them to the page the next step needs too.
  { key: 'fab', target: 'fab', route: '/products', padding: 10 },
  { key: 'scanner', target: 'scanner' },
  { key: 'lists', target: 'nav-shopping-lists' },
  { key: 'profile', target: 'nav-profile' }
]

/**
 * Index of the first step at or after `fromIndex` whose target this run can actually
 * show, or `-1` when the tour is finished.
 *
 * A step that declares a `route` is always reachable — it will be on screen once the
 * navigation lands, which has not happened yet at the time this is asked. Only a step
 * expected on the *current* page is tested, and a missing target there means skip.
 */
export function nextVisibleStep(
  steps: readonly TourStep[],
  fromIndex: number,
  isPresent: (target: string) => boolean
): number {
  for (let i = Math.max(0, fromIndex); i < steps.length; i++) {
    const step = steps[i]
    if (!step) continue
    if (step.route || isPresent(step.target)) return i
  }
  return -1
}

export interface Rect { x: number, y: number, width: number, height: number }
export interface Viewport { width: number, height: number }

export interface SpotlightLayout {
  /** The cut-out, inflated by the step's padding and clamped to the viewport. */
  hole: Rect
  /** Where the tooltip card's top edge goes. */
  cardTop: number
  /** Where the tooltip card's left edge goes. */
  cardLeft: number
  /** Which side of the hole the card ended up on. Drives the little pointer arrow. */
  placement: 'above' | 'below'
}

/**
 * Places the cut-out and the tooltip card for one step.
 *
 * The card prefers to sit *below* the hole — reading order, and the highlighted thing
 * stays above the finger that is about to tap "Next". It flips above when there is not
 * enough room below, which is the common case for the bottom nav, and when neither side
 * fits it takes the roomier one and is clamped into the viewport rather than being
 * allowed to hang off the edge. A card that has to overlap the hole is better than a
 * card the user cannot read.
 */
export function spotlightLayout(
  target: Rect,
  viewport: Viewport,
  cardSize: { width: number, height: number },
  padding = DEFAULT_PADDING
): SpotlightLayout {
  const hole: Rect = {
    x: Math.max(0, target.x - padding),
    y: Math.max(0, target.y - padding),
    width: Math.min(viewport.width, target.width + padding * 2),
    height: Math.min(viewport.height, target.height + padding * 2)
  }

  const roomBelow = viewport.height - (hole.y + hole.height) - CARD_GAP - VIEWPORT_MARGIN
  const roomAbove = hole.y - CARD_GAP - VIEWPORT_MARGIN

  const placement: 'above' | 'below'
    = cardSize.height <= roomBelow
      ? 'below'
      : cardSize.height <= roomAbove
        ? 'above'
        : roomBelow >= roomAbove ? 'below' : 'above'

  const unclampedTop = placement === 'below'
    ? hole.y + hole.height + CARD_GAP
    : hole.y - CARD_GAP - cardSize.height

  const cardTop = clamp(
    unclampedTop,
    VIEWPORT_MARGIN,
    Math.max(VIEWPORT_MARGIN, viewport.height - cardSize.height - VIEWPORT_MARGIN)
  )

  const cardLeft = clamp(
    hole.x + hole.width / 2 - cardSize.width / 2,
    VIEWPORT_MARGIN,
    Math.max(VIEWPORT_MARGIN, viewport.width - cardSize.width - VIEWPORT_MARGIN)
  )

  return { hole, cardTop, cardLeft, placement }
}

/**
 * The `clip-path` for the dim layer: the whole viewport with the hole punched out.
 *
 * `polygon()` draws **one** closed contour, which is the whole difficulty here. Listing
 * the viewport's four corners and then the hole's four does not give an outer ring plus
 * an inner one; it gives a single path that runs diagonally from the viewport's
 * bottom-left corner to the hole and back out to the top-left — and even-odd over that
 * self-intersecting shape dims a wedge of the screen instead of everything-but-the-hole.
 * That was the visible bug: the scrim split the app diagonally into a dark half and a
 * lit half, with the highlighted element nowhere near the lit part (#153).
 *
 * So the hole is reached along a *degenerate bridge* instead: straight in along the
 * viewport's left edge at the hole's top, round the hole, and back out along the same
 * line. Traced twice in opposite directions, the bridge encloses no area and cannot be
 * seen. The hole is then wound the opposite way to the viewport rectangle, so `nonzero`
 * and `evenodd` agree on it — `evenodd` stays declared because it is the rule that makes
 * the intent explicit, not because the shape depends on it.
 */
export function holeClipPath(hole: Rect): string {
  const right = hole.x + hole.width
  const bottom = hole.y + hole.height

  // Out along the left edge to the hole's top-left, round the hole, and back out the
  // way we came in. The bridge is traced twice, so it encloses no area and is invisible.
  return 'polygon(evenodd,'
    + ' 0px 0px, 100% 0px, 100% 100%, 0px 100%,'
    + ` 0px ${hole.y}px, ${hole.x}px ${hole.y}px,`
    + ` ${hole.x}px ${bottom}px, ${right}px ${bottom}px, ${right}px ${hole.y}px,`
    + ` ${hole.x}px ${hole.y}px, 0px ${hole.y}px)`
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max)
}
