/**
 * Health API composable
 * Provides health check related API calls
 */
import type { ApiResponse } from '~/types/common'
import type { HealthCheckResponse } from '~/types/health'

export const useHealthApi = () => {
  const { $api } = useNuxtApp()

  /**
   * Basic health check - overall application status
   */
  const getHealth = async (): Promise<ApiResponse<HealthCheckResponse>> => {
    return await $api<ApiResponse<HealthCheckResponse>>('/api/v1/Health', {
      method: 'GET'
    })
  }

  /**
   * Readiness probe - checks if ready to receive traffic
   */
  const getReadiness = async (): Promise<ApiResponse<HealthCheckResponse>> => {
    return await $api<ApiResponse<HealthCheckResponse>>('/api/v1/Health/ready', {
      method: 'GET'
    })
  }

  /**
   * Liveness probe - verify application process is running
   */
  const getLiveness = async (): Promise<ApiResponse<HealthCheckResponse>> => {
    return await $api<ApiResponse<HealthCheckResponse>>('/api/v1/Health/live', {
      method: 'GET'
    })
  }

  return {
    getHealth,
    getReadiness,
    getLiveness
  }
}
