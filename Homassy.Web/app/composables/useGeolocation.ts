/**
 * Browser Geolocation wrapper.
 *
 * Mirrors the support-check + permission-query + request-on-user-action pattern used by
 * `usePushNotifications` and `useCameraAvailability`. All browser access is guarded with
 * `import.meta.client` because SSR is in play.
 *
 * Note: web geolocation only works while the page is in the foreground — there is no
 * reliable background/geofencing capability in a PWA.
 */
export interface GeoPosition {
  lat: number
  lon: number
  accuracy: number
}

export const useGeolocation = () => {
  const isSupported = computed(() => {
    if (!import.meta.client) return false
    return 'geolocation' in navigator
  })

  /**
   * Reads the current geolocation permission state without prompting the user.
   * Returns 'unknown' where the Permissions API is unavailable.
   */
  const getPermissionStatus = async (): Promise<'granted' | 'denied' | 'prompt' | 'unknown'> => {
    if (!import.meta.client || !navigator.permissions) return 'unknown'
    try {
      const status = await navigator.permissions.query({ name: 'geolocation' as PermissionName })
      return status.state
    } catch {
      return 'unknown'
    }
  }

  /**
   * Requests the current position. On the first call this triggers the browser's permission
   * prompt, so it must be invoked from a user gesture. Rejects if unsupported or denied.
   */
  const getCurrentPosition = (options?: PositionOptions): Promise<GeoPosition> => {
    return new Promise((resolve, reject) => {
      if (!isSupported.value) {
        reject(new Error('Geolocation is not supported'))
        return
      }
      navigator.geolocation.getCurrentPosition(
        position => resolve({
          lat: position.coords.latitude,
          lon: position.coords.longitude,
          accuracy: position.coords.accuracy
        }),
        error => reject(error),
        { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000, ...options }
      )
    })
  }

  let watchId: number | null = null

  /**
   * Starts watching the position while the page is in the foreground. `onUpdate` fires on
   * each fix; `onError` (optional) fires on failures. Returns nothing — use `stopWatch()`.
   */
  const startWatch = (
    onUpdate: (position: GeoPosition) => void,
    onError?: (error: GeolocationPositionError) => void,
    options?: PositionOptions
  ): void => {
    if (!isSupported.value) return
    stopWatch()
    watchId = navigator.geolocation.watchPosition(
      position => onUpdate({
        lat: position.coords.latitude,
        lon: position.coords.longitude,
        accuracy: position.coords.accuracy
      }),
      error => onError?.(error),
      { enableHighAccuracy: true, timeout: 20000, maximumAge: 30000, ...options }
    )
  }

  const stopWatch = (): void => {
    if (watchId !== null && import.meta.client) {
      navigator.geolocation.clearWatch(watchId)
      watchId = null
    }
  }

  return { isSupported, getPermissionStatus, getCurrentPosition, startWatch, stopWatch }
}
