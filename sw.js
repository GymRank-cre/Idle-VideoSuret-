// Service worker minimal : cache applicatif, réseau d'abord pour rester à jour.

const CACHE = 'surete-tycoon-v3';
const ASSETS = [
  './',
  './index.html',
  './manifest.json',
  './styles/main.css',
  './src/main.js',
  './src/ui.js',
  './src/tower-view.js',
  './src/tower3d.js',
  './src/tower.js',
  './src/game.js',
  './src/state.js',
  './src/economy.js',
  './src/data.js',
  './src/format.js',
  './assets/icon.svg',
  './assets/icon-maskable.svg',
  './vendor/three.min.js',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE).then((c) => c.addAll(ASSETS)).then(() => self.skipWaiting())
  );
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
  if (req.method !== 'GET' || !req.url.startsWith(self.location.origin)) return;

  event.respondWith(
    fetch(req)
      .then((res) => {
        const copy = res.clone();
        caches.open(CACHE).then((c) => c.put(req, copy)).catch(() => {});
        return res;
      })
      .catch(() => caches.match(req).then((hit) => hit || caches.match('./index.html')))
  );
});
