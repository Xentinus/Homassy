/**
 * Error Codes API composable
 * Provides error code related API calls
 */
import type { ApiResponse } from '~/types/common'
import type { ErrorCodeInfo } from '~/types/errorCodes'

export const useErrorCodesApi = () => {
  const { $api } = useNuxtApp()

  /**
   * Get all error codes with descriptions
   */
  const getErrorCodes = async (): Promise<ApiResponse<ErrorCodeInfo[]>> => {
    return await $api<ApiResponse<ErrorCodeInfo[]>>('/api/v1/ErrorCodes', {
      method: 'GET'
    })
  }

  /**
   * Get error codes for a specific group
   * @param group - Error code group prefix (e.g., "AUTH", "USER", "PRODUCT")
   */
  const getErrorCodesByGroup = async (group: string): Promise<ApiResponse<ErrorCodeInfo[]>> => {
    return await $api<ApiResponse<ErrorCodeInfo[]>>(`/api/v1/ErrorCodes/${group}`, {
      method: 'GET'
    })
  }

  return {
    getErrorCodes,
    getErrorCodesByGroup
  }
}
