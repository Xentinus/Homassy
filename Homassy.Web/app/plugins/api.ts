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
      // Read token directly from cookies to work in SSR and client
      const accessTokenCookie = useCookie('homassy_access_token')
      const token = accessTokenCookie.value as unknown as string | null

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

      if (isUnauthorized && !isLogout && !isRefresh && !alreadyRetried) {
        const authStore = useAuthStore()
        try {
          // Sync from cookies and refresh
          await authStore.refreshAccessToken()
          const retryOptions = { ...options, _retried: true }
          return await rawApi<T>(request, retryOptions)
        } catch (refreshError) {
          authStore.clearAuthData()
          if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/auth/login')) {
            navigateTo('/auth/login')
          }
          throw refreshError
        }
      }

      // Hard fail path: clear auth and redirect
      if (isUnauthorized && !isLogout) {
        try {
          const authStore = useAuthStore()
          authStore.clearAuthData()
          if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/auth/login')) {
            navigateTo('/auth/login')
          }
        } catch (cleanupError) {
          console.error('Failed to handle unauthorized error', cleanupError)
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