/**
 * Version Check Plugin
 * Detects app version changes and prompts user to reload
 */
export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig()
  const toast = useToast()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const i18n = nuxtApp.$i18n as any
  const currentVersion = config.public.appVersion
  const storedVersion = localStorage.getItem('homassy_app_version')

  // Delay version check until after hydration to avoid mismatch
  nuxtApp.hooks.hook('app:mounted', () => {
    // Add extra delay to ensure hydration is fully complete
    setTimeout(() => {
      // Check if version has changed
      if (storedVersion && storedVersion !== currentVersion) {
        console.debug('[Version Check] New version detected:', { stored: storedVersion, current: currentVersion })
        
        const toastId = toast.add({
          id: 'app-version-update',
          title: i18n.t('toast.appVersionUpdate'),
          description: i18n.t('toast.appVersionUpdateDescription'),
          color: 'primary',
          actions: [{
            label: i18n.t('toast.refresh'),
            onClick: () => {
              console.debug('[Version Check] User triggered reload')
              localStorage.setItem('homassy_app_version', currentVersion)
              location.reload()
            }
          }]
        })

        // Auto-remove toast after 15 seconds
        setTimeout(() => {
          toast.remove(toastId.id)
        }, 15000)
      } else if (!storedVersion) {
        // First time loading - store the current version
        console.debug('[Version Check] First load, storing version:', currentVersion)
        localStorage.setItem('homassy_app_version', currentVersion)
      } else {
        console.debug('[Version Check] Version up to date:', currentVersion)
      }
    }, 100) // Wait 100ms after mount to ensure hydration is complete
  })
})
