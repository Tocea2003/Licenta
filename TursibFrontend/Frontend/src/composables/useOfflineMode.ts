import { ref, computed } from 'vue'
import type { Ref } from 'vue'

interface Route {
  id: number
  routeNumber: string
  name: string
  color?: string
}

interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

interface CachedData {
  routes: Route[]
  stations: Station[]
  shapes: any[]
  lastUpdate: number
}

const DB_NAME = 'tursib-offline'
const DB_VERSION = 1
const STORES = {
  ROUTES: 'routes',
  STATIONS: 'stations',
  SHAPES: 'shapes',
  METADATA: 'metadata'
}

// Reactive state
const isOnline = ref(navigator.onLine)
const isSyncing = ref(false)
const lastSyncTime = ref<number | null>(null)

// Initialize IndexedDB
let db: IDBDatabase | null = null

const initDB = (): Promise<IDBDatabase> => {
  return new Promise((resolve, reject) => {
    if (db) {
      resolve(db)
      return
    }

    const request = indexedDB.open(DB_NAME, DB_VERSION)

    request.onerror = () => {
      reject(new Error('Failed to open IndexedDB'))
    }

    request.onsuccess = () => {
      db = request.result
      console.log('✅ IndexedDB initialized')
      resolve(db)
    }

    request.onupgradeneeded = (event) => {
      const database = (event.target as IDBOpenDBRequest).result

      // Create object stores
      if (!database.objectStoreNames.contains(STORES.ROUTES)) {
        const routesStore = database.createObjectStore(STORES.ROUTES, { keyPath: 'id' })
        routesStore.createIndex('routeNumber', 'routeNumber', { unique: false })
      }

      if (!database.objectStoreNames.contains(STORES.STATIONS)) {
        const stationsStore = database.createObjectStore(STORES.STATIONS, { keyPath: 'id' })
        stationsStore.createIndex('name', 'name', { unique: false })
      }

      if (!database.objectStoreNames.contains(STORES.SHAPES)) {
        database.createObjectStore(STORES.SHAPES, { keyPath: 'id' })
      }

      if (!database.objectStoreNames.contains(STORES.METADATA)) {
        database.createObjectStore(STORES.METADATA, { keyPath: 'key' })
      }

      console.log('✅ IndexedDB stores created')
    }
  })
}

// Save data to IndexedDB
const saveToStore = async <T>(storeName: string, data: T[]): Promise<void> => {
  const database = await initDB()
  
  return new Promise((resolve, reject) => {
    const transaction = database.transaction([storeName], 'readwrite')
    const store = transaction.objectStore(storeName)

    // Clear existing data
    store.clear()

    // Add new data
    data.forEach((item) => {
      store.add(item)
    })

    transaction.oncomplete = () => {
      console.log(`✅ Saved ${data.length} items to ${storeName}`)
      resolve()
    }

    transaction.onerror = () => {
      reject(new Error(`Failed to save to ${storeName}`))
    }
  })
}

// Read data from IndexedDB
const readFromStore = async <T>(storeName: string): Promise<T[]> => {
  const database = await initDB()
  
  return new Promise((resolve, reject) => {
    const transaction = database.transaction([storeName], 'readonly')
    const store = transaction.objectStore(storeName)
    const request = store.getAll()

    request.onsuccess = () => {
      console.log(`✅ Read ${request.result.length} items from ${storeName}`)
      resolve(request.result)
    }

    request.onerror = () => {
      reject(new Error(`Failed to read from ${storeName}`))
    }
  })
}

// Save metadata
const saveMetadata = async (key: string, value: any): Promise<void> => {
  const database = await initDB()
  
  return new Promise((resolve, reject) => {
    const transaction = database.transaction([STORES.METADATA], 'readwrite')
    const store = transaction.objectStore(STORES.METADATA)
    
    store.put({ key, value })

    transaction.oncomplete = () => {
      resolve()
    }

    transaction.onerror = () => {
      reject(new Error('Failed to save metadata'))
    }
  })
}

