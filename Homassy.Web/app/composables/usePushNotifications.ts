export const usePushNotifications = () => {
  const client = useApiClient()

  const isSupported = computed(() => {
    if (!import.meta.client) return false
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
  })

  const permissionStatus = computed(() => {
    if (!import.meta.client) return 'default'
    return Notification.permission
  })

  const urlBase64ToUint8Array = (base64String: string): Uint8Array => {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
    const rawData = window.atob(base64)
    const outputArray = new Uint8Array(rawData.length)
    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i)
    }
    return outputArray
  }

  const subscribe = async (): Promise<boolean> => {
    try {
      if (!isSupported.value) {
        console.warn('[Push] subscribe: not supported')
        return false
      }

      const permission = await Notification.requestPermission()
      console.log('[Push] subscribe: permission =', permission)
      if (permission !== 'granted') return false

      // Get VAPID public key from backend
      const vapidResponse = await client.get<{ publicKey: string }>('/api/v1/User/push/vapid-key', { showErrorToast: false })
      console.log('[Push] subscribe: vapidResponse =', vapidResponse)
      // Support both camelCase and PascalCase API responses
      const vapidKey = vapidResponse?.data?.publicKey ?? (vapidResponse?.data as any)?.PublicKey
      console.log('[Push] subscribe: vapidKey =', vapidKey ? `${vapidKey.substring(0, 10)}...` : 'MISSING')
      if (!vapidKey) {
        console.error('[Push] subscribe: VAPID key missing from response')
        return false
      }

      // Get service worker registration with timeout
      console.log('[Push] subscribe: waiting for service worker...')
      const timeoutPromise = new Promise<ServiceWorkerRegistration>((_, reject) => 
        setTimeout(() => reject(new Error('Service worker timeout after 10s')), 10000)
      )
      const registration = await Promise.race([navigator.serviceWorker.ready, timeoutPromise])
      console.log('[Push] subscribe: SW ready, state =', registration.active?.state)

      // Unsubscribe any existing subscription first (handles VAPID key rotation)
      const existingSubscription = await registration.pushManager.getSubscription()
      console.log('[Push] subscribe: existing subscription =', existingSubscription?.endpoint ?? 'none')
      if (existingSubscription) {
        await existingSubscription.unsubscribe()
        console.log('[Push] subscribe: unsubscribed existing')
      }

      // Subscribe to push manager
      console.log('[Push] subscribe: calling pushManager.subscribe...')
      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(vapidKey)
      })
      console.log('[Push] subscribe: subscribed, endpoint =', subscription.endpoint)

      const subscriptionJson = subscription.toJSON()
      const p256dh = subscriptionJson.keys?.p256dh
      const auth = subscriptionJson.keys?.auth
      console.log('[Push] subscribe: keys present =', { hasP256dh: !!p256dh, hasAuth: !!auth, hasEndpoint: !!subscriptionJson.endpoint })

      if (!subscriptionJson.endpoint || !p256dh || !auth) {
        console.error('[Push] subscribe: subscription keys missing', subscriptionJson)
        return false
      }

      // Send subscription to backend
      console.log('[Push] subscribe: posting subscription to backend...')
      const result = await client.post('/api/v1/User/push/subscribe', {
        endpoint: subscriptionJson.endpoint,
        p256dh,
        auth,
        userAgent: navigator.userAgent
      }, { showErrorToast: false })
      console.log('[Push] subscribe: backend result =', result)

      return result?.success ?? false
    } catch (error) {
      console.error('[Push] subscribe: failed with error:', error)
      return false
    }
  }

  const unsubscribe = async (): Promise<boolean> => {
    if (!isSupported.value) return false

    const registration = await navigator.serviceWorker.ready
    const subscription = await registration.pushManager.getSubscription()

    if (subscription) {
      const endpoint = subscription.endpoint
      await subscription.unsubscribe()
      await client.post('/api/v1/User/push/unsubscribe', { endpoint }, { showErrorToast: false })
    }

    return true
  }

  const isSubscribed = async (): Promise<boolean> => {
    if (!isSupported.value) return false

    try {
      const registration = await navigator.serviceWorker.ready
      const subscription = await registration.pushManager.getSubscription()
      return subscription !== null
    } catch (error) {
      return false
    }
  }

  return {
    isSupported,
    permissionStatus,
    subscribe,
    unsubscribe,
    isSubscribed
  }
}
