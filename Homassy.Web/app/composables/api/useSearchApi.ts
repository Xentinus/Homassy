/**
 * Global search API composable.
 * One endpoint answers every searchable entity type — see `SearchFunctions` on the API side.
 */
import type { ApiCallOptions } from '../useApiClient'
import type { GlobalSearchResponse } from '~/types/search'

export const useSearchApi = () => {
  const client = useApiClient()

  /**
   * Search everything the caller can see.
   *
   * @param query  The search term. Under two characters the server answers with no groups.
   * @param limit  Hits per group (1–20, default 5).
   */
  const search = async (
    query: string,
    limit?: number,
    options?: ApiCallOptions
  ) => {
    const params = new URLSearchParams({ q: query })
    if (limit) params.append('limit', limit.toString())

    return await client.get<GlobalSearchResponse>(
      `/api/v1/Search?${params.toString()}`,
      // A keystroke that fails is not worth a toast — the palette shows its own empty state and
      // the next keystroke retries anyway.
      { showErrorToast: false, ...options }
    )
  }

  return { search }
}
