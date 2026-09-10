/**
 * What a shopping list is likely to cost, from the best price the household has actually paid for
 * each product (#128) — see `Homassy.API.Functions.PriceInsightFunctions.GetBestPricesAsync`.
 *
 * Pure and dependency-free on purpose, like the rest of `~/utils`: the estimate is the one part of
 * this feature with rules worth pinning down in a test (currencies kept apart, an unpriced item
 * counted rather than treated as free, a zero quantity not turning into a credit), and none of
 * those rules need a component, a store or a Nuxt runtime to state.
 */

/** One row of the list: how many, and what the household last paid per unit, if anything. */
export interface EstimateInput {
  /**
   * Which product the row is for. Carried for the caller's own bookkeeping and never read by
   * `estimateListTotal` — it exists so a caller can build the array straight from its rows without
   * a second parallel structure. Deliberately widened from the task brief's `number`: the API keys
   * best prices by the product's public id (a Guid string), because no public DTO in this codebase
   * hands out an internal integer key.
   */
  productId: string | number
  quantity: number
  /** Absent when the household has never recorded a price for this product. */
  bestPrice?: { unitPrice: number; currency: string }
}

export interface EstimateResult {
  /**
   * One running total per currency, never a combined figure. This milestone has no exchange rate
   * and invents none, so a list holding a forint price and a euro price yields two numbers the UI
   * shows on two lines — adding them would produce a total that is not money in any currency.
   */
  totalsByCurrency: Record<string, number>
  /**
   * How many rows contributed nothing because no price is known. The header has to say this out
   * loud: a total that silently omits three items reads as the cost of the whole list, which is
   * the one way this feature could actively mislead someone at the till.
   */
  unpricedCount: number
  /** How many rows did contribute — so the UI can tell "nothing priced yet" from "all priced". */
  pricedCount: number
}

/**
 * A quantity of zero (or a negative one, which the API's own validation should already prevent)
 * still means "buy this", so it is estimated as a single unit rather than as nothing — and
 * certainly never as a negative amount subtracted from the total. Anything above zero is used as
 * given, fractional quantities included: half a kilo of something costs half.
 */
const effectiveQuantity = (quantity: number): number =>
  Number.isFinite(quantity) && quantity > 0 ? quantity : 1

export const estimateListTotal = (items: readonly EstimateInput[]): EstimateResult => {
  const totalsByCurrency: Record<string, number> = {}
  let unpricedCount = 0
  let pricedCount = 0

  for (const item of items) {
    if (!item.bestPrice) {
      unpricedCount++
      continue
    }

    const { unitPrice, currency } = item.bestPrice
    totalsByCurrency[currency] = (totalsByCurrency[currency] ?? 0) + unitPrice * effectiveQuantity(item.quantity)
    pricedCount++
  }

  return { totalsByCurrency, unpricedCount, pricedCount }
}
