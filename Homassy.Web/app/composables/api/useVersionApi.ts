/**
 * Version API composable
 * Provides version information API calls
 */
import type { ApiResponse } from '~/types/common'
import type { VersionInfo } from '~/types/version'

export const useVersionApi = () => {
  const { $api } = useNuxtApp()

  /**
   * Get current API version information
   */
  const getVersion = async (): Promise<ApiResponse<VersionInfo>> => {
    return await $api<ApiResponse<VersionInfo>>('/api/Version', {
      method: 'GET'
    })
  }

  return {
    getVersion
  }
}
