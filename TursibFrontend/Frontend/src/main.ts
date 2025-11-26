import './assets/main.css'
import 'leaflet/dist/leaflet.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { VueFire, VueFireDatabaseOptionsAPI } from 'vuefire'
import { initializeApp } from 'firebase/app'
import { getDatabase } from 'firebase/database'

import App from './App.vue'
import router from './router'

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