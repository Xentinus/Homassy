/**
 * Reading a failed `$fetch` without reaching for `any`.
 *
 * ofetch rejects with a `FetchError`, but only when a server actually answered;
 * a request that never left the device rejects with a bare `TypeError`. Every
 * caller therefore has to treat the caught value as `unknown` and ask these two
 * questions instead of assuming a shape.
 */
import type { FetchError } from 'ofetch'

/**
 * HTTP status of a failed request, or `undefined` when nothing answered — which
 * is the distinction the API client uses to decide who reports the failure.
 *
 * Both spellings are checked because they come from different layers: `response`
 * is ofetch's, `statusCode` is what Nitro and `createError` set.
 */
export function responseStatus(error: unknown): number | undefined {
  const failure = error as FetchError<unknown> | undefined
  return failure?.response?.status ?? failure?.statusCode
}

/** The parsed response body of a failed request, if there was one. */
export function responseData(error: unknown): unknown {
  return (error as FetchError<unknown> | undefined)?.data
}

/**
 * The property named `key` of a failed request's body, when the body is an
 * object. Saves every caller a `typeof === 'object'` dance for the one or two
 * fields it needs off an error payload.
 */
export function responseDataProperty(error: unknown, key: string): unknown {
  const data = responseData(error)
  if (!data || typeof data !== 'object') return undefined
  return (data as Record<string, unknown>)[key]
}
