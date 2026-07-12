// Minimal offline app-shell service worker.
// Caches static assets and serves an offline fallback page for navigations when the
// server is unreachable. Data-entry features require the server and are NOT offline-capable;
// this only keeps the shell usable and degrades gracefully (see offline.html).
const CACHE = 'homestock-shell-v1';
const SHELL = [
    '/',
    '/offline.html',
    '/app.css',
    '/js/homestock.js',
    '/manifest.webmanifest',
    '/icons/icon-192.png',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap-icons/font/bootstrap-icons.min.css'
];

self.addEventListener('install', (event) => {
    event.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    if (req.method !== 'GET') return;

    // Navigations: try network first, fall back to the offline page.
    if (req.mode === 'navigate') {
        event.respondWith(fetch(req).catch(() => caches.match('/offline.html')));
        return;
    }

    // Static assets: cache-first with background refresh.
    event.respondWith(
        caches.match(req).then((cached) => cached || fetch(req).then((res) => {
            const copy = res.clone();
            caches.open(CACHE).then((c) => c.put(req, copy)).catch(() => { });
            return res;
        }).catch(() => cached))
    );
});
