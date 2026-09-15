import { describe, expect, it } from 'vitest'
import { currencyCodeToEnum } from '~/utils/enumMappers'

// Pins `currencyCodeToEnum` against the server's actual `Homassy.Data/Enums/Currency.cs` numbers,
// not against the web `Currency` enum it maps into — asserting through that enum would let both
// drift the same way again and still pass. Eur=98 and Usd=294 (not the visually-similar Flow=105
// and Tnd=279) is exactly the bug a whole-branch review caught: the web enum's numbers had drifted
// out of step with the server's, so choosing EUR silently stored Flow and USD stored Tunisian
// Dinar. See `Homassy.API/Models/Insights/PriceHistoryResponse.cs`'s `BestKnownPrice.Currency`
// remarks for the server-side account of the same drift.
describe('currencyCodeToEnum', () => {
  it('maps EUR to the server\'s Eur value (98)', () => {
    expect(currencyCodeToEnum('EUR')).toBe(98)
  })

  it('maps USD to the server\'s Usd value (294)', () => {
    expect(currencyCodeToEnum('USD')).toBe(294)
  })

  it('maps HUF to the server\'s Huf value (135)', () => {
    expect(currencyCodeToEnum('HUF')).toBe(135)
  })

  it('is case-insensitive', () => {
    expect(currencyCodeToEnum('eur')).toBe(98)
    expect(currencyCodeToEnum('usd')).toBe(294)
    expect(currencyCodeToEnum('huf')).toBe(135)
  })

  it('falls back to HUF (135) for an unknown code', () => {
    expect(currencyCodeToEnum('XYZ')).toBe(135)
    expect(currencyCodeToEnum('')).toBe(135)
  })
})
