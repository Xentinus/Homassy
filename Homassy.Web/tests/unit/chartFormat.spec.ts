import { describe, expect, it } from 'vitest'
import { formatAxisDate, formatCompact, formatCurrency } from '~/utils/chart/format'

describe('formatCompact', () => {
  it('shortens thousands', () => {
    expect(formatCompact(12400, 'en')).toMatch(/12/)
    expect(formatCompact(12400, 'en').length).toBeLessThan('12400'.length + 2)
  })

  it('leaves small numbers alone', () => {
    expect(formatCompact(7, 'en')).toBe('7')
  })
})

describe('formatCurrency', () => {
  it('renders the currency code it is given', () => {
    expect(formatCurrency(1234.5, 'EUR', 'en')).toContain('1,234.5')
  })

  it('does not throw on an unknown code, which the Currency enum has plenty of', () => {
    expect(() => formatCurrency(10, 'DOGE', 'en')).not.toThrow()
    expect(formatCurrency(10, 'DOGE', 'en')).toBe('10 DOGE')
  })
})

describe('formatAxisDate', () => {
  it('formats a day bucket without the year', () => {
    const label = formatAxisDate(Date.UTC(2026, 8, 9), 'en', 'day')
    expect(label).not.toContain('2026')
    expect(label.length).toBeGreaterThan(0)
  })

  it('formats a week bucket in UTC regardless of the runtime timezone', () => {
    // 2026-09-07T23:00:00Z is a Monday, 23:00 UTC. Any positive-offset timezone (e.g. this
    // repo's own dev machine, Europe/Budapest at UTC+2) rolls that instant into the next
    // calendar day, Sep 8, if formatAxisDate ever stopped pinning `timeZone: 'UTC'` — unlike
    // the day-bucket test above, whose midnight-UTC input stays on the same local day under
    // a +2 offset and so cannot catch that regression on this machine. Asserting the
    // UTC-correct label pins the pin.
    const ms = Date.UTC(2026, 8, 7, 23, 0, 0)
    expect(formatAxisDate(ms, 'en', 'week')).toBe('Sep 7')
  })
})
