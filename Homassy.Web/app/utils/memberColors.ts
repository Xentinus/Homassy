/**
 * Identity colours for family members.
 *
 * Anywhere the avatar is small or absent — calendar day dots, activity rows, presence chips,
 * the "changed by" flash — the colour is the fast "who" cue. Two rules shape this file:
 *
 * 1. **Eight curated presets, plus an arbitrary custom pick.** A hash straight into the hue wheel
 *    puts two members on near-identical shades often enough to matter in a four-person family, so
 *    the deterministic pick still draws from eight hand-picked, well-separated hues. A member who
 *    wants something else can pick any hex; see point 2 for how that stays legible.
 * 2. **Accent only, and always 3:1.** Each entry carries a light-theme and a dark-theme value that
 *    both clear 3:1 against their own theme's card background, because the colour lands on rings,
 *    borders and dots. It is never a text colour on an arbitrary background and never a card
 *    fill. The eight presets are hand-tuned to already clear this bar (see the `MEMBER_COLORS`
 *    comment below); a custom hex clears it by having its lightness adjusted per theme at resolve
 *    time — see `resolveMemberColor` and `adjustForContrast` — so the member's hue is preserved
 *    and only ever pushed as far as it has to be.
 */

export type MemberColorKey =
  | 'rose'
  | 'amber'
  | 'lime'
  | 'teal'
  | 'sky'
  | 'indigo'
  | 'violet'
  | 'fuchsia'

export interface MemberColor {
  /** One of the eight palette keys, or `'custom'` for a member's own hex pick. */
  key: MemberColorKey | 'custom'
  /** Accent for the light theme, used for rings, borders and dots. */
  light: string
  /** Accent for the dark theme — lifted so it holds up on a near-black card, used for rings, borders and dots. */
  dark: string
  /** From/to pair for the initials-placeholder gradient. */
  gradient: [string, string]
}

/**
 * Eight hues spaced around the wheel, with the 600→400 pair as the placeholder gradient. The
 * light column is Tailwind's 500 step where that clears 3:1 on white, otherwise the 700 step
 * (amber, lime, teal, sky). The dark column is the 400 step throughout. Both length and order
 * are load-bearing: pickMemberColor indexes with % MEMBER_COLORS.length, so changing either
 * repaints every existing member. Treat the array as frozen.
 */
export const MEMBER_COLORS: readonly MemberColor[] = [
  { key: 'rose', light: '#f43f5e', dark: '#fb7185', gradient: ['#e11d48', '#fb7185'] },
  { key: 'amber', light: '#b45309', dark: '#fbbf24', gradient: ['#d97706', '#fbbf24'] },
  { key: 'lime', light: '#4d7c0f', dark: '#a3e635', gradient: ['#4d7c0f', '#a3e635'] },
  { key: 'teal', light: '#0f766e', dark: '#2dd4bf', gradient: ['#0d9488', '#2dd4bf'] },
  { key: 'sky', light: '#0369a1', dark: '#38bdf8', gradient: ['#0284c7', '#38bdf8'] },
  { key: 'indigo', light: '#6366f1', dark: '#818cf8', gradient: ['#4f46e5', '#818cf8'] },
  { key: 'violet', light: '#8b5cf6', dark: '#a78bfa', gradient: ['#7c3aed', '#a78bfa'] },
  { key: 'fuchsia', light: '#d946ef', dark: '#e879f9', gradient: ['#c026d3', '#e879f9'] }
] as const

const KEYS: readonly string[] = MEMBER_COLORS.map(c => c.key)

export const isMemberColorKey = (value: unknown): value is MemberColorKey =>
  typeof value === 'string' && KEYS.includes(value)

/**
 * A strict six-digit hex colour (`#rrggbb`, either case). Mirrors the API's
 * `UpdateUserSettingsRequest.IdentityColor` validation — three-digit shorthand, alpha forms,
 * `rgb()` and bare names are all rejected here too, so the client's notion of "a valid custom
 * colour" never drifts from what the server will actually persist.
 */
export const isHexColor = (value: unknown): value is string =>
  typeof value === 'string' && /^#[0-9a-fA-F]{6}$/.test(value)

/**
 * FNV-1a over the normalised id. Chosen over `reduce((h, c) => h * 31 + c)` because the latter
 * loses precision past 2^53 and collapses to a single bucket for any same-length input.
 */
const hash = (input: string): number => {
  let h = 0x811c9dc5
  for (let i = 0; i < input.length; i++) {
    h ^= input.charCodeAt(i)
    h = Math.imul(h, 0x01000193) >>> 0
  }
  return h >>> 0
}

