const CACHE = 'quiniela133-v2';
const OFFLINE_URL = '/offline.html';

self.addEventListener('install', e =>
    e.waitUntil(
        caches.open(CACHE)
            .then(c => c.add(OFFLINE_URL))
            .then(() => self.skipWaiting())
    )
);

self.addEventListener('activate', e =>
    e.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    )
);

self.addEventListener('fetch', e => {
    if (e.request.mode === 'navigate') {
        e.respondWith(fetch(e.request).catch(() => caches.match(OFFLINE_URL)));
    }
});

self.addEventListener('push', event => {
    const data = event.data?.json() ?? {};
    event.waitUntil(
        self.registration.showNotification(data.title ?? 'Quiniela', {
            body: data.body,
            icon: '/icons/icon.svg',
            data: { url: data.url ?? '/' }
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    event.waitUntil(clients.openWindow(event.notification.data.url));
});
