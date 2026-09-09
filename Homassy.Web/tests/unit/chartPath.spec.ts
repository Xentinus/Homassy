import { describe, expect, it } from 'vitest'
import { arcPath, areaPath, linePath, polarPoint } from '~/utils/chart/path'

describe('linePath', () => {
  it('emits a move followed by line commands', () => {
    expect(linePath([{ x: 0, y: 0 }, { x: 10, y: 5 }])).toBe('M0,0L10,5')
  })

  it('returns an empty string for no points, so the <path> renders nothing', () => {
    expect(linePath([])).toBe('')
  })

  it('emits a single move for one point rather than a malformed path', () => {
    expect(linePath([{ x: 3, y: 4 }])).toBe('M3,4')
  })
})

describe('areaPath', () => {
  it('closes the shape down to the baseline', () => {
    const d = areaPath([{ x: 0, y: 10 }, { x: 10, y: 0 }], 20)
    expect(d.startsWith('M0,10')).toBe(true)
    expect(d.endsWith('Z')).toBe(true)
    expect(d).toContain('20')
  })

  it('returns an empty string for no points', () => {
    expect(areaPath([], 20)).toBe('')
  })
})

describe('arcPath', () => {
  it('produces a donut segment with both radii and the large-arc flag set past half a turn', () => {
    const d = arcPath(50, 50, 40, 25, 0, Math.PI * 1.5)
    expect(d).toContain('A40,40')
    expect(d).toContain('A25,25')
    expect(d).toContain('Z')
    expect(d).toMatch(/A40,40 0 1 1/)
  })

  it('clears the large-arc flag below half a turn', () => {
    const d = arcPath(50, 50, 40, 25, 0, Math.PI * 0.5)
    expect(d).toMatch(/A40,40 0 0 1/)
  })

  it('renders a full circle as a closed two-arc shape rather than collapsing to nothing', () => {
    const d = arcPath(50, 50, 40, 25, 0, Math.PI * 2)
    expect(d.length).toBeGreaterThan(0)
    expect(d).toContain('A40,40')
  })
})

describe('polarPoint', () => {
  it('puts angle 0 at twelve o clock, which is where a donut should start', () => {
    const p = polarPoint(0, 0, 10, 0)
    expect(p.x).toBeCloseTo(0)
    expect(p.y).toBeCloseTo(-10)
  })
})
