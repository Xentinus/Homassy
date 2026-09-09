import { describe, expect, it } from 'vitest'
import { MEMBER_COLORS } from '~/utils/memberColors'
import { seriesColor, seriesColors } from '~/utils/chart/series'

describe('seriesColor', () => {
  it('only ever returns a colour from the curated palette, never a new hex', () => {
    const palette = new Set(MEMBER_COLORS.map(c => c.light))
    for (let i = 0; i < 30; i++) {
      expect(palette.has(seriesColor(`series-${i}`, i).light)).toBe(true)
    }
  })

  it('is stable for the same key and index', () => {
    expect(seriesColor('Aldi', 0)).toEqual(seriesColor('Aldi', 0))
  })
})

describe('seriesColors', () => {
  it('gives distinct colours to distinct series while the palette lasts', () => {
    const keys = ['Aldi', 'Lidl', 'Spar', 'Tesco']
    const assigned = seriesColors(keys)
    expect(new Set([...assigned.values()].map(c => c.light)).size).toBe(keys.length)
  })

  it('wraps rather than running out past the palette length', () => {
    const keys = Array.from({ length: MEMBER_COLORS.length + 3 }, (_, i) => `s${i}`)
    expect(seriesColors(keys).size).toBe(keys.length)
  })
})
