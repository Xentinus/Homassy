/**
 * Insights API composable
 * Family-scoped aggregation endpoints for the R5 "Insight" milestone (issue #99) — see
 * `Homassy.API.Controllers.InsightsController` and `~/types/insights` for the response shapes.
 */
import type {
  ConsumptionSeriesResponse,
  InventoryCompositionResponse,
  SpendByLocationResponse
} from '~/types/insights'

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

  return {
    getInventoryComposition,
    getConsumption,
    getSpendByLocation
  }
}
