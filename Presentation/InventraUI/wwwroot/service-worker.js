// Inventra service worker: makes the app installable and shows a friendly page when offline.
// Strategy is network-first, so users always get fresh pages/scripts when online. API calls are never cached.
const CACHE = 'inventra-shell-v1';
const OFFLINE_URL = '/offline.html';

self.addEventListener('install', (event) => {
    event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll([OFFLINE_URL, '/icons/icon-192.png'])));
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    const url = new URL(req.url);

    if (req.method !== 'GET' || url.origin !== self.location.origin) return;
    if (url.pathname.startsWith('/api/') || url.pathname === '/health') return; // never cache API/health

    event.respondWith(
        fetch(req)
            .then((res) => {
                // keep a copy of static assets for offline use
                if (res.ok && /\.(js|css|png|jpg|jpeg|svg|ico|woff2?|webmanifest)$/i.test(url.pathname)) {
                    const copy = res.clone();
                    caches.open(CACHE).then((cache) => cache.put(req, copy));
                }
                return res;
            })
            .catch(async () => {
                const cached = await caches.match(req);
                if (cached) return cached;
                if (req.mode === 'navigate') return caches.match(OFFLINE_URL);
                return Response.error();
            })
    );
});
