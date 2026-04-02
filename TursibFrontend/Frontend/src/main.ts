import './assets/main.css'
import 'leaflet/dist/leaflet.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { VueFire, VueFireDatabaseOptionsAPI } from 'vuefire'
import { initializeApp } from 'firebase/app'
import { getDatabase } from 'firebase/database'

import App from './App.vue'
import router from './router'

// Initialize dark mode before app mount
import { useDarkMode } from './composables/useDarkMode'
useDarkMode() // This will load saved preference and apply it

// Firebase configuration
// Configurație minimală pentru Realtime Database în mod test
const firebaseConfig = {
  databaseURL: import.meta.env.VITE_FIREBASE_DATABASE_URL || 'https://licenta-ulbs-default-rtdb.europe-west1.firebasedatabase.app'
}

// Initialize Firebase
const firebaseApp = initializeApp(firebaseConfig)
const database = getDatabase(firebaseApp)

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(VueFire, {
  firebaseApp,
  modules: [VueFireDatabaseOptionsAPI()]
})

app.mount('#app')

// Export database pentru a fi folosit în componente
export { database }

// Banner non-blocant pentru actualizări SW
function showUpdateBanner(worker: ServiceWorker) {
  const banner = document.createElement('div')
  banner.id = 'sw-update-banner'
  banner.innerHTML = `
    <span>🔄 Versiune nouă disponibilă!</span>
    <button id="sw-update-btn" style="margin-left:12px;padding:4px 12px;border-radius:6px;border:none;background:#3b82f6;color:#fff;cursor:pointer;font-size:13px">Actualizează</button>
    <button id="sw-dismiss-btn" style="margin-left:6px;padding:4px 8px;border-radius:6px;border:none;background:transparent;color:inherit;cursor:pointer;font-size:13px">×</button>
  `
  Object.assign(banner.style, {
    position: 'fixed', bottom: '72px', left: '50%', transform: 'translateX(-50%)',
    background: 'var(--bg-secondary,#1e293b)', color: 'var(--text-primary,#f8fafc)',
    padding: '10px 16px', borderRadius: '10px', boxShadow: '0 4px 16px rgba(0,0,0,.3)',
    zIndex: '9999', display: 'flex', alignItems: 'center', fontSize: '14px',
    border: '1px solid var(--border-color,#334155)'
  })
  document.body.appendChild(banner)

  document.getElementById('sw-update-btn')?.addEventListener('click', () => {
    worker.postMessage({ type: 'SKIP_WAITING' })
    banner.remove()
    window.location.reload()
  })
  document.getElementById('sw-dismiss-btn')?.addEventListener('click', () => banner.remove())
}

// Handle unhandled promise rejections (especially from vue-leaflet)
window.addEventListener('unhandledrejection', (event) => {
  // Suppress undefined promise rejections from vue-leaflet
  if (event.reason === undefined) {
    console.warn('⚠️ Caught undefined promise rejection (likely from vue-leaflet)')
    event.preventDefault()
    return
  }
  
  // Log other errors normally
  console.error('❌ Unhandled promise rejection:', event.reason)
})

// Înregistrare Service Worker pentru PWA (și în development pentru notificări)
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js')
      .then((registration) => {
        console.log('✅ Service Worker registered:', registration.scope)
        
        // Check for updates every 60 seconds (doar în production)
        if (import.meta.env.PROD) {
          setInterval(() => {
            registration.update()
          }, 60000)
        }
        
        // Listen for updates - banner non-blocant în loc de confirm()
        registration.addEventListener('updatefound', () => {
          const newWorker = registration.installing

          newWorker?.addEventListener('statechange', () => {
            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
              console.log('🔄 New version available! Refresh to update.')
              showUpdateBanner(newWorker)
            }
          })
        })
      })
      .catch((error) => {
        console.error('❌ Service Worker registration failed:', error)
      })
  })
  
  // Handle service worker controller change
  let refreshing = false
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (!refreshing) {
      refreshing = true
      window.location.reload()
    }
  })
}
