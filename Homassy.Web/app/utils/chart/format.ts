/**
 * Locale-aware label formatting for the hand-rolled SVG chart layer (R5 "Insight").
 *
 * Sibling to `scale.ts` and `path.ts` — same rules apply here: pure functions, explicit return
 * types, no semicolons, no imports (`Intl` is a global, not a module), nothing renders anything and
 * nothing touches Vue/DOM. Later tasks (`ChartLine.vue`, `ChartBar.vue`, `PriceHistoryCard.vue`)
 * call these instead of touching `Intl` directly, so the memoisation and the currency fallback below
 * live in exactly one place. Exercised by the same `node`-environment vitest spec as its siblings,
 * with no Nuxt/Vue runtime (see `vitest.config.ts`).
 */

// --- formatter memoisation (private) ------------------------------------------------------------
//
// A chart recomputes its axis ticks on every resize and formats one label per tick; constructing a
// fresh Intl.NumberFormat/DateTimeFormat per label is the classic charting perf trap — the
// constructor itself does the expensive locale-data lookup, not `.format()`. Both caches are
// module-level, so they outlive any one chart instance, and keyed by every argument that can change
// which formatter is needed, so two calls that mean the same thing always share one instance.

const numberFormatters = new Map<string, Intl.NumberFormat>()
const dateFormatters = new Map<string, Intl.DateTimeFormat>()

const numberFormatter = (key: string, locale: string, options: Intl.NumberFormatOptions): Intl.NumberFormat => {
  const cached = numberFormatters.get(key)
  if (cached) return cached
  const formatter = new Intl.NumberFormat(locale, options)
  numberFormatters.set(key, formatter)
  return formatter
}

const dateFormatter = (key: string, locale: string, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat => {
  const cached = dateFormatters.get(key)
  if (cached) return cached
  const formatter = new Intl.DateTimeFormat(locale, options)
  dateFormatters.set(key, formatter)
  return formatter
}

// --- formatCompact -------------------------------------------------------------------------------
//
// Shortens a large number for a space-constrained label — an axis tick, a stat tile — where "12.4K"
// serves the reader better than "12400" and exact precision is not the point.

export const formatCompact = (value: number, locale: string): string =>
  numberFormatter(`compact:${locale}`, locale, { notation: 'compact' }).format(value)

// --- formatCurrency --------------------------------------------------------------------------------
//
// Renders a money amount tagged with its own currency: a price-history chart plots shops that bill
// in different currencies, so the unit has to travel with every value rather than being assumed
// once for a whole axis.

/**
 * `formatCurrency`'s fallback for a `currencyCode` that `Intl.NumberFormat` rejects outright. The
 * API's `Currency` enum is not limited to ISO 4217 — it also carries cryptocurrencies (`Doge`,
 * `Aave`, `OneInch`, …) — and the ECMAScript spec requires the constructor to throw a `RangeError`
 * for a currency argument it does not recognise as a well-formed code, rather than degrading on its
 * own. With no locale-aware currency rules to fall back to, a plain "value code" string is the only
 * rendering left that still says something true.
 */
const formatUnknownCurrency = (value: number, currencyCode: string): string => `${value} ${currencyCode}`

export const formatCurrency = (value: number, currencyCode: string, locale: string): string => {
  try {
    return numberFormatter(`currency:${locale}:${currencyCode}`, locale, {
      style: 'currency',
      currency: currencyCode
    }).format(value)
  } catch (error) {
    // Intl.NumberFormat validates `currency` in its own constructor (see numberFormatter above), so
    // the throw happens before anything is cached — a bad code fails the same way on every call,
    // and re-attempting the construction each time is the cost of that, not a bug. An unsupported
    // code is the occasional path a price history hits, not the hot loop the cache above protects.
    if (error instanceof RangeError) return formatUnknownCurrency(value, currencyCode)
    throw error
  }
}

// --- formatAxisDate --------------------------------------------------------------------------------
//
// Labels one of `timeTicks`'s boundaries (see `scale.ts`): a day tick, or a Monday-anchored week
// tick. Both drop the year — a family's purchase history realistically spans months, rarely crosses
// into a second calendar year, and the extra digits would only crowd the axis.

/**
 * Every option set below pins `timeZone: 'UTC'`. `timeTicks` computes its boundaries as pure UTC
 * millisecond arithmetic — in its own words, "no Date object and no locale/timezone dependency" —
 * so the millisecond value handed to `formatAxisDate` already *is* the boundary, not a local
 * wall-clock reading of it. Formatting it in the caller's own timezone instead (the default `Intl`
 * would otherwise pick) reinterprets that same instant against a different midnight and can name
 * the wrong calendar day — for anyone west of Greenwich, on every tick, not as an occasional edge
 * case. Pinning `'UTC'` here is what keeps the label naming the boundary it was actually computed
 * to be, regardless of which timezone the chart happens to render in.
 *
 * A week tick's date is always the Monday `scale.ts` aligned it to, so naming the weekday would
 * print "Mon" under every single label on that axis; a day tick can land on any weekday, where
 * naming it is the one thing the date alone does not already say (e.g. spotting a weekend spending
 * spike).
 */
const AXIS_DATE_OPTIONS: Record<'day' | 'week', Intl.DateTimeFormatOptions> = {
  day: { weekday: 'short', month: 'short', day: 'numeric', timeZone: 'UTC' },
  week: { month: 'short', day: 'numeric', timeZone: 'UTC' }
}

export const formatAxisDate = (ms: number, locale: string, bucket: 'day' | 'week'): string =>
  dateFormatter(`axis:${bucket}:${locale}`, locale, AXIS_DATE_OPTIONS[bucket]).format(ms)
