/**
 * Auth initialization plugin
 * Checks for existing Kratos session on client startup.
 * Must run client-side only: Kratos session cookies are httpOnly and cannot
 * be read server-side; SSR init would always fail inside Docker (localhost:4433
 * is not reachable from the container) and would poison the store with
 * initialized=true / session=null, causing the middleware to redirect to login.
 */
export default defineNuxtPlugin(async () => {
  // Skip on SSR — middleware already returns early on server
  if (import.meta.server) {
    return
  }

  const authStore = useAuthStore()

  // Load authentication state by checking the Kratos session
  await authStore.loadFromCookies()

  // Setup visibility listener to handle long-running background tabs
  authStore.setupVisibilityListener()
})
