import { describe, expect, it } from 'vitest'
import { toBadgeViews } from '~/utils/badgeState'
import type { BadgeStateDto } from '~/types/insights'

/**
 * The test that matters most here is the fallback one: it is what proves a badge the server added
 * after the last frontend release still renders as a real tile.
 */
const badge = (overrides: Partial<BadgeStateDto> = {}): BadgeStateDto => ({
  id: 'items-added-25',
  iconName: 'i-lucide-package-plus',
  titleKey: 'badges.itemsAdded25.title',
  descriptionKey: 'badges.itemsAdded25.description',
  fallbackTitle: 'Stocking up',
  fallbackDescription: 'Added 25 items to the household inventory.',
  threshold: 25,
  progress: 10,
  earnedAt: null,
  justUnlocked: false,
  ...overrides
})

/** A locale that knows every key it is asked about. */
const allTranslated = {
  translate: (key: string) => `translated:${key}`,
  hasTranslation: () => true
}

/** A locale that knows none of them - a client that has not shipped this badge's strings yet. */
const noneTranslated = {
  translate: (key: string) => key,
  hasTranslation: () => false
}

describe('toBadgeViews', () => {
  it('uses the translation when the locale has one', () => {
    const [view] = toBadgeViews([badge()], allTranslated.translate, allTranslated.hasTranslation)

    expect(view!.title).toBe('translated:badges.itemsAdded25.title')
    expect(view!.description).toBe('translated:badges.itemsAdded25.description')
  })

  it('falls back to the server-shipped English when the locale has no translation', () => {
    const [view] = toBadgeViews([badge()], noneTranslated.translate, noneTranslated.hasTranslation)

    expect(view!.title).toBe('Stocking up')
    expect(view!.description).toBe('Added 25 items to the household inventory.')
    // Specifically not the raw key, which is what vue-i18n's t() would have returned.
    expect(view!.title).not.toContain('badges.')
  })

  it('never reports progress above the threshold', () => {
    const [view] = toBadgeViews(
      [badge({ progress: 999, threshold: 25 })],
      allTranslated.translate,
      allTranslated.hasTranslation
    )

    expect(view!.progress).toBe(25)
  })

  it('never reports negative progress', () => {
    const [view] = toBadgeViews(
      [badge({ progress: -5 })],
      allTranslated.translate,
      allTranslated.hasTranslation
    )

    expect(view!.progress).toBe(0)
  })

  it('is earned exactly when earnedAt is set', () => {
    const [locked] = toBadgeViews([badge({ earnedAt: null })], allTranslated.translate, allTranslated.hasTranslation)
    const [earned] = toBadgeViews(
      [badge({ earnedAt: '2026-09-10T10:00:00Z', progress: 25 })],
      allTranslated.translate,
      allTranslated.hasTranslation
    )

    expect(locked!.earned).toBe(false)
    expect(earned!.earned).toBe(true)
  })

  it('carries justUnlocked through untouched', () => {
    const [view] = toBadgeViews(
      [badge({ earnedAt: '2026-09-10T10:00:00Z', progress: 25, justUnlocked: true })],
      allTranslated.translate,
      allTranslated.hasTranslation
    )

    expect(view!.justUnlocked).toBe(true)
  })

  it('returns an empty array for an empty input', () => {
    expect(toBadgeViews([], allTranslated.translate, allTranslated.hasTranslation)).toEqual([])
  })
})
