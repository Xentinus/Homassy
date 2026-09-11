/**
 * Authentication middleware (Kratos Version)
 * Protects routes that require authentication using Ory Kratos sessions
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const authStore = useAuthStore()

  /**
   * Bounce to the login page *carrying the destination* (#118, and the redirect loss
   * described in #87). The login page has always honoured `return_to` — nothing ever
   * sent it, so every unauthenticated launch landed on the calendar and threw the
   * intended route away. That is fine for a bookmark and fatal for a deep link: an
   * app-shortcut or share-target launch into a cold start *is* an unauthenticated
   * launch, and dropping the target drops the whole point of the shortcut.
   *
   * `to.fullPath` is always a router-resolved same-origin path here, but it becomes a
   * query parameter the login page pushes, so the login page validates it before
   * using it rather than trusting the shape of whatever ends up in the URL.
   */
  const toLogin = () => navigateTo({ path: '/auth/login', query: { return_to: to.fullPath } })

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
      return toLogin()
    }

    // Check if session is expired
    if (!authStore.isSessionValid()) {
      console.debug('[Middleware] Kratos session expired, attempting refresh...')
      try {
        const refreshed = await authStore.refreshSession()
        if (!refreshed) {
          console.debug('[Middleware] Session refresh failed, redirecting to login')
          authStore.clearAuthData()
          return toLogin()
        }
      } catch (e) {
        console.error('[Middleware] Session refresh error:', e)
        authStore.clearAuthData()
        return toLogin()
      }
    }
  }

  console.debug(`[Middleware] Final isAuthenticated: ${authStore.isAuthenticated}`)

  // Final check - must be authenticated
  if (!authStore.isAuthenticated) {
    console.debug(`[Middleware] Redirecting to login, redirect=${to.fullPath}`)
    return toLogin()
  }
})

