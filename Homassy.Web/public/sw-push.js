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
      applyAppBadge(data.badgeCount)
    ])
  );
});

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
