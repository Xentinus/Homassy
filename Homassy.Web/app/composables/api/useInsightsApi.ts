/**
 * Insights API composable
 * Family-scoped aggregation endpoints for the R5 "Insight" milestone (issue #99) — see
 * `Homassy.API.Controllers.InsightsController` and `~/types/insights` for the response shapes.
 */
import type {
  BestKnownPrice,
  ConsumptionSeriesResponse,
  InventoryCompositionResponse,
  SpendByLocationResponse
} from '~/types/insights'

/**
 * The most product ids one `getBestPrices` call may carry — mirrors
 * `PriceInsightFunctions.MaxBestPriceProductIds`, which answers 400 above it. Exported so a caller
 * with a very long list can chunk rather than discover the limit as a failed request.
 */
export const MAX_BEST_PRICE_PRODUCT_IDS = 200

export const useInsightsApi = () => {
  const client = useApiClient()

  /**
   * The caller's current inventory (their own personal items plus their family's shared items),
   * broken down by product category. No parameters — always reflects "right now".
   */
  const getInventoryComposition = async () => {
    return await client.get<InventoryCompositionResponse>('/api/v1/Insights/inventory-composition')
  }

  /**
   * A dense day/week consumption series. Deliberately sends no timezone: the server resolves the
   * viewer's own saved zone from their profile, and the response's `timeZoneId` names it back.
   * `days` must be 30 or 90 and `bucket` must be 'day' or 'week' — any other value is a 400.
   */
  const getConsumption = async (days: 30 | 90, bucket: 'day' | 'week') => {
    return await client.get<ConsumptionSeriesResponse>(
      `/api/v1/Insights/consumption?days=${days}&bucket=${bucket}`
    )
  }

  /**
   * The caller's purchases in the last `days` days, broken down by shopping location and, within
   * each location, by currency. `days` must be 30 or 90 — any other value is a 400.
   */
  const getSpendByLocation = async (days: 30 | 90) => {
    return await client.get<SpendByLocationResponse>(
      `/api/v1/Insights/spend-by-location?days=${days}`
    )
  }

  /**
   * The cheapest price the household has actually paid for each of the given products — one call
   * for a whole shopping list, never one per row (the API asserts that in its own tests).
   *
   * Products with no usable purchase history are **omitted** from the map rather than returned
   * with a null value, so a missing key is the single "no price known" signal. An empty
   * `productPublicIds` answers an empty map without the server querying at all; more than
   * `MAX_BEST_PRICE_PRODUCT_IDS` ids is a 400.
   */
  const getBestPrices = async (productPublicIds: readonly string[]) => {
    return await client.post<Record<string, BestKnownPrice>>(
      '/api/v1/Insights/best-prices',
      { productPublicIds }
    )
  }

  return {
    getInventoryComposition,
    getConsumption,
    getSpendByLocation,
    getBestPrices
  }
}
