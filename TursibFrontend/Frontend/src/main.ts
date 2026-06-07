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
const firebaseConfig = {
  apiKey: "AIzaSyCmoHsN90YRcoWN7ItrTpR4QKLDyHthFrY",
  authDomain: "licenta-ulbs.firebaseapp.com",
  databaseURL: import.meta.env.VITE_FIREBASE_DATABASE_URL || "https://licenta-ulbs-default-rtdb.europe-west1.firebasedatabase.app",
  projectId: "licenta-ulbs",
  storageBucket: "licenta-ulbs.firebasestorage.app",
  messagingSenderId: "414414048044",
  appId: "1:414414048044:web:9e449f6135afa01f4bba89"
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
        
        // Listen for updates
        registration.addEventListener('updatefound', () => {
          const newWorker = registration.installing
          
          newWorker?.addEventListener('statechange', () => {
            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
              console.log('🔄 New version available! Refresh to update.')
              
              // Optionally show a notification to user
              if (confirm('O versiune nouă este disponibilă! Reîmprospătați pagina?')) {
                newWorker.postMessage({ type: 'SKIP_WAITING' })
                window.location.reload()
              }
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
