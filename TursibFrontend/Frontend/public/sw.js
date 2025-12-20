// Service Worker pentru Tursib PWA
const CACHE_VERSION = 'v1.0.0'
const CACHE_NAME = `tursib-cache-${CACHE_VERSION}`
const DATA_CACHE_NAME = `tursib-data-${CACHE_VERSION}`

const isDevelopment = location.hostname === 'localhost' || location.hostname === '127.0.0.1'

// Skip service worker caching in development but keep it active for notifications
if (isDevelopment) {
  console.log('[SW] Development mode - caching bypassed, notifications enabled')
  
  self.addEventListener('install', () => {
    console.log('[SW] Service Worker installed (dev mode)')
    self.skipWaiting()
  })
  
  self.addEventListener('activate', () => {
    console.log('[SW] Service Worker activated (dev mode)')
    self.clients.claim()
  })
  
  self.addEventListener('fetch', (event) => {
    // Don't cache in development
    event.respondWith(fetch(event.request))
  })
  
  // But still handle notifications
  self.addEventListener('push', (event) => {
    console.log('[SW] Push notification received (dev)')
    
    let data = {
      title: 'Tursib',
      body: 'Autobuzul se apropie!',
      icon: '/front-of-bus.png',
      badge: '/bus-station.png'
    }
    
    if (event.data) {
      try {
        data = event.data.json()
      } catch (e) {
        console.error('[SW] Error parsing push data:', e)
      }
    }
    
    const options = {
      body: data.body,
      icon: data.icon,
      badge: data.badge,
      vibrate: [200, 100, 200],
      requireInteraction: false,
      silent: false
    }
    
    event.waitUntil(
      self.registration.showNotification(data.title, options)
        .catch(err => console.error('[SW] Show notification error:', err))
    )
  })
  
  self.addEventListener('notificationclick', (event) => {
    console.log('[SW] Notification clicked (dev)')
    event.notification.close()
    event.waitUntil(
      clients.openWindow('/').catch(err => console.error('[SW] Open window error:', err))
    )
  })
  
} else {
// Production service worker below

// Resurse statice de cached
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/icon-192x192.png',
  '/icon-512x512.png'
]

// Install event - cache static assets
self.addEventListener('install', (event) => {
  console.log('[SW] Installing service worker...')
  
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => {
        console.log('[SW] Caching static assets')
        return cache.addAll(STATIC_ASSETS)
      })
      .then(() => {
        console.log('[SW] Service worker installed successfully')
        return self.skipWaiting()
      })
      .catch((error) => {
        console.error('[SW] Install failed:', error)
      })
  )
})

// Activate event - cleanup old caches
self.addEventListener('activate', (event) => {
  console.log('[SW] Activating service worker...')
  
  event.waitUntil(
    caches.keys()
      .then((cacheNames) => {
        return Promise.all(
          cacheNames.map((cacheName) => {
            if (cacheName !== CACHE_NAME && cacheName !== DATA_CACHE_NAME) {
              console.log('[SW] Deleting old cache:', cacheName)
              return caches.delete(cacheName)
            }
          })
        )
      })
      .then(() => {
        console.log('[SW] Service worker activated')
        return self.clients.claim()
      })
  )
})

// Fetch event - network strategies
self.addEventListener('fetch', (event) => {
  const { request } = event
  const url = new URL(request.url)

  // Skip non-GET requests
  if (request.method !== 'GET') {
    return
  }

  // Skip Chrome extensions
  if (url.protocol === 'chrome-extension:') {
    return
  }

  // API requests - Network First strategy
  if (url.pathname.includes('/api/')) {
    event.respondWith(
      networkFirst(request, DATA_CACHE_NAME)
    )
    return
  }

  // Firebase requests - Network Only (real-time data)
  if (url.hostname.includes('firebase')) {
    event.respondWith(fetch(request))
    return
  }

  // Static assets - Cache First strategy
  event.respondWith(
    cacheFirst(request, CACHE_NAME)
  )
})

// Cache First Strategy - pentru assets statice
async function cacheFirst(request, cacheName) {
  try {
    const cache = await caches.open(cacheName)
    const cached = await cache.match(request)
    
    if (cached) {
      console.log('[SW] Serving from cache:', request.url)
      return cached
    }

    console.log('[SW] Fetching from network:', request.url)
    const response = await fetch(request)
    
    if (response && response.status === 200) {
      cache.put(request, response.clone())
    }
    
    return response
  } catch (error) {
    console.error('[SW] Cache first failed:', error)
    
    // Fallback pentru HTML
    if (request.headers.get('accept').includes('text/html')) {
      const cache = await caches.open(CACHE_NAME)
      return cache.match('/index.html')
    }
    
    throw error
  }
}

// Network First Strategy - pentru API requests
async function networkFirst(request, cacheName) {
  try {
    console.log('[SW] Fetching from network:', request.url)
    const response = await fetch(request)
    
    if (response && response.status === 200) {
      const cache = await caches.open(cacheName)
      cache.put(request, response.clone())
    }
    
    return response
  } catch (error) {
    console.log('[SW] Network failed, serving from cache:', request.url)
    
    const cache = await caches.open(cacheName)
    const cached = await cache.match(request)
    
    if (cached) {
      return cached
    }
    
    console.error('[SW] Network first failed:', error)
    throw error
  }
}

// Background Sync pentru notificări
self.addEventListener('sync', (event) => {
  console.log('[SW] Background sync:', event.tag)
  
  if (event.tag === 'sync-bus-data') {
    event.waitUntil(syncBusData())
  }
})

async function syncBusData() {
  try {
    console.log('[SW] Syncing bus data...')
    // Aici poți adăuga logică pentru sincronizare în background
    return Promise.resolve()
  } catch (error) {
    console.error('[SW] Sync failed:', error)
    return Promise.reject(error)
  }
}

// Push notifications
self.addEventListener('push', (event) => {
  console.log('[SW] Push notification received')
  
  let data = {
    title: 'Tursib',
    body: 'Autobuzul se apropie!',
    icon: '/icon-192x192.png',
    badge: '/icon-72x72.png'
  }
  
  if (event.data) {
    data = event.data.json()
  }
  
  const options = {
    body: data.body,
    icon: data.icon,
    badge: data.badge,
    vibrate: [200, 100, 200],
    data: {
      dateOfArrival: Date.now(),
      primaryKey: 1
    },
    actions: [
      {
        action: 'explore',
        title: 'Vezi harta'
      },
      {
        action: 'close',
        title: 'Închide'
      }
    ]
  }
  
  event.waitUntil(
    self.registration.showNotification(data.title, options)
  )
})

// Notification click
self.addEventListener('notificationclick', (event) => {
  console.log('[SW] Notification clicked:', event.action)
  
  event.notification.close()
  
  if (event.action === 'explore') {
    event.waitUntil(
      clients.openWindow('/')
    )
  }
})

// Message handler
self.addEventListener('message', (event) => {
  console.log('[SW] Message received:', event.data)
  
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting()
  }
  
  if (event.data && event.data.type === 'CACHE_URLS') {
    const urlsToCache = event.data.urls
    event.waitUntil(
      caches.open(DATA_CACHE_NAME)
        .then((cache) => cache.addAll(urlsToCache))
    )
  }
})

} // End production service worker

console.log('[SW] Service Worker loaded')
