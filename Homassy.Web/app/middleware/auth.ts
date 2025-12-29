/**
 * Authentication middleware
 * Protects routes that require authentication
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const authStore = useAuthStore()

  console.debug(`[Middleware] Checking auth for route: ${to.path}`)
  console.debug(`[Middleware] isAuthenticated before restore: ${authStore.isAuthenticated}`)

  // Attempt to restore session from cookies if needed
  if (!authStore.isAuthenticated) {
    console.debug('[Middleware] Restoring from cookies...')
    await authStore.loadFromCookies()
  }

  console.debug(`[Middleware] isAuthenticated after restore: ${authStore.isAuthenticated}`)
  console.debug(`[Middleware] hasTokens: ${!!authStore.accessToken && !!authStore.refreshToken}`)

  // If tokens exist but user missing, try to recover once (SSR-safe)
  if (!authStore.isAuthenticated && authStore.accessToken && authStore.refreshToken) {
    console.debug('[Middleware] Attempting token-based recovery...')
    try {
      // First, refresh the access token in case it's expired
      console.debug('[Middleware] Refreshing access token...')
      await authStore.refreshAccessToken()
      console.debug('[Middleware] Token refreshed successfully')
    } catch (e) {
      console.error('[Middleware] Token refresh failed:', e)
      authStore.clearAuthData()
      return navigateTo('/auth/login')
    }
  }

  console.debug(`[Middleware] Final isAuthenticated: ${authStore.isAuthenticated}`)

  // Check if user is authenticated after attempting restore
  if (!authStore.isAuthenticated) {
    // Redirect to login page
    console.debug(`[Middleware] Redirecting to login, redirect=${to.fullPath}`)
    return navigateTo('/auth/login')
  }
})
