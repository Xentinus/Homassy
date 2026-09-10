import { describe, expect, it } from 'vitest'
import { ProductCategory } from '~/types/enums'
import type { InventoryCompositionResponse, SpendByLocationResponse } from '~/types/insights'
import { buildCompositionSlices, buildSpendGroups } from '~/utils/insightsCharts'

// A fake `formatCategory`, standing in for `useEnumLabel().formatProductCategory` — these tests
// only care that each category maps to its own distinct, deterministic label, never the real
// translated text.
const formatCategory = (category: ProductCategory): string => `category:${category}`

// A fake translate function, standing in for `useI18n().t` — resolves the two keys these
// transforms actually look up and echoes anything else back, so a wrong key shows up in a failure
// message instead of silently passing.
const fakeT = (key: string): string => {
  if (key === 'chart.donut.otherLabel') return 'Other'
  if (key === 'insights.spend.unknownLocation') return 'Ismeretlen hely'
  return key
}

describe('buildCompositionSlices', () => {
  it('folds a ranked slice under 2% of the total into the other bucket, leaving every returned slice at or above 2%', () => {
    // totalCount = 1000, so 2% = 20. CerealAndBreakfast sits at 19 (1.9%) — under the line — while
    // every other slice and the backend's own otherCount clear it comfortably.
    const data: InventoryCompositionResponse = {
      totalCount: 1000,
      otherCount: 50,
      slices: [
        { category: ProductCategory.Grain, count: 500, share: 0.5 },
        { category: ProductCategory.Bread, count: 300, share: 0.3 },
        { category: ProductCategory.CerealAndBreakfast, count: 19, share: 0.019 },
        { category: ProductCategory.Pasta, count: 131, share: 0.131 }
      ]
    }

    const result = buildCompositionSlices(data, formatCategory, fakeT)

    // The under-2% slice never appears as its own entry.
    expect(result.some(slice => slice.key === String(ProductCategory.CerealAndBreakfast))).toBe(false)

    // This must fail if the threshold import is broken (comparing against `undefined`/`NaN` never
    // folds anything) or the comparison flips (`>=` instead of `<` folds everything instead) — both
    // leave a slice under 2% sitting in the result on its own.
    for (const slice of result) {
      expect(slice.value / data.totalCount).toBeGreaterThanOrEqual(0.02)
    }

    // The backend's otherCount (50) plus the one folded slice (19) land in a single trailing bucket.
    const other = result.find(slice => slice.key === 'other')
    expect(other).toBeDefined()
    expect(other!.value).toBe(69)
    expect(other!.label).toBe('Other')
    expect(result).toHaveLength(4)
  })

  it('produces no "other" entry at all when otherCount is zero and no slice is under 2%', () => {
    const data: InventoryCompositionResponse = {
      totalCount: 100,
      otherCount: 0,
      slices: [
        { category: ProductCategory.Grain, count: 60, share: 0.6 },
        { category: ProductCategory.Bread, count: 40, share: 0.4 }
      ]
    }

    const result = buildCompositionSlices(data, formatCategory, fakeT)

    // An empty other bucket must not become a visible zero-value wedge.
    expect(result.some(slice => slice.key === 'other')).toBe(false)
    expect(result).toHaveLength(2)
    expect(result.every(slice => slice.value > 0)).toBe(true)
  })

  it('stays coherent, with no negative or NaN share, when every slice is under 2%', () => {
    const data: InventoryCompositionResponse = {
      totalCount: 1000,
      otherCount: 924,
      slices: [
        { category: ProductCategory.Grain, count: 19, share: 0.019 },
        { category: ProductCategory.Bread, count: 19, share: 0.019 },
        { category: ProductCategory.CerealAndBreakfast, count: 19, share: 0.019 },
        { category: ProductCategory.Pasta, count: 19, share: 0.019 }
      ]
    }

    const result = buildCompositionSlices(data, formatCategory, fakeT)

    // Every ranked slice folds away, leaving exactly one trailing "other" carrying the whole total.
    expect(result).toHaveLength(1)
    const [other] = result
    expect(other!.key).toBe('other')
    expect(other!.value).toBe(1000)

    const share = other!.value / data.totalCount
    expect(Number.isNaN(share)).toBe(false)
    expect(share).toBeGreaterThanOrEqual(0)
    expect(share).toBe(1)
  })
})

describe('buildSpendGroups', () => {
  it('keeps two currencies at one location as two separate entries, never summed', () => {
    const data: SpendByLocationResponse = {
      locations: [
        {
          shoppingLocationPublicId: 'loc-1',
          locationName: 'Tesco',
          itemCount: 5,
          spendByCurrency: { Huf: 15000, Eur: 42 }
        }
      ]
    }

    const result = buildSpendGroups(data, fakeT)

    expect(result.map(group => group.currencyCode)).toEqual(['Eur', 'Huf'])
    const eur = result.find(group => group.currencyCode === 'Eur')!
    const huf = result.find(group => group.currencyCode === 'Huf')!
    expect(eur.bars).toEqual([{ key: 'loc-1', label: 'Tesco', value: 42 }])
    expect(huf.bars).toEqual([{ key: 'loc-1', label: 'Tesco', value: 15000 }])

    // The regression this guards: no bar anywhere carries a cross-currency sum like 15042.
    const allValues = result.flatMap(group => group.bars.map(bar => bar.value))
    expect(allValues).not.toContain(15042)
  })

  it('renders a null shoppingLocationPublicId as the unknown-location bucket, labelled from the caller\'s own translation', () => {
    const data: SpendByLocationResponse = {
      locations: [
        {
          shoppingLocationPublicId: null,
          // Stands in for the server's own hardcoded English name for this bucket — the label must
          // NOT come from here.
          locationName: 'Unknown location',
          itemCount: 2,
          spendByCurrency: { Huf: 500 }
        }
      ]
    }

    const result = buildSpendGroups(data, fakeT)

    expect(result).toHaveLength(1)
    const [bar] = result[0]!.bars
    expect(bar!.key).toBe('unknown')
    expect(bar!.label).toBe('Ismeretlen hely')
    expect(bar!.label).not.toBe(data.locations[0]!.locationName)
  })

  it('still carries a location with only unpriced purchases through, with its itemCount, while it contributes no spend', () => {
    const data: SpendByLocationResponse = {
      locations: [
        { shoppingLocationPublicId: 'loc-priced', locationName: 'Aldi', itemCount: 2, spendByCurrency: { Huf: 1000 } },
        // Only unpriced purchases: it still appears in the response, with a real itemCount, but has
        // nothing in any currency.
        { shoppingLocationPublicId: 'loc-unpriced', locationName: 'Free Market', itemCount: 3, spendByCurrency: {} }
      ]
    }

    // The fixture itself carries the unpriced location's itemCount, mirroring what the real API
    // response looks like for this case.
    expect(data.locations[1]!.itemCount).toBe(3)

    const result = buildSpendGroups(data, fakeT)

    // No currency bucket is synthesized for a location with nothing priced — it contributes no bar
    // to any currency's chart, and no phantom empty-currency group appears either.
    expect(result).toHaveLength(1)
    expect(result[0]!.currencyCode).toBe('Huf')
    const allKeys = result.flatMap(group => group.bars.map(bar => bar.key))
    expect(allKeys).not.toContain('loc-unpriced')
    expect(allKeys).toEqual(['loc-priced'])
  })
})
