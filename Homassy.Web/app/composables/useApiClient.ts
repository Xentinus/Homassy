/**
 * API Client wrapper with toast/error handling
 */
import type { ApiResponse } from '~/types/common'
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
  const { $i18n } = nuxtApp

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
      // Build request headers
      const requestHeaders: Record<string, string> = {
        ...headers
      }

      // Only set JSON content type when body is not FormData and caller did not override
      const isFormData = typeof FormData !== 'undefined' && body instanceof FormData
      if (!isFormData && !requestHeaders['Content-Type']) {
        requestHeaders['Content-Type'] = 'application/json'
      }

      // Make the API request
      const response = await ($api as any)(endpoint, {
        method,
        body,
        headers: requestHeaders
      }) as ApiResponse<T>

      // Show success toast if enabled
      if (showSuccessToast && successMessage) {
        toast.add({
          title: $i18n.t('toast.success'),
          description: successMessage,
          color: 'success',
          icon: 'i-heroicons-check-circle'
        })
      }

      return response
    } catch (error: any) {
      // Handle API response errors with error codes
      if (error.data && error.data.errorCodes && Array.isArray(error.data.errorCodes)) {
        const errorMessages = getErrorMessages(error.data.errorCodes)

        if (showErrorToast) {
          toast.add({
            title: $i18n.t('toast.error'),
            description: $i18n.t('toast.requestError'),
            color: 'error',
            icon: 'i-heroicons-x-circle'
          })
        }

        return error.data as ApiResponse<T>
      }

      // Handle network or other errors
      if (showErrorToast) {
        toast.add({
          title: $i18n.t('toast.error'),
          description: $i18n.t('toast.requestError'),
          color: 'error',
          icon: 'i-heroicons-x-circle'
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
