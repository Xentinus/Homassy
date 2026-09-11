import { describe, expect, it } from 'vitest'
import {
  CARD_GAP,
  TOUR_STEPS,
  VIEWPORT_MARGIN,
  holeClipPath,
  nextVisibleStep,
  spotlightLayout,
  type TourStep
} from '~/utils/onboardingTour'

const VIEWPORT = { width: 390, height: 844 }
const CARD = { width: 352, height: 180 }

describe('TOUR_STEPS', () => {
  it('gives every step a distinct target', () => {
    const targets = TOUR_STEPS.map(step => step.target)

    // Two steps on the same element would spotlight the same box twice, which reads as
    // the tour having got stuck.
    expect(new Set(targets).size).toBe(targets.length)
  })

  it('gives every step a distinct i18n key', () => {
    const keys = TOUR_STEPS.map(step => step.key)
    expect(new Set(keys).size).toBe(keys.length)
  })
})

describe('nextVisibleStep', () => {
  const steps: TourStep[] = [
    { key: 'a', target: 'a' },
    { key: 'b', target: 'b' },
    { key: 'c', target: 'c', route: '/products' },
    { key: 'd', target: 'd' }
  ]

  it('returns the requested step when its target is on screen', () => {
    expect(nextVisibleStep(steps, 1, () => true)).toBe(1)
  })

  it('skips past a step whose target is missing', () => {
    expect(nextVisibleStep(steps, 0, target => target !== 'a')).toBe(1)
  })

  it('skips a whole run of missing targets', () => {
    const routeless: TourStep[] = [
      { key: 'a', target: 'a' },
      { key: 'b', target: 'b' },
      { key: 'c', target: 'c' },
      { key: 'd', target: 'd' }
    ]

    expect(nextVisibleStep(routeless, 0, target => target === 'd')).toBe(3)
  })

  it('never skips a step that declares a route', () => {
    // The element is not there *yet* — it arrives with the navigation, which has not
    // happened at the time this is asked.
    expect(nextVisibleStep(steps, 2, () => false)).toBe(2)
  })

  it('reports the end of the tour as -1', () => {
    expect(nextVisibleStep(steps, 4, () => true)).toBe(-1)
    expect(nextVisibleStep(steps, 3, () => false)).toBe(-1)
  })

  it('treats a negative index as the start', () => {
    expect(nextVisibleStep(steps, -3, () => true)).toBe(0)
  })
})

describe('spotlightLayout', () => {
  it('inflates the hole by the padding', () => {
    const { hole } = spotlightLayout({ x: 100, y: 200, width: 40, height: 40 }, VIEWPORT, CARD, 8)

    expect(hole).toEqual({ x: 92, y: 192, width: 56, height: 56 })
  })

  it('never lets the hole start off-screen', () => {
    // A target flush against the edge would otherwise punch a hole at a negative
    // coordinate, which clips to nothing on the leading edge.
    const { hole } = spotlightLayout({ x: 2, y: 1, width: 40, height: 40 }, VIEWPORT, CARD, 10)

    expect(hole.x).toBe(0)
    expect(hole.y).toBe(0)
  })

  it('puts the card below a target near the top', () => {
    const target = { x: 100, y: 80, width: 40, height: 40 }
    const { placement, cardTop } = spotlightLayout(target, VIEWPORT, CARD)

    expect(placement).toBe('below')
    expect(cardTop).toBeGreaterThan(target.y + target.height)
  })

  it('flips the card above a target near the bottom', () => {
    // The bottom nav is the everyday case: there is no room under it at all.
    const target = { x: 20, y: 760, width: 350, height: 64 }
    const { placement, cardTop } = spotlightLayout(target, VIEWPORT, CARD)

    expect(placement).toBe('above')
    expect(cardTop + CARD.height).toBeLessThanOrEqual(target.y - CARD_GAP + 1)
  })

  it('centres the card on the hole horizontally', () => {
    // Far enough from either edge that the clamp below does not come into it. The
    // padding is symmetric, so the hole's centre is the target's centre: 550.
    const { cardLeft } = spotlightLayout({ x: 500, y: 300, width: 100, height: 40 }, { width: 1200, height: 900 }, CARD)

    expect(cardLeft).toBe(550 - CARD.width / 2)
  })

  it('clamps the card inside the viewport horizontally', () => {
    const near = spotlightLayout({ x: 0, y: 300, width: 40, height: 40 }, VIEWPORT, CARD)
    expect(near.cardLeft).toBe(VIEWPORT_MARGIN)

    const far = spotlightLayout({ x: 380, y: 300, width: 10, height: 40 }, VIEWPORT, CARD)
    expect(far.cardLeft).toBe(VIEWPORT.width - CARD.width - VIEWPORT_MARGIN)
  })

  it('keeps the card on screen when it fits on neither side', () => {
    // A card taller than either gap has to overlap something; overlapping the hole is
    // better than hanging off the edge unreadable.
    const tall = { width: 352, height: 700 }
    const { cardTop } = spotlightLayout({ x: 20, y: 400, width: 350, height: 60 }, VIEWPORT, tall)

    expect(cardTop).toBeGreaterThanOrEqual(VIEWPORT_MARGIN)
    expect(cardTop + tall.height).toBeLessThanOrEqual(VIEWPORT.height)
  })
})

