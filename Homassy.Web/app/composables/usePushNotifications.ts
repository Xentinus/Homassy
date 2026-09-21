/**
 * Where the service worker looks for the API base URL and the VAPID key when it has to
 * re-register a subscription with no page open (`pushsubscriptionchange`). Kept in step with
 * `public/sw-push.js`.
 */
const PUSH_CONFIG_CACHE = 'homassy-push-config'
const PUSH_CONFIG_KEY = '/__homassy_push_config'

/**
 * The app-start reconciliation runs once per page load, not once per call site. `syncSubscription`
 * is safe to call from anywhere, and the layout calling it on mount must not turn into one request
 * per navigation.
 */
let syncPromise: Promise<void> | null = null

export const usePushNotifications = () => {
  const client = useApiClient()
  // Read here, in setup, rather than inside the async helpers below: those run from a mounted hook
  // and from click handlers, where the Nuxt instance is no longer the ambient one.
  const runtimeConfig = useRuntimeConfig()

  const isSupported = computed(() => {
    if (!import.meta.client) return false
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
  })

  const permissionStatus = computed(() => {
    if (!import.meta.client) return 'default'
    return Notification.permission
  })

  /**
   * Decodes the VAPID public key (base64url) into the bytes `pushManager.subscribe` wants.
   *
   * Returns `Uint8Array<ArrayBuffer>`, not a bare `Uint8Array`: since TypeScript 5.7 the class is
   * generic over its buffer, and the default `ArrayBufferLike` includes `SharedArrayBuffer`, which
   * `BufferSource` does not accept. Constructing over an explicit `ArrayBuffer` is what pins it.
   */
  const urlBase64ToUint8Array = (base64String: string): Uint8Array<ArrayBuffer> => {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
    const rawData = window.atob(base64)
    const outputArray = new Uint8Array(new ArrayBuffer(rawData.length))
    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i)
    }
    return outputArray
  }

  /** The VAPID public key, in whichever casing the API answered in. */
  const fetchVapidKey = async (): Promise<string | undefined> => {
    const response = await client.get<{ publicKey: string }>('/api/v1/User/push/vapid-key', { showErrorToast: false })
    // The endpoint has answered in both spellings; accept either rather than depending on which
    // serializer casing is configured server-side.
    return response?.data?.publicKey ?? (response?.data as { PublicKey?: string } | undefined)?.PublicKey
  }

  /**
   * Leaves the API base URL and the VAPID key where the service worker can find them.
   *
   * The worker has no access to the Nuxt runtime config and no client to ask when
   * `pushsubscriptionchange` fires — by definition nothing is open — so the page has to have put
   * them somewhere durable beforehand. Rewritten only when the record is missing or the base URL
   * changed, so the usual app start costs one cache read and nothing else.
   */
  const publishPushConfig = async (vapidKey?: string): Promise<void> => {
    if (!import.meta.client || typeof caches === 'undefined') return

    const apiBase = String(runtimeConfig.public.apiBase || window.location.origin)

    try {
      const cache = await caches.open(PUSH_CONFIG_CACHE)
      const existing = await cache.match(PUSH_CONFIG_KEY)

      if (existing) {
        const stored = await existing.clone().json() as { apiBase?: string, vapidKey?: string }
        if (stored.apiBase === apiBase && stored.vapidKey) return
      }

      const key = vapidKey ?? await fetchVapidKey()
      if (!key) return

      await cache.put(
        PUSH_CONFIG_KEY,
        new Response(JSON.stringify({ apiBase, vapidKey: key }), {
          headers: { 'Content-Type': 'application/json' }
        })
      )
    } catch {
      // Cache Storage refused (private window, cleared site data). The worker falls back to the
      // old subscription's own key and this origin, which covers the common case anyway.
    }
  }

  /**
   * Reconcile what this browser holds with what the server has recorded, at app start.
   *
   * The two can drift apart without anything looking wrong: the browser rotates a subscription
   * while the app is closed, a server-side cleanup removes the row, a database is restored from a
   * backup. Nothing detected that, because `isSubscribed()` only ever asked the browser — so the
   * settings screen kept saying "subscribed" while the server had nobody to send to, and the user's
   * only clue was that notifications stopped arriving.
   *
   * Re-posting the current subscription is cheap and idempotent (the endpoint is an upsert, and it
   * un-deletes a row that was removed), so this runs unconditionally rather than trying to guess
   * whether the server is out of step.
   */
  const syncSubscription = async (): Promise<void> => {
    if (!import.meta.client || !isSupported.value) return
    if (syncPromise) return syncPromise

    syncPromise = (async () => {
      try {
        if (Notification.permission !== 'granted') return

        const registration = await navigator.serviceWorker.ready
        const subscription = await registration.pushManager.getSubscription()
        if (!subscription) return

        const json = subscription.toJSON()
        const p256dh = json.keys?.p256dh
        const auth = json.keys?.auth
        if (!json.endpoint || !p256dh || !auth) return

        await client.post('/api/v1/User/push/subscribe', {
          endpoint: json.endpoint,
          p256dh,
          auth,
          userAgent: navigator.userAgent
        }, { showErrorToast: false })

        await publishPushConfig()
      } catch {
        // Offline, or the session has expired. Either way the next app start tries again, and
        // nothing the user can act on has happened.
      }
    })()

    return syncPromise
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
      const vapidKey = await fetchVapidKey()
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

      // Hand the worker what it needs to re-register this subscription on its own later.
      await publishPushConfig(vapidKey)

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

    // Take the worker's re-registration kit away with it. Opting out has to be the end of it —
    // leaving the key behind is how a background event turns a deliberate "no" back into a yes.
    if (typeof caches !== 'undefined') {
      try {
        const cache = await caches.open(PUSH_CONFIG_CACHE)
        await cache.delete(PUSH_CONFIG_KEY)
      } catch {
        // Nothing to clean up, or storage refused. The subscription itself is already gone, which
        // is what actually stops the pushes.
      }
    }

    return true
  }

  const isSubscribed = async (): Promise<boolean> => {
    if (!isSupported.value) return false

    try {
      const registration = await navigator.serviceWorker.ready
      const subscription = await registration.pushManager.getSubscription()
      return subscription !== null
    } catch {
      return false
    }
  }

  return {
    isSupported,
    permissionStatus,
    subscribe,
    unsubscribe,
    isSubscribed,
    syncSubscription
  }
}
