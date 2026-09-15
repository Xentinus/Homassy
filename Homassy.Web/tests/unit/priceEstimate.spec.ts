import { describe, expect, it } from 'vitest'
import { estimateListTotal } from '~/utils/priceEstimate'

describe('estimateListTotal', () => {
  it('multiplies unit price by quantity', () => {
    const r = estimateListTotal([{ productId: 1, quantity: 3, bestPrice: { unitPrice: 250, currency: 'HUF' } }])
    expect(r.totalsByCurrency.HUF).toBe(750)
    expect(r.pricedCount).toBe(1)
    expect(r.unpricedCount).toBe(0)
  })

  it('keeps currencies apart instead of summing them', () => {
    const r = estimateListTotal([
      { productId: 1, quantity: 1, bestPrice: { unitPrice: 100, currency: 'HUF' } },
      { productId: 2, quantity: 1, bestPrice: { unitPrice: 2, currency: 'EUR' } }
    ])
    expect(r.totalsByCurrency).toEqual({ HUF: 100, EUR: 2 })
  })

  it('counts unpriced items rather than treating them as free', () => {
    const r = estimateListTotal([
      { productId: 1, quantity: 1, bestPrice: { unitPrice: 100, currency: 'HUF' } },
      { productId: 2, quantity: 1 }
    ])
    expect(r.unpricedCount).toBe(1)
    expect(r.totalsByCurrency.HUF).toBe(100)
  })

  it('returns an empty result for an empty list', () => {
    expect(estimateListTotal([])).toEqual({ totalsByCurrency: {}, unpricedCount: 0, pricedCount: 0 })
  })

  it('treats a zero or negative quantity as one item, not as a credit', () => {
    const r = estimateListTotal([{ productId: 1, quantity: 0, bestPrice: { unitPrice: 100, currency: 'HUF' } }])
    expect(r.totalsByCurrency.HUF).toBe(100)
  })

  it('prefers a manual price over the best known price', () => {
    const r = estimateListTotal([{
      productId: 1,
      quantity: 2,
      bestPrice: { unitPrice: 100, currency: 'HUF' },
      manualPrice: { unitPrice: 150, currency: 'HUF' }
    }])
    expect(r.totalsByCurrency.HUF).toBe(300)
    expect(r.pricedCount).toBe(1)
  })

  it('prices a row that has only a manual price', () => {
    const r = estimateListTotal([{ productId: 'custom-row', quantity: 3, manualPrice: { unitPrice: 50, currency: 'HUF' } }])
    expect(r.totalsByCurrency.HUF).toBe(150)
    expect(r.unpricedCount).toBe(0)
  })

  it('uses the manual price currency, not the known price currency', () => {
    const r = estimateListTotal([{
      productId: 1,
      quantity: 1,
      bestPrice: { unitPrice: 100, currency: 'HUF' },
      manualPrice: { unitPrice: 2, currency: 'EUR' }
    }])
    expect(r.totalsByCurrency).toEqual({ EUR: 2 })
  })
})
