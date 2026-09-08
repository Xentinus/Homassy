/**
 * The app's expiration ramp: one mapping from "when does this expire" to a severity level, and one
 * table of what each level looks like.
 *
 * Every surface that says something about expiry reads from here — the inventory rows, the grid
 * cards and the nav badge — so they cannot drift into saying different things about the same date.
 * Before this, each component carried its own colour literals and the nav badge was permanently
 * red whether milk went off today or in a fortnight.
 *
 * Colours are Nuxt UI **semantic tokens** (`warning`, `error`, `dimmed`), not palette shades, so
 * both themes are handled without a `dark:` variant per class — and the stock ring can stroke with
 * `currentColor`.
 */

/** Ordered least to most severe. `none` is "no expiry date at all". */
export type ExpirationLevel = 'none' | 'ok' | 'soon' | 'critical' | 'expired'

/** Inside this many days an item starts showing amber. Matches `isExpiringWithinTwoWeeks`. */
export const EXPIRATION_SOON_DAYS = 14

/** Inside this many days it goes red — the point where "soon" becomes "now". */
export const EXPIRATION_CRITICAL_DAYS = 3

export interface ExpirationTone {
  level: ExpirationLevel
  /** Text colour, and therefore the stock ring's stroke. */
  text: string
  /** Card / row border. */
  border: string
  /** Card / row background wash. */
  surface: string
  /** Solid fill, for the nav badge. */
  badge: string
  icon: string
}

const TONES: Record<ExpirationLevel, ExpirationTone> = {
  none: {
    level: 'none',
    text: 'text-dimmed',
    border: 'border-default',
    surface: 'bg-default',
    badge: 'bg-elevated',
    icon: 'i-lucide-calendar'
  },
  ok: {
    level: 'ok',
    text: 'text-dimmed',
    border: 'border-default',
    surface: 'bg-default',
    badge: 'bg-elevated',
    icon: 'i-lucide-calendar'
  },
  soon: {
    level: 'soon',
    text: 'text-warning',
    border: 'border-warning/50',
    surface: 'bg-warning/5',
    badge: 'bg-warning',
    icon: 'i-lucide-clock'
  },
  critical: {
    level: 'critical',
    text: 'text-error',
    border: 'border-error/50',
    surface: 'bg-error/5',
    badge: 'bg-error',
    icon: 'i-lucide-alarm-clock'
  },
  expired: {
    level: 'expired',
    text: 'text-error',
    border: 'border-error',
    surface: 'bg-error/10',
    badge: 'bg-error',
    icon: 'i-lucide-alert-circle'
  }
}

const SEVERITY: ExpirationLevel[] = ['none', 'ok', 'soon', 'critical', 'expired']

/**
 * Whole calendar days from today until the date — negative once it is past.
 *
 * Calendar days, not elapsed hours: "expires tomorrow" has to mean tomorrow's date regardless of
 * what time of day either end falls on, which is also how `useExpirationCheck` compares.
 */
export const daysUntilExpiration = (date: string | Date | null | undefined): number | null => {
  if (!date) return null

  const target = new Date(date)
  if (Number.isNaN(target.getTime())) return null

  target.setHours(0, 0, 0, 0)
  const today = new Date()
  today.setHours(0, 0, 0, 0)

  return Math.round((target.getTime() - today.getTime()) / 86_400_000)
}

export const expirationLevel = (date: string | Date | null | undefined): ExpirationLevel => {
  const days = daysUntilExpiration(date)
  if (days === null) return 'none'
  if (days < 0) return 'expired'
  if (days <= EXPIRATION_CRITICAL_DAYS) return 'critical'
  if (days <= EXPIRATION_SOON_DAYS) return 'soon'
  return 'ok'
}

export const expirationTone = (date: string | Date | null | undefined): ExpirationTone =>
  TONES[expirationLevel(date)]

export const toneForLevel = (level: ExpirationLevel): ExpirationTone => TONES[level]

/**
 * The most severe level across several dates — what a card summarising a product's items shows.
 */
export const worstExpirationLevel = (dates: (string | Date | null | undefined)[]): ExpirationLevel => {
  let worst: ExpirationLevel = 'none'
  for (const date of dates) {
    const level = expirationLevel(date)
    if (SEVERITY.indexOf(level) > SEVERITY.indexOf(worst)) worst = level
  }
  return worst
}

export const useExpirationStatus = () => ({
  daysUntilExpiration,
  expirationLevel,
  expirationTone,
  toneForLevel,
  worstExpirationLevel
})