/** Lower-cased, dashes stripped: the same user must not change colour because a caller passed a formatted GUID. */
const normalise = (publicId: string): string => (publicId ?? '').toLowerCase().replace(/-/g, '')

/** The member's colour when they have not chosen one. Stable for the lifetime of the account. */
export const pickMemberColor = (publicId: string): MemberColor =>
  MEMBER_COLORS[hash(normalise(publicId)) % MEMBER_COLORS.length]!

// --- Custom-colour contrast adjustment -------------------------------------------------------
//
// A member's custom pick keeps its hue always; only lightness moves, and only as far as it has
// to, so that each theme's variant clears MIN_CONTRAST against that theme's own card background.

/** Parses a strict `#rrggbb` string into 0-255 channels. Assumes `isHexColor` already passed. */
const hexToRgb = (hex: string): [number, number, number] => [
  parseInt(hex.slice(1, 3), 16),
  parseInt(hex.slice(3, 5), 16),
  parseInt(hex.slice(5, 7), 16)
]

const rgbToHex = (r: number, g: number, b: number): string =>
  '#' + [r, g, b]
    .map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0'))
    .join('')

/** WCAG's piecewise gamma curve for one 0-255 sRGB channel, per the contrast-ratio formula. */
const channelLuminance = (value: number): number => {
  const c = value / 255
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

/** WCAG relative luminance of a hex colour: 0 (black) to 1 (white). */
const relativeLuminance = (hex: string): number => {
  const [red, green, blue] = hexToRgb(hex)
  return 0.2126 * channelLuminance(red) + 0.7152 * channelLuminance(green) + 0.0722 * channelLuminance(blue)
}

/**
 * The two card backgrounds a member accent has to clear MIN_CONTRAST against — from `@nuxt/ui`'s
 * tokens with `neutral: 'slate'` (`app/app.config.ts`): `--ui-bg` is `#fff` in light mode and
 * Tailwind's `slate-900` in dark mode (verified against the installed `tailwindcss` package's
 * `--color-slate-900`, an OKLCH value that resolves to this same hex within rounding).
 */
const LIGHT_CARD_BACKGROUND = '#ffffff'
const DARK_CARD_BACKGROUND = '#0f172a'

/** The WCAG contrast ratio an accent colour must clear against a card background. */
const MIN_CONTRAST_RATIO = 3

/**
 * The accent luminance that gives exactly `MIN_CONTRAST_RATIO` against a light background —
 * derived from that background's own luminance rather than a hand-picked lightness percentage, so
 * the guarantee survives a theme (or a palette-neutral) change. Below this, an accent is dark
 * enough to clear the bar on a light card; a colour already at or under it needs no adjustment.
 *
 * WCAG contrast = (lighter + 0.05) / (darker + 0.05). Against a light background the accent is
 * always the darker of the two (or the search below would not converge), so solving that formula
 * for the accent's luminance at ratio = MIN_CONTRAST_RATIO gives this threshold directly.
 */
const maxAccentLuminanceOn = (backgroundHex: string): number =>
  (relativeLuminance(backgroundHex) + 0.05) / MIN_CONTRAST_RATIO - 0.05

/** Mirrors `maxAccentLuminanceOn` for a dark background, where the accent must be the lighter colour. */
const minAccentLuminanceOn = (backgroundHex: string): number =>
  MIN_CONTRAST_RATIO * (relativeLuminance(backgroundHex) + 0.05) - 0.05

const LIGHT_MAX_ACCENT_LUMINANCE = maxAccentLuminanceOn(LIGHT_CARD_BACKGROUND)
const DARK_MIN_ACCENT_LUMINANCE = minAccentLuminanceOn(DARK_CARD_BACKGROUND)

interface Hsl { h: number; s: number; l: number } // h in [0, 360), s and l in [0, 100]

const hexToHsl = (hex: string): Hsl => {
  const [r8, g8, b8] = hexToRgb(hex)
  const r = r8 / 255
  const g = g8 / 255
  const b = b8 / 255
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  const l = (max + min) / 2
  if (max === min) return { h: 0, s: 0, l: l * 100 } // grey: hue is meaningless, not undefined

  const d = max - min
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
  let h: number
  switch (max) {
    case r: h = (g - b) / d + (g < b ? 6 : 0); break
    case g: h = (b - r) / d + 2; break
    default: h = (r - g) / d + 4
  }
  return { h: h * 60, s: s * 100, l: l * 100 }
}

const hslToHex = ({ h, s, l }: Hsl): string => {
  const S = s / 100
  const L = l / 100
  if (S === 0) {
    const v = L * 255
    return rgbToHex(v, v, v)
  }

  const hue2rgb = (p: number, q: number, t: number): number => {
    let tt = t
    if (tt < 0) tt += 1
    if (tt > 1) tt -= 1
    if (tt < 1 / 6) return p + (q - p) * 6 * tt
    if (tt < 1 / 2) return q
    if (tt < 2 / 3) return p + (q - p) * (2 / 3 - tt) * 6
    return p
  }

  const q = L < 0.5 ? L * (1 + S) : L + S - L * S
  const p = 2 * L - q
  const H = h / 360
  return rgbToHex(
    hue2rgb(p, q, H + 1 / 3) * 255,
    hue2rgb(p, q, H) * 255,
    hue2rgb(p, q, H - 1 / 3) * 255
  )
}

/**
 * Fixed iteration count for the two searches below, rather than "until close enough": the loop
 * must terminate even for a saturation-0 pick (pure black or white), where hue is meaningless but
 * lightness alone still has to converge on a target luminance. 24 halvings of a 0-100 range is
 * far finer than perceptible, and cheap enough to always spend regardless of how close the start
 * point already is.
 */
const CONTRAST_SEARCH_ITERATIONS = 24

/**
 * Binary-searches `hsl`'s lightness downward from its own value (known too light — callers only
 * reach this after the no-op check already failed) toward 0 (pure black, always dark enough),
 * returning the least-darkened hex whose luminance still clears `maxLuminance`.
 */
const darkenUntilContrast = (hsl: Hsl, maxLuminance: number): string => {
  let lo = 0
  let hi = hsl.l
  let best = hslToHex({ ...hsl, l: 0 })

  for (let i = 0; i < CONTRAST_SEARCH_ITERATIONS; i++) {
    const mid = (lo + hi) / 2
    const candidate = hslToHex({ ...hsl, l: mid })
    if (relativeLuminance(candidate) <= maxLuminance) {
      best = candidate
      lo = mid
    } else {
      hi = mid
    }
  }
  return best
}

/** Mirrors `darkenUntilContrast`, searching upward toward 100 (pure white) instead. */
const lightenUntilContrast = (hsl: Hsl, minLuminance: number): string => {
  let lo = hsl.l
  let hi = 100
  let best = hslToHex({ ...hsl, l: 100 })

  for (let i = 0; i < CONTRAST_SEARCH_ITERATIONS; i++) {
    const mid = (lo + hi) / 2
    const candidate = hslToHex({ ...hsl, l: mid })
    if (relativeLuminance(candidate) >= minLuminance) {
      best = candidate
      hi = mid
    } else {
      lo = mid
    }
  }
  return best
}

/**
 * Resolves a lower-cased, already-validated hex pick into its two theme-safe variants. A pick
 * that already clears both bounds — the common case — is returned unchanged (literally the same
 * string, not a round-tripped reconstruction) in both variants; that is the no-op path every
 * palette colour and most custom picks take.
 */
const adjustForContrast = (hex: string): { light: string; dark: string } => {
  const luminance = relativeLuminance(hex)
  const hsl = hexToHsl(hex)
  const light = luminance <= LIGHT_MAX_ACCENT_LUMINANCE ? hex : darkenUntilContrast(hsl, LIGHT_MAX_ACCENT_LUMINANCE)
  const dark = luminance >= DARK_MIN_ACCENT_LUMINANCE ? hex : lightenUntilContrast(hsl, DARK_MIN_ACCENT_LUMINANCE)
  return { light, dark }
}

/**
 * Builds a full `MemberColor` for a custom hex pick. There is no Tailwind 600/400 step to reach
 * for here, so the gradient reuses the two contrast-adjusted variants themselves: `light` is
 * generally the darker of the two (pulled down for a white card) and `dark` the lighter (pulled
 * up for a near-black one), which keeps the palette's dark-to-light gradient direction without
 * inventing a third shade.
 */
const customMemberColor = (hex: string): MemberColor => {
  const { light, dark } = adjustForContrast(hex)
  return { key: 'custom', light, dark, gradient: [light, dark] }
}

/**
 * The member's effective colour: their own palette choice, their own custom hex, or the
 * deterministic pick. An unknown or malformed override (an old value, a hand-edited row) falls
 * back rather than producing something unpredictable.
 */
export const resolveMemberColor = (publicId: string, override?: string | null): MemberColor => {
  if (isMemberColorKey(override)) {
    return MEMBER_COLORS.find(c => c.key === override)!
  }
  if (isHexColor(override)) {
    return customMemberColor(override.toLowerCase())
  }
  return pickMemberColor(publicId)
}
