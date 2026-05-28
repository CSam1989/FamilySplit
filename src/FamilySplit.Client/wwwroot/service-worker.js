// Development service worker — intentionally minimal.
// No caching: every request goes straight to the network so hot-reload works
// without stale assets.
//
// Push notifications work in dev so you can test VAPID without publishing.
// SignalR handles in-app toasts; this SW only shows the OS notification
// when no app window is currently visible.

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => clients.claim());

// ── Push: show OS notification only when app is not visible ──────────────────
// When the app is open, SignalR delivers the message and shows a snackbar.
// Showing an OS popup on top would be redundant, so we suppress it here.

self.addEventListener('push', event => {
    event.waitUntil(handlePush(event));
});

async function handlePush(event) {
    const data    = event.data?.json() ?? {};
    const title   = data.title   ?? 'FamilySplit';
    const options = {
        body:    data.body   ?? data.message ?? '',
        icon:    data.icon   ?? '/icons/icon-192.png',
        badge:   data.badge  ?? '/icons/icon-192.png',
        tag:     data.tag    ?? 'familysplit-settlement',
        data:    { url: data.url ?? '/' },
        vibrate: [100, 50, 100],
        requireInteraction: false,
    };

    // Suppress OS notification if any app window is currently visible —
    // SignalR will have already shown a snackbar.
    const windowClients = await clients.matchAll({ type: 'window', includeUncontrolled: true });
    const appIsVisible  = windowClients.some(c => c.visibilityState === 'visible');
    if (appIsVisible) return;

    await self.registration.showNotification(title, options);
}

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = event.notification.data?.url ?? '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
            const existing = list.find(c => c.url === url && 'focus' in c);
            if (existing) return existing.focus();
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});
