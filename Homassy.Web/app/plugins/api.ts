/**
 * API Plugin (Kratos Version)
 * Provides $api for making HTTP requests throughout the app
 * Authentication: Kratos session cookie is sent automatically via credentials: 'include'
 * Behavior: on 401 -> redirect to /auth/login
 */
export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase || 'http://localhost:5226'

  // Base client - Kratos session cookie is sent automatically
  const rawApi = $fetch.create({
    baseURL,
    credentials: 'include' // Send cookies with every request
  })

  // Wrapper to handle 401 errors
  const $api = async <T>(request: string, options: any = {}): Promise<T> => {
    try {
      return await rawApi<T>(request, options)
    } catch (error: any) {
      const status = error?.response?.status || error?.statusCode
      const isUnauthorized = status === 401

      // Handle 401 errors
      if (isUnauthorized) {
        console.error('[API] 401 Unauthorized - Kratos session invalid or expired')
        const authStore = useAuthStore()

        // Check current location to prevent redirect loops
        const isOnLoginPage = typeof window !== 'undefined' && 
                              window.location.pathname.startsWith('/auth/')

        // Clear local auth state
        authStore.clearAuthData()

        // Redirect to login if not already there
        if (!isOnLoginPage && typeof window !== 'undefined') {
          console.debug('[API] Redirecting to login')
          await navigateTo('/auth/login')
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
