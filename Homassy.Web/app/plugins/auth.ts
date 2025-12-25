/**
 * Auth initialization plugin
 * Checks for existing cookies on app startup and auto-login returning users
 */
export default defineNuxtPlugin(() => {
  const authStore = useAuthStore()

  // Load authentication state from cookies on client-side
  if (import.meta.client) {
    authStore.loadFromCookies()
  }
})
