/* Crypto Payment - Service Worker - PWA & Push */
const CACHE_NAME = 'crypto-payment-v2';
const OFFLINE_URL = '/';

self.addEventListener('install', (e) => {
  e.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll([OFFLINE_URL]);
    }).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys().then((names) => {
      /* Tüm cache'leri temizle */
      return Promise.all(names.map((n) => caches.delete(n)));
    }).then(() => self.clients.claim())
  );
});

self.addEventListener('push', (e) => {
  let data = { title: 'Crypto Payment', body: 'Yeni bildirim', url: '/', tag: 'default' };
  if (e.data) {
    try {
      data = { ...data, ...e.data.json() };
    } catch (_) {}
  }
  const options = {
    body: data.body,
    icon: '/admin/velzon-dist/assets/images/favicon.ico',
    badge: '/admin/velzon-dist/assets/images/favicon.ico',
    tag: data.tag || 'crypto-payment',
    data: { url: data.url || '/', type: data.type },
    requireInteraction: data.requireInteraction || false,
    vibrate: [200, 100, 200],
    actions: data.url ? [{ action: 'open', title: 'Aç' }] : []
  };
  e.waitUntil(self.registration.showNotification(data.title, options));
});

self.addEventListener('notificationclick', (e) => {
  e.notification.close();
  const url = e.notification.data?.url || '/';
  e.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then((list) => {
      if (list.length) {
        const w = list.find((c) => c.url.startsWith(self.registration.scope));
        if (w) {
          w.focus();
          w.navigate?.(url);
          return;
        }
        list[0].focus();
        list[0].navigate?.(url);
      } else if (clients.openWindow) {
        clients.openWindow(url);
      }
    })
  );
});

self.addEventListener('fetch', (e) => {
  if (e.request.mode !== 'navigate') return;
  e.respondWith(
    fetch(e.request).catch(() => caches.match(OFFLINE_URL) || caches.match('/'))
  );
});
