/**
 * API Plugin
 * Provides $api for making HTTP requests throughout the app
 */
export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase || 'http://localhost:5226'

  const $api = $fetch.create({
    baseURL,
    credentials: 'include',
    onRequest({ request, options }) {
      // Read token directly from cookies to work in SSR and client
      const accessTokenCookie = useCookie('homassy_access_token')
      const token = accessTokenCookie.value as unknown as string | null
      
      console.debug(`[API:onRequest] Token from cookie:`, token ? `${token.slice(0, 20)}...` : 'null')
      console.debug(`[API:onRequest] Endpoint:`, request.toString())
      
      if (token) {
        const headers = (options.headers || {}) as unknown as Record<string, string>
        headers.Authorization = `Bearer ${token}`
        options.headers = headers as any
        console.debug(`[API:onRequest] Authorization header set`)
      } else {
        console.debug(`[API:onRequest] No token - Authorization header NOT set`)
      }
    },
    onResponseError({ response, request }) {
      // Handle 401 - token might be expired
      // Don't trigger logout if we're already calling logout endpoint
      if (response.status === 401 && !request.toString().includes('/Auth/logout')) {
        try {
          const authStore = useAuthStore()
          // Just clear the auth data locally, don't make another API call
          authStore.clearAuthData()
          
          // Redirect to login if not already there
          const currentPath = typeof window !== 'undefined' ? window.location.pathname : ''
          if (!currentPath.startsWith('/auth/login')) {
            navigateTo('/auth/login')
          }
        } catch (error) {
          console.error('Failed to handle 401:', error)
        }
      }
    }
  })

  return {
    provide: {
      api: $api
    }
  }
})