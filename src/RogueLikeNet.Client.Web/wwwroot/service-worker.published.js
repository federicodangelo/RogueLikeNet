// Published service worker with assets manifest for offline caching
self.importScripts('./service-worker-assets.js');

const CACHE_NAME = 'roguelikenet-v1';

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => {
            const assets = self.assetsManifest.assets
                .filter(a => a.hash)
                .map(a => new Request(a.url, { integrity: a.hash, cache: 'no-cache' }));
            return cache.addAll(assets);
        })
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
});

self.addEventListener('fetch', event => {
    event.respondWith(
        caches.match(event.request).then(cached => {
            if (cached) return cached;
            return fetch(event.request).then(response => {
                if (response.ok && event.request.method === 'GET') {
                    const clone = response.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
                }
                return response;
            }).catch(() => cached || new Response('Offline', { status: 503 }));
        })
    );
});
