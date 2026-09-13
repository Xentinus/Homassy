/**
 * API Plugin (Kratos Version)
 * Provides $api for making HTTP requests throughout the app
 * Authentication: Kratos session cookie is sent automatically via credentials: 'include'
 * Behavior: on 401 -> redirect to /auth/login, carrying the current route as `return_to`
 */
import type { ApiFetch } from '~/types/api'
import { loginRedirectFor } from '~/utils/authRedirect'
import { responseStatus } from '~/utils/httpErrors'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase || 'http://localhost:5226'

  // Resolved here, in the plugin body, where the Nuxt instance context exists. The 401 handler
  // below runs after an await inside a rejected request, which is outside that context —
  // calling useRouter() there would throw, and the redirect would be skipped entirely.
  const router = useRouter()

  // Base client - Kratos session cookie is sent automatically
  const rawApi = $fetch.create({
    baseURL,
    credentials: 'include' // Send cookies with every request
  })

  // Wrapper to handle 401 errors
  const $api: ApiFetch = async <T>(request: string, options = {}): Promise<T> => {
    try {
      return await rawApi<T>(request, options)
    } catch (error) {
      const isUnauthorized = responseStatus(error) === 401

      // Handle 401 errors
      if (isUnauthorized) {
        console.error('[API] 401 Unauthorized - Kratos session invalid or expired')
        const authStore = useAuthStore()

        // Clear local auth state
        authStore.clearAuthData()

        if (typeof window !== 'undefined') {
          /**
           * Carry the destination, exactly as `middleware/auth.ts` does (#87). This is the
           * *expiry* case rather than the cold-start one, and on mobile it is the common one:
           * a session that lapses while the app is open, or an installed PWA reopened after it
           * lapsed. Redirecting to a bare `/auth/login` threw away whatever the user was doing
           * and dropped them on the calendar after signing back in.
           *
           * The router's current route, not `window.location`: it is the resolved app path, and
           * it is what the login page will push. `loginRedirectFor` answers `null` on an auth
           * route — nothing to preserve, and nowhere to bounce to without looping.
           */
          const redirect = loginRedirectFor(router.currentRoute.value.fullPath)

          if (redirect) {
            console.debug('[API] Redirecting to login')
            await navigateTo(redirect)
          }
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
