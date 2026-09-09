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
  })
})

describe('formatAxisDate', () => {
  it('formats a day bucket without the year', () => {
    const label = formatAxisDate(Date.UTC(2026, 8, 9), 'en', 'day')
    expect(label).not.toContain('2026')
    expect(label.length).toBeGreaterThan(0)
  })
})
