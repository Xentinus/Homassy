/**
 * Types for the `$api` client provided by app/plugins/api.ts.
 */
import type { FetchOptions } from 'ofetch'

/**
 * What `$api` actually is: a `$fetch` wrapper that resolves to the caller's
 * generic and handles a 401 on the way out.
 *
 * It is deliberately narrower than `typeof $fetch` — which is what app/types/nuxt.d.ts
 * used to claim `$api` was, and why every caller had to cast it back to `any`
 * before it would accept a plain `{ method, body, headers }` object.
 */
export type ApiFetch = <T>(request: string, options?: FetchOptions) => Promise<T>
