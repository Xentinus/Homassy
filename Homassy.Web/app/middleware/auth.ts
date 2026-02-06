/**
 * Authentication middleware (Kratos Version)
 * Protects routes that require authentication using Ory Kratos sessions
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const authStore = useAuthStore()

  console.debug(`[Middleware] Checking auth for route: ${to.path}`)
  console.debug(`[Middleware] isAuthenticated before init: ${authStore.isAuthenticated}`)
  console.debug(`[Middleware] Running in: ${import.meta.server ? 'SSR' : 'Client'}`)

  // SSR: Allow render - client will validate Kratos session
  // Kratos sessions are managed via httpOnly cookies, so SSR can't check them directly
  if (import.meta.server) {
    console.debug('[Middleware] SSR: Allowing render, client will validate Kratos session')
    return
  }

  // Client-side: Initialize auth state and validate Kratos session
  if (import.meta.client) {
    // Initialize auth state if not already done
    if (!authStore.initialized) {
      console.debug('[Middleware] Initializing auth state...')
      await authStore.initialize()
    }

    const hasSession = authStore.session?.active === true
    const hasUser = !!authStore.user

    console.debug(`[Middleware] Client: hasSession=${hasSession}, hasUser=${hasUser}`)

    if (!hasSession || !hasUser) {
      // No valid session - redirect to login
      console.debug('[Middleware] No valid Kratos session, redirecting to login')
      authStore.clearAuthData()
      return navigateTo('/auth/login')
    }

    // Check if session is expired
    if (!authStore.isSessionValid()) {
      console.debug('[Middleware] Kratos session expired, attempting refresh...')
      try {
        const refreshed = await authStore.refreshSession()
        if (!refreshed) {
          console.debug('[Middleware] Session refresh failed, redirecting to login')
          authStore.clearAuthData()
          return navigateTo('/auth/login')
        }
      } catch (e) {
        console.error('[Middleware] Session refresh error:', e)
        authStore.clearAuthData()
        return navigateTo('/auth/login')
      }
    }
  }

  console.debug(`[Middleware] Final isAuthenticated: ${authStore.isAuthenticated}`)

  // Final check - must be authenticated
  if (!authStore.isAuthenticated) {
    console.debug(`[Middleware] Redirecting to login, redirect=${to.fullPath}`)
    return navigateTo('/auth/login')
  }
})

