/**
 * Turns the server's badge payload (#109) into the shape the badge grid renders, resolving each
 * badge's text through the locale files with the server's own English as the fallback.
 *
 * `translate` and `hasTranslation` come in as parameters rather than being taken from `useI18n()`
 * inside this module - which is what keeps it a plain function the vitest harness can import with
 * no Vue runtime, and what makes the fallback rule testable at all (a test can simply say "this
 * key has no translation").
 */
import type { BadgeStateDto } from '~/types/insights'

export interface BadgeView {
  id: string
  icon: string
  title: string
  description: string
  /** Never above `threshold` - the server caps it, and this caps it again. */
  progress: number
  threshold: number
  earned: boolean
  justUnlocked: boolean
}

/**
 * A badge's own text if the locale files know this badge, otherwise the English the server shipped
 * with it.
 *
 * This is the whole of #109's "a new badge needs no frontend release": the server can add a badge
 * today, and every client renders it correctly today - with English text, but with the right title,
 * the right description and the right threshold, rather than a blank tile waiting for a deploy.
 * `hasTranslation` is asked first because vue-i18n's `t()` returns the key itself when it has no
 * translation, and a tile reading "badges.itemsAdded100.title" is worse than one reading English.
 */
const resolveText = (
  key: string,
  fallback: string,
  translate: (key: string) => string,
  hasTranslation: (key: string) => boolean
): string => (hasTranslation(key) ? translate(key) : fallback)

export const toBadgeViews = (
  badges: readonly BadgeStateDto[],
  translate: (key: string) => string,
  hasTranslation: (key: string) => boolean
): BadgeView[] => badges.map(badge => ({
  id: badge.id,
  icon: badge.iconName,
  title: resolveText(badge.titleKey, badge.fallbackTitle, translate, hasTranslation),
  description: resolveText(badge.descriptionKey, badge.fallbackDescription, translate, hasTranslation),
  // Clamped here as well as server-side: a progress ring drawn from progress/threshold must never
  // overflow, and this module is the last place the number passes through before it becomes an arc.
  progress: Math.min(Math.max(badge.progress, 0), badge.threshold),
  threshold: badge.threshold,
  // `earnedAt` is the earned state - the durable row's timestamp - not `progress >= threshold`.
  // They agree in the ordinary case, but only the row survives a threshold being re-tuned, and
  // only the row is what the unlock celebration was fired against.
  earned: badge.earnedAt !== null,
  justUnlocked: badge.justUnlocked
}))
