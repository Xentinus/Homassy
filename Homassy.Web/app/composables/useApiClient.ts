/**
 * API Client wrapper with toast/error handling
 */
import type { ApiResponse } from '~/types/common'

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
  showErrorToast?: boolean
  showSuccessToast?: boolean
  successMessage?: string
  /**
   * The caller's contextual message ("saving the product failed"), shown when the response
   * carries no `errorCodes` of its own. Pass this instead of toasting from the caller: a
   * failure is reported once, by whichever side owns that path (see `request`).
   */
  errorMessage?: string
}

/**
 * What an API composable method forwards on behalf of its caller. A form passes the message
 * it used to toast itself; `showErrorToast: false` is the opt-out for callers that own their
 * own reporting entirely.
 */
export type ApiCallOptions = Pick<RequestOptions, 'errorMessage' | 'showErrorToast'>

export const useApiClient = () => {
  const toast = useToast()
  const nuxtApp = useNuxtApp()
  const $api = nuxtApp.$api as any
  const { $i18n } = nuxtApp

  /** Falls back to the raw code when a code has no translation. */
  const localizeErrorCode = (code: string) => {
    const key = `errorCodes.${code}`
    const translated = $i18n.t(key)
    return translated === key ? code : translated
  }

  /**
   * Make an API request, reporting a failure exactly once.
   *
   * Which side reports it depends on whether the API answered at all:
   *
   * - **The API answered with an error status.** `request` returns a failure-shaped
   *   `ApiResponse` and shows the only toast. Callers handle it on their `else` branch and
   *   must not toast; they pass their context as `errorMessage` instead.
   * - **The request never reached the API.** `request` rethrows and stays silent, leaving the
   *   caller's own `catch` to report it.
   *
   * Doing both on one path — toasting *and* rethrowing — is what used to give every failed
   * request two toasts.
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
      successMessage,
      errorMessage
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
      const status = error?.response?.status ?? error?.statusCode

      // No status means no response: a network or transport failure. Rethrow it silently so
      // the caller's catch is the single report.
      if (!status) throw error

      const responseBody = error.data
      const envelope = responseBody && typeof responseBody === 'object' && 'success' in responseBody
        ? responseBody as ApiResponse<T>
        : undefined

      // The API's own envelope carries specific, localized codes.
      const codes = Array.isArray(envelope?.errorCodes) && envelope.errorCodes.length
        ? envelope.errorCodes
        : undefined

      // MVC's own model-validation answer, which has no codes at all.
      const validationErrors = normalizeValidationErrors(responseBody?.errors)

      if (validationErrors) {
        // The server's English message never reaches the UI, so leave it here — a form the
        // client validates being rejected anyway is a bug, and this is the only trail to it.
        console.warn(`[API] ${method} ${endpoint} rejected by model validation:`, responseBody.errors)
      }

      // A 401 is already handled by the $api plugin, which clears the auth state and
      // redirects to the login page. A toast on the way out is noise.
      if (showErrorToast && status !== 401) {
        toast.add({
          title: $i18n.t('toast.error'),
          description: codes
            ? codes.map(localizeErrorCode).join('\n')
            : errorMessage || $i18n.t(validationErrors ? 'toast.validationError' : 'toast.requestError'),
          color: 'error',
          icon: 'i-heroicons-x-circle'
        })
      }

      return {
        ...envelope,
        success: false,
        errorCodes: codes,
        validationErrors,
        timestamp: envelope?.timestamp ?? new Date().toISOString()
      }
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
  const del = <T>(endpoint: string, body?: unknown, options: Omit<RequestOptions, 'method'> = {}) => {
    return request<T>(endpoint, { ...options, method: 'DELETE', body })
  }

  return {
    request,
    get,
    post,
    put,
    delete: del
  }
}
