import { describe, expect, it } from 'vitest'
import {
  MEMBER_COLORS,
  isHexColor,
  isMemberColorKey,
  pickMemberColor,
  resolveMemberColor
} from '~/utils/memberColors'

const ID_A = '3f2504e0-4f89-11d3-9a0c-0305e82c3301'
const ID_B = 'b1f0c6a2-8d55-4c1e-9a2b-7e4d1f6c8a90'

/**
 * Independent WCAG contrast-ratio check for the "a custom hex override" specs below.
 *
 * Deliberately duplicated here rather than imported from `memberColors.ts`: these are the
 * assertions meant to catch a broken solver in that file, so they cannot share a luminance bug
 * with the code under test — if `relativeLuminance` there were wrong, importing it here would let
 * a broken solver pass anyway.
 */
const LIGHT_CARD_BG = '#ffffff'
const DARK_CARD_BG = '#0f172a' // Tailwind slate-900 — @nuxt/ui's --ui-bg in dark mode (neutral: 'slate')
const MIN_CONTRAST = 3

function channelLuminance(value: number): number {
  const c = value / 255
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

function relativeLuminance(hex: string): number {
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  return 0.2126 * channelLuminance(r) + 0.7152 * channelLuminance(g) + 0.0722 * channelLuminance(b)
}

function contrastRatio(hexA: string, hexB: string): number {
  const l1 = relativeLuminance(hexA)
  const l2 = relativeLuminance(hexB)
  const lighter = Math.max(l1, l2)
  const darker = Math.min(l1, l2)
  return (lighter + 0.05) / (darker + 0.05)
}

/** Hue in degrees (0 for a grey, where hue is meaningless) — independent of memberColors.ts. */
function hueOf(hex: string): number {
  const r = parseInt(hex.slice(1, 3), 16) / 255
  const g = parseInt(hex.slice(3, 5), 16) / 255
  const b = parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  if (max === min) return 0
  const d = max - min
  let h: number
  switch (max) {
    case r: h = (g - b) / d + (g < b ? 6 : 0); break
    case g: h = (b - r) / d + 2; break
    default: h = (r - g) / d + 4
  }
  return Math.round(h * 60)
}

describe('pickMemberColor', () => {
  it('always returns a colour from the curated palette', () => {
    const keys = MEMBER_COLORS.map(c => c.key)
    expect(keys).toContain(pickMemberColor(ID_A).key)
    expect(keys).toContain(pickMemberColor('').key)
    expect(keys).toContain(pickMemberColor('not-a-guid').key)
  })

  it('is case- and dash-insensitive, so the same user never changes colour on formatting', () => {
    expect(pickMemberColor(ID_A.toUpperCase()).key).toBe(pickMemberColor(ID_A).key)
    expect(pickMemberColor(ID_A.replace(/-/g, '')).key).toBe(pickMemberColor(ID_A).key)
  })

  it('spreads a realistic family across distinct colours', () => {
    const ids = [ID_A, ID_B, '00000000-0000-0000-0000-000000000001', '7c9e6679-7425-40de-944b-e07fc1f90ae7']
    const keys = new Set(ids.map(id => pickMemberColor(id).key))
    expect(keys.size).toBe(ids.length)
  })
})

describe('resolveMemberColor', () => {
  it('honours a valid palette-key override', () => {
    expect(resolveMemberColor(ID_A, 'teal').key).toBe('teal')
  })

  it('falls back to the deterministic pick for a null, empty or invalid override', () => {
    const fallback = pickMemberColor(ID_A).key
    expect(resolveMemberColor(ID_A, null).key).toBe(fallback)
    expect(resolveMemberColor(ID_A, '').key).toBe(fallback)
    expect(resolveMemberColor(ID_A, 'chartreuse').key).toBe(fallback)
    expect(resolveMemberColor(ID_A, '#abc').key).toBe(fallback)
    expect(resolveMemberColor(ID_A, 'rgb(0,0,0)').key).toBe(fallback)
  })

  describe('a custom hex override', () => {
    it('returns key "custom" and preserves the picked hue in both variants', () => {
      const resolved = resolveMemberColor(ID_A, '#3366cc')
      expect(resolved.key).toBe('custom')
      expect(hueOf(resolved.light)).toBe(hueOf('#3366cc'))
      expect(hueOf(resolved.dark)).toBe(hueOf('#3366cc'))
    })

    it('darkens a very light pick enough to clear 3:1 on white, leaving the dark variant alone', () => {
      const resolved = resolveMemberColor(ID_A, '#ffff00')
      expect(contrastRatio(resolved.light, LIGHT_CARD_BG)).toBeGreaterThanOrEqual(MIN_CONTRAST)
      expect(resolved.dark).toBe('#ffff00')
    })

    it('lightens a very dark pick enough to clear 3:1 on slate-900, leaving the light variant alone', () => {
      const resolved = resolveMemberColor(ID_A, '#000080')
      expect(resolved.light).toBe('#000080')
      expect(contrastRatio(resolved.dark, DARK_CARD_BG)).toBeGreaterThanOrEqual(MIN_CONTRAST)
    })

    it('leaves a mid-range pick unchanged in both variants when it already clears both bounds', () => {
      const resolved = resolveMemberColor(ID_A, '#4a7a8c')
      expect(resolved.light).toBe('#4a7a8c')
      expect(resolved.dark).toBe('#4a7a8c')
    })

    it('still produces two usable variants for pure black and pure white (saturation 0, hue meaningless — the search must terminate on the lightness axis alone)', () => {
      const black = resolveMemberColor(ID_A, '#000000')
      expect(black.light).toBe('#000000')
      expect(contrastRatio(black.dark, DARK_CARD_BG)).toBeGreaterThanOrEqual(MIN_CONTRAST)

      const white = resolveMemberColor(ID_A, '#ffffff')
      expect(contrastRatio(white.light, LIGHT_CARD_BG)).toBeGreaterThanOrEqual(MIN_CONTRAST)
      expect(white.dark).toBe('#ffffff')
    })

    it('treats an uppercase hex identically to its lowercase form', () => {
      expect(resolveMemberColor(ID_A, '#FFFF00')).toEqual(resolveMemberColor(ID_A, '#ffff00'))
    })

    it('meets 3:1 contrast on both card backgrounds for a range of picks', () => {
      const picks = ['#ffff00', '#000080', '#000000', '#ffffff', '#4a7a8c', '#3366cc']
      for (const hex of picks) {
        const resolved = resolveMemberColor(ID_A, hex)
        expect(contrastRatio(resolved.light, LIGHT_CARD_BG)).toBeGreaterThanOrEqual(MIN_CONTRAST)
        expect(contrastRatio(resolved.dark, DARK_CARD_BG)).toBeGreaterThanOrEqual(MIN_CONTRAST)
      }
    })
  })
})

describe('isMemberColorKey', () => {
  it('accepts palette keys and rejects everything else', () => {
    expect(isMemberColorKey('rose')).toBe(true)
    expect(isMemberColorKey('ROSE')).toBe(false)
    expect(isMemberColorKey('#abcdef')).toBe(false)
    expect(isMemberColorKey(null)).toBe(false)
  })
})

describe('isHexColor', () => {
  it('accepts a strict 6-digit hex (either case) and rejects everything else', () => {
    expect(isHexColor('#ffffff')).toBe(true)
    expect(isHexColor('#FFFFFF')).toBe(true)
    expect(isHexColor('#abc')).toBe(false)
    expect(isHexColor('#abcdefgh')).toBe(false)
    expect(isHexColor('rgb(0,0,0)')).toBe(false)
    expect(isHexColor('teal')).toBe(false)
    expect(isHexColor('')).toBe(false)
    expect(isHexColor(null)).toBe(false)
  })
})
