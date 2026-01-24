/**
 * Auth initialization plugin
 * Checks for existing cookies on app startup and auto-login returning users
 */
export default defineNuxtPlugin(async () => {
  const authStore = useAuthStore()

  // Load authentication state from cookies on both server and client
  await authStore.loadFromCookies()

  // Setup visibility listener on client to handle long-running background tabs
  authStore.setupVisibilityListener()
})
