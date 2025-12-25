/**
 * API Client wrapper with automatic token refresh and error handling
 */
import type { ApiResponse } from '~/types/api'
import { getErrorMessages } from '~/utils/errorCodes'

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
  showErrorToast?: boolean
  showSuccessToast?: boolean
  successMessage?: string
}

export const useApiClient = () => {
  const toast = useToast()
  const nuxtApp = useNuxtApp()
  const $api = nuxtApp.$api as any
  
  // Get auth store (will be used for token management)
  const authStore = useAuthStore()

  /**
   * Make API request with automatic error handling and toast notifications
   */
  const request = async <T>(
    endpoint: string,
    options: RequestOptions = {}
  ): Promise<ApiResponse<T>> => {
    const {
      method = 'GET',
      body,
      headers = {},
      showErrorToast = true,
      showSuccessToast = false,
      successMessage
    } = options

    try {
      // Get access token from auth store
      const accessToken = authStore.accessToken

      // Build request headers
      const requestHeaders: Record<string, string> = {
        'Content-Type': 'application/json',
        ...headers
      }

      // Add authorization header if token exists
      if (accessToken) {
        requestHeaders['Authorization'] = `Bearer ${accessToken}`
      }

      // Make the API request
      const response = await $api<ApiResponse<T>>(endpoint, {
        method,
        body,
        headers: requestHeaders,
        credentials: 'include' // Include cookies for httpOnly cookie support
      })

      // Show success toast if enabled
      if (showSuccessToast && successMessage) {
        toast.add({
          title: 'Success',
          description: successMessage,
          color: 'success',
          icon: 'i-heroicons-check-circle'
        })
      }

      return response
    } catch (error: unknown) {
      // Handle 401 Unauthorized - attempt token refresh
      if (error.statusCode === 401 && authStore.refreshToken) {
        try {
          // Attempt to refresh the token
          await authStore.refreshAccessToken()
          
          // Retry the original request with new token
          const accessToken = authStore.accessToken
          const requestHeaders: Record<string, string> = {
            'Content-Type': 'application/json',
            ...headers
          }

          if (accessToken) {
            requestHeaders['Authorization'] = `Bearer ${accessToken}`
          }

          const retryResponse = await $api<ApiResponse<T>>(endpoint, {
            method,
            body,
            headers: requestHeaders,
            credentials: 'include'
          })

          if (showSuccessToast && successMessage) {
            toast.add({
              title: 'Success',
              description: successMessage,
              color: 'success',
              icon: 'i-heroicons-check-circle'
            })
          }

          return retryResponse
        } catch (refreshError: unknown) {
          // Refresh failed - logout user
          await authStore.logout()
          
          if (showErrorToast) {
            toast.add({
              title: 'Session expired',
              description: 'Please log in again.',
              color: 'error',
              icon: 'i-heroicons-x-circle',
              timeout: 5000
            })
          }

          throw refreshError
        }
      }

      // Handle API response errors with error codes
      if (error.data && error.data.errorCodes && Array.isArray(error.data.errorCodes)) {
        const errorMessages = getErrorMessages(error.data.errorCodes)
        
        if (showErrorToast && errorMessages.length > 0) {
          // Show first error message in toast
          toast.add({
            title: 'Error',
            description: errorMessages[0],
            color: 'error',
            icon: 'i-heroicons-x-circle',
            timeout: 5000
          })
        }

        return error.data as ApiResponse<T>
      }

      // Handle network or other errors
      if (showErrorToast) {
        const errorMessage = error.message || 'An unknown error occurred'
        toast.add({
          title: 'Error',
          description: errorMessage,
          color: 'error',
          icon: 'i-heroicons-x-circle',
          timeout: 5000
        })
      }

      throw error
    }
  }

  /**
   * GET request
   */
  const get = <T>(endpoint: string, options: Omit<RequestOptions, 'method' | 'body'> = {}) => {
    return request<T>(endpoint, { ...options, method: 'GET' })
  }

  /**
   * POST request
   */
  const post = <T>(endpoint: string, body?: unknown, options: Omit<RequestOptions, 'method'> = {}) => {
    return request<T>(endpoint, { ...options, method: 'POST', body })
  }

  /**
   * PUT request
   */
  const put = <T>(endpoint: string, body?: unknown, options: Omit<RequestOptions, 'method'> = {}) => {
    return request<T>(endpoint, { ...options, method: 'PUT', body })
  }

  /**
   * DELETE request
   */
  const del = <T>(endpoint: string, options: Omit<RequestOptions, 'method' | 'body'> = {}) => {
    return request<T>(endpoint, { ...options, method: 'DELETE' })
  }

  return {
    request,
    get,
    post,
    put,
    delete: del
  }
}