describe('holeClipPath', () => {
  /** The polygon's points, as `[x, y]` token pairs — `100%` kept verbatim. */
  const points = (path: string): [string, string][] =>
    path
      .replace(/^polygon\(evenodd,/, '')
      .replace(/\)$/, '')
      .split(',')
      .map((point) => {
        const [x, y] = point.trim().split(/\s+/)
        return [x!, y!] as [string, string]
      })

  it('declares the even-odd rule rather than relying on winding order', () => {
    const path = holeClipPath({ x: 10, y: 20, width: 30, height: 40 })

    expect(path.startsWith('polygon(evenodd,')).toBe(true)
  })

  it('draws no diagonal edge, so the scrim cannot dim a wedge of the screen', () => {
    // The bug this pins: `polygon()` is a single contour, so jumping from a viewport
    // corner to a hole corner draws a diagonal, and even-odd over that self-intersecting
    // shape dims a triangle rather than everything-but-the-hole. Every edge of a
    // viewport-with-a-hole path — the bridge in and out included — is axis-aligned.
    const path = holeClipPath({ x: 120, y: 300, width: 90, height: 60 })
    const pts = points(path)

    for (let i = 0; i < pts.length; i++) {
      const from = pts[i]!
      const to = pts[(i + 1) % pts.length]!

      expect(
        from[0] === to[0] || from[1] === to[1],
        `edge ${from.join(' ')} → ${to.join(' ')} is diagonal`
      ).toBe(true)
    }
  })

  it('reaches the hole along a bridge that encloses no area', () => {
    const pts = points(holeClipPath({ x: 120, y: 300, width: 90, height: 60 }))
    const bridge = pts.filter(([x, y]) => x === '0px' && y === '300px')

    // Traced out and back along the same line: two visits, zero width, invisible.
    expect(bridge).toHaveLength(2)
  })

  it('closes the hole ring before returning to the edge', () => {
    const pts = points(holeClipPath({ x: 120, y: 300, width: 90, height: 60 }))
    const corner = pts.filter(([x, y]) => x === '120px' && y === '300px')

    // The hole's top-left is both where the ring starts and where it ends.
    expect(corner).toHaveLength(2)
  })

  it('punches the hole at the rect it was given', () => {
    const path = holeClipPath({ x: 10, y: 20, width: 30, height: 40 })

    expect(path).toContain('10px 20px')
    expect(path).toContain('40px 60px')
  })

  it('emits the same number of points whatever the rect, so it can be animated', () => {
    const countPoints = (path: string) => path.split(',').length
    const a = holeClipPath({ x: 0, y: 0, width: 10, height: 10 })
    const b = holeClipPath({ x: 300, y: 700, width: 90, height: 60 })

    // `clip-path` only transitions between shapes with matching point counts.
    expect(countPoints(a)).toBe(countPoints(b))
  })
})
