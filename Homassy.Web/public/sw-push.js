/**
 * Where the page left the two things this worker needs to re-register a subscription on its own:
 * the API's base URL (a runtime config the worker cannot read) and the VAPID key.
 *
 * Cache Storage rather than a `postMessage` from a client, because the event this is for -
 * `pushsubscriptionchange` - fires precisely when nothing is open: the browser rotates the
 * subscription in the background and there is no page to ask. `usePushNotifications` writes the
 * record on every app start.
 */
const PUSH_CONFIG_CACHE = 'homassy-push-config';
const PUSH_CONFIG_KEY = '/__homassy_push_config';

async function readPushConfig() {
  try {
    const cache = await caches.open(PUSH_CONFIG_CACHE);
    const response = await cache.match(PUSH_CONFIG_KEY);
    if (!response) return null;
    return await response.json();
  } catch (e) {
    return null;
  }
}

self.addEventListener('push', (event) => {
  if (!event.data) return;

  let data;
  try {
    data = event.data.json();
  } catch (e) {
    data = {
      title: 'Homassy',
      body: event.data.text(),
      icon: '/apple-touch-icon-180x180.png',
      url: '/'
    };
  }

  const options = {
    body: data.body,
    icon: data.icon || '/apple-touch-icon-180x180.png',
    badge: data.badge || '/favicon-32x32.png',
    tag: 'homassy-notification',
    renotify: true,
    vibrate: [200, 100, 200],
    requireInteraction: true,
    actions: [
      {
        action: 'open',
        title: data.actionTitle || 'Open Homassy',
        icon: '/favicon-32x32.png'
      }
    ],
    data: {
      url: data.url || '/',
      action: 'open'
    }
  };

  event.waitUntil(
    Promise.all([
      self.registration.showNotification(data.title || 'Homassy', options),
      applyAppBadge(data.badgeCount),
      announceToClients(data)
    ])
  );
});

/**
 * Tell any open tab that a notification just arrived (#116), so the notification centre can
 * prepend it and the header bell can update its unread badge without waiting for the user to
 * reopen the app.
 *
 * The message carries no content: the row is already in the database, and the page fetches it.
 * Trusting a payload assembled here would mean two descriptions of the same notification, and
 * the one the page renders should be the one the server stored.
 */
function announceToClients(data) {
  return self.clients
    .matchAll({ type: 'window', includeUncontrolled: true })
    .then((clientList) => {
      for (const client of clientList) {
        client.postMessage({ type: 'homassy:notification', url: data.url || '/' });
      }
    })
    .catch(() => {});
}

/**
 * Mirror the sender's count onto the installed app's icon (#130), so the badge is
 * right without the app ever being opened. Only senders that actually know a count
 * put `badgeCount` in the payload; a payload without one leaves whatever the app
 * last set in place rather than guessing (an increment-per-push would drift the
 * moment two devices received the same notification).
 *
 * `navigator.setAppBadge` is feature-detected, and a rejection is swallowed: it
 * means the platform declined (not installed, no permission), which is not
 * something a service worker can act on.
 */
function applyAppBadge(badgeCount) {
  if (typeof badgeCount !== 'number' || !Number.isFinite(badgeCount)) return Promise.resolve();
  if (!('setAppBadge' in navigator)) return Promise.resolve();

  const promise = badgeCount > 0
    ? navigator.setAppBadge(Math.floor(badgeCount))
    : navigator.clearAppBadge();

  return Promise.resolve(promise).catch(() => {});
}

/**
 * Re-register when the browser rotates the subscription out from under us.
 *
 * A push subscription is not permanent: a browser may replace it after a VAPID key change, a long
 * idle period, or its own storage housekeeping, and it fires this event to say so. Without a
 * handler the old endpoint starts answering 410, the server deletes the row, and the app goes
 * silent forever — while the page still believes it is subscribed, because `pushManager` hands it
 * the *new* subscription the server has never heard of. That is the state this app shipped in.
 *
 * The key comes from the old subscription when the browser provides it, which is both exact and
 * free; the stored config is the fallback for browsers that fire the event with no
 * `oldSubscription`. Subscribe first, then retire the old endpoint: a failure in the other order
 * would leave the user with no subscription at all.
 */
self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil(resubscribe(event));
});

async function resubscribe(event) {
  try {
    const config = await readPushConfig();
    const apiBase = (config && config.apiBase) || self.location.origin;

    const applicationServerKey =
      (event.oldSubscription && event.oldSubscription.options && event.oldSubscription.options.applicationServerKey)
      || (config && config.vapidKey ? urlBase64ToUint8Array(config.vapidKey) : null);

    if (!applicationServerKey) return;

    // The browser may have already made the replacement; `event.newSubscription` is it. Only
    // subscribe when it did not.
    const subscription = event.newSubscription
      || await self.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey });

    const json = subscription.toJSON();
    if (!json.endpoint || !json.keys || !json.keys.p256dh || !json.keys.auth) return;

    await fetch(`${apiBase}/api/v1/User/push/subscribe`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        endpoint: json.endpoint,
        p256dh: json.keys.p256dh,
        auth: json.keys.auth,
        userAgent: self.navigator ? self.navigator.userAgent : undefined
      })
    });

    const oldEndpoint = event.oldSubscription && event.oldSubscription.endpoint;
    if (oldEndpoint && oldEndpoint !== json.endpoint) {
      await fetch(`${apiBase}/api/v1/User/push/unsubscribe`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ endpoint: oldEndpoint })
      }).catch(() => {});
    }
  } catch (e) {
    // Nothing here can prompt the user or retry usefully. The app-start reconciliation in
    // `usePushNotifications` is the second line of defence: it pushes whatever subscription the
    // browser holds to the server the next time the app is opened.
  }
}

/** base64url VAPID key → the bytes `pushManager.subscribe` wants. Mirrors the composable's copy. */
function urlBase64ToUint8Array(base64String) {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = self.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const url = event.notification.data?.url || '/';

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windowClients) => {
      for (const client of windowClients) {
        if (client.url.includes(self.location.origin) && 'focus' in client) {
          client.navigate(url);
          return client.focus();
        }
      }
      return clients.openWindow(url);
    })
  );
});
