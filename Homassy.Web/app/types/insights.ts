/**
 * Types for the R5 "Insight" endpoints (issue #99). Field names mirror the C# response records
 * exactly (camelCase, matching the API's default JSON casing) — see
 * `Homassy.API/Controllers/InsightsController.cs` and `Homassy.API/Models/Insights/*.cs`.
 */
import type { ProductCategory } from './enums'

/**
 * One category's share of the family's current inventory. Never carries `category ===
 * ProductCategory.Other` — see `InventoryCompositionResponse.otherCount`, which is the single
 * "not individually listed" bucket for that case too.
 */
export interface CompositionSlice {
  category: ProductCategory
  count: number
  share: number
}

/**
 * The family's current inventory (the caller's own personal items plus their family's shared
 * items) broken down by product category: the largest categories as their own `slices`,
 * everything past that folded into `otherCount`.
 */
export interface InventoryCompositionResponse {
  slices: CompositionSlice[]
  otherCount: number
  totalCount: number
}

/**
 * Mirrors `Homassy.API.Functions.SeriesBucket` (`Day = 0`, `Week = 1`) as it appears on the wire —
 * a plain enum property, so it serializes as a number, unlike a `Currency` used as a *dictionary
 * key* (see `LocationSpend.spendByCurrency`), which serializes as its name. The page always
 * derives the bucket it asks for from its own `days` selection rather than reading this back.
 */
export type ApiSeriesBucket = 0 | 1

/**
 * One bucketed point. `bucket` is a timezone-free calendar date — the server's own `DateOnly`
 * serializes as `"yyyy-MM-dd"` — and must be turned into a chart x-value with `Date.UTC`, never
 * `new Date(y, m, d)`; see `parseDateOnlyUtc` in `pages/insights/index.vue`.
 */
export interface SeriesPoint {
  bucket: string
  value: number
}

/**
 * A dense day/week consumption series for the caller, bucketed in their own saved timezone
 * (resolved server-side — the request never sends one).
 */
export interface ConsumptionSeriesResponse {
  points: SeriesPoint[]
  bucket: ApiSeriesBucket
  timeZoneId: string
}

/**
 * One shopping location's purchases within the requested window. `shoppingLocationPublicId` is
 * `null` for the single "unknown location" bucket every purchase with no shopping location tag
 * folds into. `spendByCurrency` is keyed by the `Currency` enum's own name (e.g. `"Huf"`,
 * `"Eur"`) — a `Dictionary<Currency, decimal>` serializes its enum key as a name, unlike a plain
 * `Currency` property elsewhere in the API, which serializes as a number (and unlike the
 * frontend's own numeric `Currency` in `~/types/enums`, whose values do not need to, and do not,
 * apply here). Never summed across currencies and never converted between them — this milestone
 * has no exchange rate.
 */
export interface LocationSpend {
  shoppingLocationPublicId: string | null
  locationName: string
  itemCount: number
  spendByCurrency: Record<string, number>
}

/** The caller's purchases within the requested window, broken down by shopping location. */
export interface SpendByLocationResponse {
  locations: LocationSpend[]
}
