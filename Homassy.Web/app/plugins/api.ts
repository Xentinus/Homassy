/**
 * API Plugin
 * Provides $api for making HTTP requests throughout the app
 * Token source of truth: cookies only
 * Behavior: attach Bearer from cookies, on 401 try refresh once, else redirect to /auth/login
 */
export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase || 'http://localhost:5226'

  // Base client that only attaches headers; retry logic lives in wrapper below
  const rawApi = $fetch.create({
    baseURL,
    credentials: 'include',
    async onRequest({ request, options }) {
      // Read token and expiration directly from cookies to work in SSR and client
      const accessTokenCookie = useCookie('homassy_access_token')
      const accessTokenExpiresCookie = useCookie('homassy_access_token_expires')
      const refreshTokenCookie = useCookie('homassy_refresh_token')

      const token = accessTokenCookie.value as unknown as string | null
      const expiresAt = accessTokenExpiresCookie.value as unknown as string | null
      const refreshToken = refreshTokenCookie.value as unknown as string | null

      // Check if access token is expired
      // Only attempt refresh if we have both tokens (prevents unnecessary 401s on first visit)
      if (token && expiresAt && refreshToken) {
        const expiryTime = new Date(expiresAt).getTime()
        const now = Date.now()
        const isExpired = expiryTime <= now
        const isRefreshEndpoint = request.toString().includes('/Auth/refresh')
        const isLoginEndpoint = request.toString().includes('/Auth/verify-code') ||
                                request.toString().includes('/Auth/request-code')

        // If token is expired and not a refresh/login request, refresh proactively
        if (isExpired && !isRefreshEndpoint && !isLoginEndpoint) {
          console.debug('[API] Access token expired, refreshing before request...')
          const authStore = useAuthStore()

          try {
            await authStore.refreshAccessToken()
            // After refresh, read the new token
            const newToken = (useCookie('homassy_access_token').value) as unknown as string | null
            if (newToken) {
              const headers = new Headers(options.headers as HeadersInit | undefined)
              headers.set('Authorization', `Bearer ${newToken}`)
              options.headers = headers
              return
            }
          } catch (error) {
            console.error('[API] Proactive token refresh failed', error)
            // Let the request continue with expired token, 401 handler will deal with it
          }
        }
      }

      // Attach current token (might be expired, 401 handler will refresh)
      if (token) {
        const headers = new Headers(options.headers as HeadersInit | undefined)
        headers.set('Authorization', `Bearer ${token}`)
        options.headers = headers
      }
    }
  })

  // Wrapper to handle 401 -> refresh -> single retry
  const $api = async <T>(request: string, options: any = {}): Promise<T> => {
    try {
      return await rawApi<T>(request, options)
    } catch (error: any) {
      const status = error?.response?.status || error?.statusCode
      const isUnauthorized = status === 401
      const isLogout = request.toString().includes('/Auth/logout')
      const isRefresh = request.toString().includes('/Auth/refresh')
      const alreadyRetried = Boolean(options?._retried)

      // Handle 401 errors (except for logout and refresh endpoints)
      if (isUnauthorized && !isLogout && !isRefresh) {
        const authStore = useAuthStore()

        // Check if we have a refresh token to attempt refresh
        const refreshTokenCookie = useCookie('homassy_refresh_token')
        const hasRefreshToken = !!refreshTokenCookie.value

        // If this is the first attempt and we have a refresh token, try to refresh
        if (!alreadyRetried && hasRefreshToken) {
          try {
            // Attempt token refresh (this is guarded and will call logout if refresh fails)
            await authStore.refreshAccessToken()

            // Retry the original request with refreshed token
            const retryOptions = { ...options, _retried: true }
            return await rawApi<T>(request, retryOptions)
          } catch (refreshError) {
            // Refresh failed - clear cookies and navigate to login
            authStore.clearAuthData()
            if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/auth/login')) {
              navigateTo('/auth/login')
            }
            throw refreshError
          }
        }

        // No refresh token or already retried - clear auth data and navigate to login
        authStore.clearAuthData()
        if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/auth/login')) {
          navigateTo('/auth/login')
        }
      }

      throw error
    }
  }

  return {
    provide: {
      api: $api
    }
  }
})