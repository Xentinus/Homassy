/**
 * Auth initialization plugin
 * Checks for existing Kratos session on client startup.
 * Must run client-side only: Kratos session cookies are httpOnly and cannot
 * be read server-side; SSR init would always fail inside Docker (localhost:4433
 * is not reachable from the container) and would poison the store with
 * initialized=true / session=null, causing the middleware to redirect to login.
 *
 * Also owns the boot splash dismissal (standalone PWA only — see
 * useSplashScreen / SplashScreen.vue): the splash stays up through the async
 * session check and is dismissed once we know where the app is going.
 */
import { useAuthStore } from '~/stores/auth'

export default defineNuxtPlugin(async () => {
  // Skip on SSR — middleware already returns early on server
  if (import.meta.server) {
    return
  }

  const authStore = useAuthStore()
  const router = useRouter()
  const { markReady } = useSplashScreen()

  // Load authentication state by checking the Kratos session
  await authStore.loadFromCookies()

  const isBootRoute = (path: string) => path === '/' || path === '/calendar'

  if (!authStore.isAuthenticated) {
    // Anonymous: reveal the landing / login page immediately.
    markReady()
  }
  else if (!isBootRoute(router.currentRoute.value.path)) {
    // Authenticated deep-link / refresh onto a non-calendar page: that page
    // owns its own skeletons, so dismiss as soon as it takes over.
    markReady()
  }
  else {
    // Normal relaunch path (`/` redirects to `/calendar`): the calendar hides
    // the splash once its first data load finishes. Guard against ever getting
    // stuck if the redirect lands somewhere unexpected.
    const stop = router.afterEach((to) => {
      if (!isBootRoute(to.path)) {
        markReady()
        stop()
      }
    })
  }

  // Safety net: never let the splash trap the user, whatever happens above.
  setTimeout(markReady, 6000)

  // Setup visibility listener to handle long-running background tabs
  authStore.setupVisibilityListener()
})
