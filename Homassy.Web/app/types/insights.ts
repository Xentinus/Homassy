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

// --- #128 price history -----------------------------------------------------------------------
//
// Mirrors `Homassy.API/Models/Insights/PriceHistoryResponse.cs`. Two enum conventions meet here,
// both inherited from how System.Text.Json treats them and neither chosen by this file:
// `unit`/`canonicalUnit` are plain enum properties and arrive as **numbers** (which is what the
// `enums.unit.*` locale keys are keyed by, so they translate directly), while `currency` and the
// `byCurrency` keys arrive as enum **names** (`"Huf"`, `"Eur"`) — see that field's own remarks in
// the C# record for why the currency deliberately does not come across as a number.

/** One purchase, re-expressed as a price per the enclosing group's `canonicalUnit`. */
export interface PricePoint {
  /** ISO-8601 UTC instant. */
  purchasedAt: string
  unitPrice: number
  /** The quantity as purchased (500 for a 500 g jar), paired with `unit` — not the normalized value. */
  quantity: number
  /** `Unit` enum value as a number — index straight into `enums.unit.*`. */
  unit: number
}

/**
 * One shop's purchases within one currency and one basis, plus the five statistics computed over
 * those points alone. `shoppingLocationPublicId` is `null` for the "unknown location" bucket.
 */
export interface ShopPriceSeries {
  shoppingLocationPublicId: string | null
  locationName: string
  /** Oldest first, so a line chart can plot them in the order it receives them. */
  points: PricePoint[]
  min: number
  max: number
  latest: number
  average: number
  count: number
}

/**
 * Every purchase that is comparable with every other one in the group: one currency (the
 * enclosing `byCurrency` key) and one basis. `normalized` is `false` for a countable basis — the
 * prices are still comparable *inside* the group, but the label must say "per pack" rather than
 * implying a weight comparison, which is the whole point of the flag.
 */
export interface PriceBasisGroup {
  /** `"per-kg"`, `"per-l"`, `"per-pack"`, … — stable and machine-readable, never a display string. */
  seriesKey: string
  canonicalUnit: number
  normalized: boolean
  shops: ShopPriceSeries[]
}

/** The cheapest price the household has actually paid, within one currency and one basis. */
export interface BestKnownPrice {
  unitPrice: number
  /** The `Currency` enum's name, e.g. `"Huf"` — a well-formed code `Intl.NumberFormat` accepts. */
  currency: string
  shoppingLocationPublicId: string | null
  locationName: string
  /** ISO-8601 UTC instant. */
  at: string
  seriesKey: string
}

/**
 * One product's purchase price history for the caller's household. Currencies are kept apart and
 * never converted — this milestone has no exchange rate — so `byCurrency` may hold more than one
 * entry, and nothing in the response mixes two of them.
 */
export interface PriceHistoryResponse {
  byCurrency: Record<string, PriceBasisGroup[]>
  bestKnown: BestKnownPrice | null
  /**
   * The newest purchase's unit price over its own group's average: above 1 means "dearer than
   * usual". Deliberately a ratio, not a boolean — the client picks the margin worth mentioning
   * (this UI uses 1.1). `null` when the newest purchase's group holds only that one purchase.
   */
  latestAboveAverageRatio: number | null
}

/** The windows `GET /api/v1/Product/{id}/price-history` accepts — any other value is a 400. */
export type PriceHistoryWindowDays = 90 | 180 | 365
