/**
 * Insights API composable
 * Family-scoped aggregation endpoints for the R5 "Insight" milestone (issue #99) — see
 * `Homassy.API.Controllers.InsightsController` and `~/types/insights` for the response shapes.
 */
import type {
  AwayDeltaResponse,
  BadgeStateResponse,
  BestKnownPrice,
  ConsumptionSeriesResponse,
  FamilyScoreboardResponse,
  InventoryCompositionResponse,
  ScoreboardWindowDays,
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

  /**
   * The family's scoreboard: per-member counters over the window with a comparison against the
   * previous equally-long one, plus the household's two streaks. `days` must be 7, 30 or 90.
   *
   * A caller with no family gets a 200 with no members rather than an error - there is no
   * household to rank, which is an answer, not a failure.
   */
  const getScoreboard = async (days: ScoreboardWindowDays) => {
    return await client.get<FamilyScoreboardResponse>(`/api/v1/Insights/scoreboard?days=${days}`)
  }

  /**
   * Every badge in the catalog as the caller stands with it.
   *
   * **Calling this has a side effect on purpose:** the server records any threshold the caller has
   * just crossed, and `justUnlocked` is true on that one response only - which is what makes the
   * unlock celebration fire exactly once. So this must not be called speculatively (a prefetch, a
   * retry loop, a poll) - each call can consume an unlock the user then never sees.
   */
  const getBadges = async () => {
    return await client.get<BadgeStateResponse>('/api/v1/Insights/badges')
  }

  /**
   * What changed in the household since the caller was last here (#127).
   *
   * `since` is optional: without it the server falls back to its own stored last-seen, and a
   * caller with neither gets an empty delta rather than their whole history. Nothing to report is
   * a 200 with zeroes, never a 204 - the client decides whether an empty delta is worth rendering
   * (it is not).
   *
   * The response echoes the window actually used, which may be clamped to 90 days - link the
   * timeline with THAT value, not the one you asked for.
   */
  const getAwayDelta = async (since?: string) => {
    const query = since ? `?since=${encodeURIComponent(since)}` : ''
    return await client.get<AwayDeltaResponse>(`/api/v1/Insights/delta${query}`)
  }

  return {
    getInventoryComposition,
    getConsumption,
    getSpendByLocation,
    getBestPrices,
    getScoreboard,
    getBadges,
    getAwayDelta
  }
}
