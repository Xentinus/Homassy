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

    // Map language codes to locale codes
    const localeMap: Record<string, string> = {
      'English': 'en',
      'Hungarian': 'hu',
      'German': 'de'
    }

    // Helper to convert language string to locale code
    const getLocaleCode = (language: string): string => {
      return localeMap[language] || language.toLowerCase().substring(0, 2) || 'en'
    }

    // Helper to set locale and persist to cookie
    const setLocale = (localeOrLanguage: string) => {
      // Convert language name to locale code if needed
      const locale = localeMap[localeOrLanguage] || localeOrLanguage
      
      if ($i18n.locale.value !== locale && ['en', 'hu', 'de'].includes(locale)) {
        $i18n.setLocale(locale)
        localeCookie.value = locale
      }
    }

    // Initial locale setup
    if (import.meta.server) {
      // SSR: Set locale from cookie first, then user preference
      if (localeCookie.value && ['en', 'hu', 'de'].includes(localeCookie.value)) {
        setLocale(localeCookie.value)
      } else if (authStore.user?.language) {
        const localeCode = getLocaleCode(authStore.user.language)
        setLocale(localeCode)
      }
      // Otherwise use browser detection (handled by i18n module)
    }

    if (import.meta.client) {
      // Client: Set initial locale from cookie or user preference
      if (authStore.user?.language) {
        const localeCode = getLocaleCode(authStore.user.language)
        setLocale(localeCode)
      } else if (localeCookie.value) {
        setLocale(localeCookie.value)
      }

      // Watch for user language changes
      watch(
        () => authStore.user?.language,
        (newLanguage) => {
          if (newLanguage) {
            const localeCode = getLocaleCode(newLanguage)
            setLocale(localeCode)
          }
        }
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
