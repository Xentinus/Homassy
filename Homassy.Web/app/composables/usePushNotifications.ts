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
      if (!isSupported.value) return false

      const permission = await Notification.requestPermission()
      if (permission !== 'granted') return false

      // Get VAPID public key from backend
      const vapidResponse = await client.get<{ publicKey: string }>('/api/v1/User/push/vapid-key', { showErrorToast: false })
      const vapidKey = vapidResponse?.data?.publicKey
      if (!vapidKey) return false

      // Get service worker registration with timeout
      const timeoutPromise = new Promise<ServiceWorkerRegistration>((_, reject) => 
        setTimeout(() => reject(new Error('Service worker timeout')), 5000)
      )
      const registration = await Promise.race([navigator.serviceWorker.ready, timeoutPromise])

      // Subscribe to push manager
      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(vapidKey)
      })

      const subscriptionJson = subscription.toJSON()
      const p256dh = subscriptionJson.keys?.p256dh
      const auth = subscriptionJson.keys?.auth

      if (!subscriptionJson.endpoint || !p256dh || !auth) return false

      // Send subscription to backend
      const result = await client.post('/api/v1/User/push/subscribe', {
        endpoint: subscriptionJson.endpoint,
        p256dh,
        auth,
        userAgent: navigator.userAgent
      }, { showErrorToast: false })

      return result?.success ?? false
    } catch (error) {
      console.error('Failed to subscribe to push notifications:', error)
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
