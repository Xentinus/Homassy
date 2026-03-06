/**
 * Statistics API composable
 * Provides global statistics API calls
 */
import type { ApiResponse } from '~/types/common'
import type { GlobalStatistics } from '~/types/statistics'

export const useStatisticsApi = () => {
  const { $api } = useNuxtApp()

  /**
   * Get global platform statistics
   */
  const getStatistics = async (): Promise<ApiResponse<GlobalStatistics>> => {
    return await $api<ApiResponse<GlobalStatistics>>('/api/v1/statistics', {
      method: 'GET'
    })
  }

  return {
    getStatistics
  }
}
