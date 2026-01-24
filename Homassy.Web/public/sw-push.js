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
    self.registration.showNotification(data.title || 'Homassy', options)
  );
});

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
