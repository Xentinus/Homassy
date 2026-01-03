/**
 * Authentication middleware
 * Protects routes that require authentication
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const authStore = useAuthStore()

  console.debug(`[Middleware] Checking auth for route: ${to.path}`)
  console.debug(`[Middleware] isAuthenticated before restore: ${authStore.isAuthenticated}`)
  console.debug(`[Middleware] Running in: ${import.meta.server ? 'SSR' : 'Client'}`)

  // Attempt to restore session from cookies if needed
  if (!authStore.isAuthenticated) {
    console.debug('[Middleware] Restoring from cookies...')
    await authStore.loadFromCookies()
  }

  console.debug(`[Middleware] isAuthenticated after restore: ${authStore.isAuthenticated}`)
  console.debug(`[Middleware] hasTokens: ${!!authStore.accessToken && !!authStore.refreshToken}`)

  // SSR: Validate tokens exist, but don't assume they're valid
  if (import.meta.server) {
    const hasTokens = authStore.accessToken && authStore.refreshToken
    console.debug(`[Middleware] SSR: hasTokens=${hasTokens}`)
    
    if (!hasTokens) {
      console.debug('[Middleware] SSR: No tokens, redirecting to login')
      return navigateTo('/auth/login')
    }
    
    // Allow SSR to render, but client will validate tokens
    // If tokens are invalid, client-side will catch and redirect
    console.debug('[Middleware] SSR: Tokens exist, allowing render (client will validate)')
    return
  }

  // Client-side: Validate tokens and recover user data
  if (import.meta.client) {
    const hasTokens = authStore.accessToken && authStore.refreshToken
    const hasUser = !!authStore.user
    
    console.debug(`[Middleware] Client: hasTokens=${hasTokens}, hasUser=${hasUser}`)
    
    if (hasTokens && !hasUser) {
      // Tokens exist but no user - attempt recovery
      console.debug('[Middleware] Attempting token-based recovery...')
      try {
        await authStore.refreshAccessToken()
        console.debug('[Middleware] Token refreshed successfully')
        
        // Double-check authentication after refresh
        if (!authStore.isAuthenticated) {
          console.error('[Middleware] Still not authenticated after refresh')
          authStore.clearAuthData()
          return navigateTo('/auth/login')
        }
      } catch (e) {
        console.error('[Middleware] Token refresh failed, clearing auth and redirecting:', e)
        // Immediate clear - don't wait for async logout
        authStore.clearAuthData()
        return navigateTo('/auth/login')
      }
    } else if (!hasTokens) {
      // No tokens at all
      console.debug('[Middleware] No tokens found, redirecting to login')
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
