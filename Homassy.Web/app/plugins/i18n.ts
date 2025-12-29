/**
 * i18n Integration Plugin
 * Synchronizes user language preference with i18n locale
 * Handles browser detection for anonymous users and user preference for authenticated users
 */
export default defineNuxtPlugin({
  name: 'i18n-sync',
  dependsOn: ['i18n:plugin'],

  async setup() {
    const { $i18n } = useNuxtApp()
    const authStore = useAuthStore()
    const localeCookie = useCookie('homassy_locale')

    // Helper to set locale and persist to cookie
    const setLocale = (locale: string) => {
      if ($i18n.locale.value !== locale) {
        $i18n.setLocale(locale)
        localeCookie.value = locale
      }
    }

    // Initial locale setup
    if (import.meta.server) {
      // SSR: Set locale from user preference or cookie
      if (authStore.user?.language) {
        setLocale(authStore.user.language)
      } else if (localeCookie.value) {
        setLocale(localeCookie.value)
      }
      // Otherwise use browser detection (handled by i18n module)
    }

    if (import.meta.client) {
      // Client: Watch for user language changes
      watch(
        () => authStore.user?.language,
        (newLanguage) => {
          if (newLanguage) {
            setLocale(newLanguage)
          }
        },
        { immediate: true }
      )

      // Watch for logout (user becomes null)
      watch(
        () => authStore.user,
        (newUser) => {
          if (!newUser) {
            // User logged out - revert to cookie or browser detection
            if (localeCookie.value) {
              setLocale(localeCookie.value)
            }
            // Browser detection will kick in on next navigation if needed
          }
        }
      )
    }

    // Provide helper function for manual locale switching
    return {
      provide: {
        switchLocale: setLocale
      }
    }
  }
})
