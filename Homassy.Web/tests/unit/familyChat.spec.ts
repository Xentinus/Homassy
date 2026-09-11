import { describe, expect, it } from 'vitest'
import { buildChatSections, groupBySender, type GroupableMessage } from '~/utils/familyChat'

/**
 * Local wall-clock timestamps, never a hardcoded `Z` string — day bucketing is done in the
 * viewer's own local calendar (see `activityTimeline.ts`), so a UTC literal would only assert the
 * right thing on a machine at one particular offset. Month is 0-indexed, matching `Date`.
 */
const at = (year: number, month: number, day: number, hour = 0, minute = 0): string =>
  new Date(year, month, day, hour, minute).toISOString()

const message = (publicId: string, senderPublicId: string, sentAt: string): GroupableMessage => ({
  publicId,
  sentAt,
  sender: { publicId: senderPublicId }
})

describe('groupBySender', () => {
  it('collapses consecutive messages from one sender within the gap', () => {
    const runs = groupBySender([
      message('m1', 'anna', at(2026, 8, 11, 10, 0)),
      message('m2', 'anna', at(2026, 8, 11, 10, 2)),
      message('m3', 'anna', at(2026, 8, 11, 10, 4))
    ])

    expect(runs).toHaveLength(1)
    expect(runs[0]!.messages.map(m => m.publicId)).toEqual(['m1', 'm2', 'm3'])
    expect(runs[0]!.startedAt).toBe(at(2026, 8, 11, 10, 0))
  })

  it('starts a new run when the same sender comes back after the gap', () => {
    const runs = groupBySender([
      message('m1', 'anna', at(2026, 8, 11, 10, 0)),
      message('m2', 'anna', at(2026, 8, 11, 10, 30))
    ])

    expect(runs).toHaveLength(2)
  })

  it('measures the gap against the previous message, not the run start', () => {
    // Four minutes apart each time: one person typing steadily for a quarter of an hour is one
    // utterance, not four.
    const runs = groupBySender([
      message('m1', 'anna', at(2026, 8, 11, 10, 0)),
      message('m2', 'anna', at(2026, 8, 11, 10, 4)),
      message('m3', 'anna', at(2026, 8, 11, 10, 8)),
      message('m4', 'anna', at(2026, 8, 11, 10, 12))
    ])

    expect(runs).toHaveLength(1)
  })

  it('keeps two runs from one sender apart when somebody else spoke in between', () => {
    const runs = groupBySender([
      message('m1', 'anna', at(2026, 8, 11, 10, 0)),
      message('m2', 'bela', at(2026, 8, 11, 10, 1)),
      message('m3', 'anna', at(2026, 8, 11, 10, 2))
    ])

    expect(runs.map(r => r.senderPublicId)).toEqual(['anna', 'bela', 'anna'])
  })

  it('returns no runs for an empty stream', () => {
    expect(groupBySender([])).toEqual([])
  })
})

describe('buildChatSections', () => {
  const now = new Date(2026, 8, 11, 12, 0)

  it('splits a run that straddles local midnight across two day sections', () => {
    // The same sender, four minutes apart - one run by the gap rule, but two calendar days, and
    // the separator has to come between them.
    const sections = buildChatSections([
      message('m1', 'anna', at(2026, 8, 10, 23, 58)),
      message('m2', 'anna', at(2026, 8, 11, 0, 2))
    ], now)

    expect(sections.map(s => s.key)).toEqual(['yesterday', 'today'])
    expect(sections[0]!.runs).toHaveLength(1)
    expect(sections[1]!.runs).toHaveLength(1)
  })

  it('groups a day into sender runs', () => {
    const sections = buildChatSections([
      message('m1', 'anna', at(2026, 8, 11, 9, 0)),
      message('m2', 'anna', at(2026, 8, 11, 9, 1)),
      message('m3', 'bela', at(2026, 8, 11, 9, 2))
    ], now)

    expect(sections).toHaveLength(1)
    expect(sections[0]!.key).toBe('today')
    expect(sections[0]!.runs.map(r => r.messages.length)).toEqual([2, 1])
  })

  it('labels an older day with its own date key', () => {
    const sections = buildChatSections([
      message('m1', 'anna', at(2026, 8, 3, 15, 0))
    ], now)

    expect(sections[0]!.key).toBe('2026-09-03')
  })
})
