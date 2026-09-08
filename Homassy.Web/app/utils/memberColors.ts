/**
 * Identity colours for family members.
 *
 * Anywhere the avatar is small or absent — calendar day dots, activity rows, presence chips,
 * the "changed by" flash — the colour is the fast "who" cue. Two rules shape this file:
 *
 * 1. **Fixed palette, not free-form HSL.** A hash straight into the hue wheel puts two members
 *    on near-identical shades often enough to matter in a four-person family. Eight hand-picked,
 *    well-separated hues cannot.
 * 2. **Accent only.** Each entry carries a light-theme and a dark-theme value that both clear
 *    3:1 against their own theme's card background, because the colour lands on rings, borders
 *    and dots. It is never a text colour on an arbitrary background and never a card fill.
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
  key: MemberColorKey
  /** Accent for the light theme. */
  light: string
  /** Accent for the dark theme — lifted so it holds up on a near-black card. */
  dark: string
  /** Ring/border tint; the accent at reduced strength so a 2px ring is not shouting. */
  ring: string
  /** From/to pair for the initials-placeholder gradient. */
  gradient: [string, string]
}

/**
 * Tailwind's 500 (light) / 400 (dark) steps for eight hues spaced around the wheel, with the
 * 600→400 pair as the placeholder gradient. Order matters: it is the bucket order of the hash
 * below, so reordering this array repaints every existing member. Append, never reorder.
 */
export const MEMBER_COLORS: readonly MemberColor[] = [
  { key: 'rose', light: '#f43f5e', dark: '#fb7185', ring: '#fb7185', gradient: ['#e11d48', '#fb7185'] },
  { key: 'amber', light: '#f59e0b', dark: '#fbbf24', ring: '#fbbf24', gradient: ['#d97706', '#fbbf24'] },
  { key: 'lime', light: '#65a30d', dark: '#a3e635', ring: '#a3e635', gradient: ['#4d7c0f', '#a3e635'] },
  { key: 'teal', light: '#14b8a6', dark: '#2dd4bf', ring: '#2dd4bf', gradient: ['#0d9488', '#2dd4bf'] },
  { key: 'sky', light: '#0ea5e9', dark: '#38bdf8', ring: '#38bdf8', gradient: ['#0284c7', '#38bdf8'] },
  { key: 'indigo', light: '#6366f1', dark: '#818cf8', ring: '#818cf8', gradient: ['#4f46e5', '#818cf8'] },
  { key: 'violet', light: '#8b5cf6', dark: '#a78bfa', ring: '#a78bfa', gradient: ['#7c3aed', '#a78bfa'] },
  { key: 'fuchsia', light: '#d946ef', dark: '#e879f9', ring: '#e879f9', gradient: ['#c026d3', '#e879f9'] }
] as const

const KEYS: readonly string[] = MEMBER_COLORS.map(c => c.key)

export const isMemberColorKey = (value: unknown): value is MemberColorKey =>
  typeof value === 'string' && KEYS.includes(value)

/**
 * FNV-1a over the normalised id. Chosen over `reduce((h, c) => h * 31 + c)` because the latter
 * clusters badly on GUIDs, which share long runs of hex characters — two family members created
 * seconds apart can differ only in the last group and would land in the same bucket.
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

/**
 * The member's effective colour: their own choice when it is one of ours, otherwise the
 * deterministic pick. An unknown or free-form override (an old value, a hand-edited row) falls
 * back rather than escaping the palette — that is what keeps the contrast guarantee true.
 */
export const resolveMemberColor = (publicId: string, override?: string | null): MemberColor =>
  isMemberColorKey(override)
    ? MEMBER_COLORS.find(c => c.key === override)!
    : pickMemberColor(publicId)