// Read metadata
const readMetadata = async (key: string): Promise<any> => {
  const database = await initDB()
  
  return new Promise((resolve, reject) => {
    const transaction = database.transaction([STORES.METADATA], 'readonly')
    const store = transaction.objectStore(STORES.METADATA)
    const request = store.get(key)

    request.onsuccess = () => {
      resolve(request.result?.value)
    }

    request.onerror = () => {
      reject(new Error('Failed to read metadata'))
    }
  })
}

// Sync data from API to IndexedDB
const syncData = async (apiBaseUrl: string): Promise<void> => {
  if (!isOnline.value) {
    console.log('⚠️ Cannot sync - offline')
    return
  }

  isSyncing.value = true

  try {
    console.log('🔄 Syncing data from API...')

    // Fetch all data with error handling
    const [routesRes, stationsRes, shapesRes] = await Promise.all([
      fetch(`${apiBaseUrl}/api/Routes`).catch(() => null),
      fetch(`${apiBaseUrl}/api/Stations`).catch(() => null),
      fetch(`${apiBaseUrl}/api/Shapes`).catch(() => null)
    ])

    // Check if any request failed
    if (!routesRes || !stationsRes || !shapesRes) {
      console.log('⚠️ Backend offline, skipping sync')
      return
    }

    // Check for 404 or other errors
    if (!routesRes.ok || !stationsRes.ok || !shapesRes.ok) {
      console.log('⚠️ API returned errors, skipping sync')
      return
    }

    const routes = await routesRes.json()
    const stations = await stationsRes.json()
    const shapes = await shapesRes.json()

    // Save to IndexedDB
    await Promise.all([
      saveToStore(STORES.ROUTES, routes),
      saveToStore(STORES.STATIONS, stations),
      saveToStore(STORES.SHAPES, shapes)
    ])

    // Update metadata
    const now = Date.now()
    await saveMetadata('lastSync', now)
    lastSyncTime.value = now

    console.log('✅ Data synced successfully')
  } catch (error) {
    console.log('⚠️ Sync skipped:', error)
    // Don't throw - offline mode should be optional
  } finally {
    isSyncing.value = false
  }
}

// Get data (from IndexedDB if offline, from API if online)
const getData = async <T>(
  storeName: string, 
  apiUrl: string
): Promise<T[]> => {
  if (isOnline.value) {
    try {
      const response = await fetch(apiUrl)
      const data = await response.json()
      
      // Save to cache for offline use
      await saveToStore(storeName, data)
      
      return data
    } catch (error) {
      console.warn('⚠️ API failed, using cached data:', error)
      return readFromStore<T>(storeName)
    }
  } else {
    console.log('📦 Using cached data (offline)')
    return readFromStore<T>(storeName)
  }
}

// Initialize
const init = async (apiBaseUrl: string): Promise<void> => {
  await initDB()
  
  // Load last sync time
  const lastSync = await readMetadata('lastSync')
  lastSyncTime.value = lastSync

  // Sync if online and data is stale (> 24 hours)
  const ONE_DAY = 24 * 60 * 60 * 1000
  if (isOnline.value && (!lastSync || Date.now() - lastSync > ONE_DAY)) {
    await syncData(apiBaseUrl)
  }
}

// Listen to online/offline events
window.addEventListener('online', () => {
  console.log('🌐 Connection restored')
  isOnline.value = true
})

window.addEventListener('offline', () => {
  console.log('📴 Connection lost')
  isOnline.value = false
})

export const useOfflineMode = () => {
  return {
    // State
    isOnline: computed(() => isOnline.value),
    isSyncing: computed(() => isSyncing.value),
    lastSyncTime: computed(() => lastSyncTime.value),
    
    // Methods
    init,
    syncData,
    getData,
    
    // Direct store access
    saveToStore,
    readFromStore,
    saveMetadata,
    readMetadata,
    
    // Store names
    STORES
  }
}
