self.addEventListener("install", event => {
    self.skipWaiting();
});

self.addEventListener("activate", event => {
    event.waitUntil((async () => {
        if (self.caches?.keys) {
            for (const key of await caches.keys()) {
                await caches.delete(key);
            }
        }

        await self.clients.claim();
    })());
});

self.addEventListener("fetch", event => {
    event.respondWith(fetch(event.request, { cache: "no-store" }));
});
