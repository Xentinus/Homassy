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
      // Get token from auth store if available
      try {
        const authStore = useAuthStore()
        if (authStore.accessToken) {
          const headers = (options.headers || {}) as unknown as Record<string, string>
          headers.Authorization = `Bearer ${authStore.accessToken}`
          options.headers = headers as any
        }
      } catch (error) {
        // Auth store might not be initialized yet
        console.debug('Could not attach auth token:', error)
      }
    },
    onResponseError({ response }) {
      // Handle 401 - token might be expired
      if (response.status === 401) {
        try {
          const authStore = useAuthStore()
          // Store will handle token refresh or logout
          authStore.logout()
        } catch (error) {
          console.error('Failed to logout on 401:', error)
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
