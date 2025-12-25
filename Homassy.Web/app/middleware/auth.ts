/**
 * Authentication middleware
 * Protects routes that require authentication
 */
export default defineNuxtRouteMiddleware((to) => {
  const authStore = useAuthStore()

  // Check if user is authenticated
  if (!authStore.isAuthenticated) {
    // Redirect to login page
    return navigateTo('/login', {
      query: {
        redirect: to.fullPath
      }
    })
  }
})
