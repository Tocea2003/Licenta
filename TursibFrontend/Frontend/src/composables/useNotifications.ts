import { ref, watch, readonly } from 'vue'

interface BusLocation {
  id: string
  latitude: number
  longitude: number
  routeId: number
  routeNumber?: string
  routeName?: string
  speed: number
  occupancy?: number
  timestamp?: number
  heading?: number
  status?: string
  directionId?: number
  nextStationId?: number
  nextStationName?: string
  nextStationEta?: number
}

interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

export interface NotificationSettings {
  enabled: boolean
  stationId: number | null
  routeId: number | null
  notifiedBuses: Set<string>
  favoriteRoutes: number[]
  crowdingAlerts: boolean
  delayAlerts: boolean
  etaThresholdMinutes: number // Notifică când autobuzul e la X minute
}

export interface NotificationHistory {
  id: string
  timestamp: Date
  type: 'arrival' | 'delay' | 'crowding' | 'favorite'
  busId: string
  routeId: number
  message: string
  station?: string
}

const settings = ref<NotificationSettings>({
  enabled: false,
  stationId: null,
  routeId: null,
  notifiedBuses: new Set(),
  favoriteRoutes: [],
  crowdingAlerts: true,
  delayAlerts: true,
  etaThresholdMinutes: 3 // Default: 3 minute
})

const notificationHistory = ref<NotificationHistory[]>([])
const permissionGranted = ref(false)

// Save settings to localStorage
const saveSettings = () => {
  const settingsToSave = {
    ...settings.value,
    notifiedBuses: Array.from(settings.value.notifiedBuses)
  }
  localStorage.setItem('notification_settings', JSON.stringify(settingsToSave))
}

// Load settings from localStorage
const loadSettings = () => {
  const saved = localStorage.getItem('notification_settings')
  if (saved) {
    try {
      const parsed = JSON.parse(saved)
      settings.value = {
        ...parsed,
        notifiedBuses: new Set(parsed.notifiedBuses || []),
        enabled: false // Always start disabled for safety
      }
    } catch (e) {
      console.error('Failed to load notification settings:', e)
    }
  }
}

// Initialize
loadSettings()

// Diagnostic function to check notification support
export const checkNotificationSupport = (): {
  supported: boolean
  permission: NotificationPermission | 'not-supported'
  serviceWorkerRegistered: boolean
  details: string[]
} => {
  const details: string[] = []
  
  // Check if Notifications are supported
  const supported = 'Notification' in window
  details.push(`Notification API: ${supported ? '✅ Supported' : '❌ Not supported'}`)
  
  // Check permission
  const permission = supported ? Notification.permission : 'not-supported'
  details.push(`Permission: ${permission}`)
  
  // Check service worker
  const swSupported = 'serviceWorker' in navigator
  details.push(`Service Worker: ${swSupported ? '✅ Supported' : '❌ Not supported'}`)
  
  // Check if service worker is registered
  const serviceWorkerRegistered = swSupported && navigator.serviceWorker.controller !== null
  details.push(`Service Worker Registered: ${serviceWorkerRegistered ? '✅ Yes' : '⚠️ Not yet'}`)
  
  // Check secure context
  const isSecure = window.isSecureContext
  details.push(`Secure Context (HTTPS/localhost): ${isSecure ? '✅ Yes' : '❌ No'}`)
  
  console.log('🔍 Notification Diagnostics:', details.join(' | '))
  
  return { supported, permission, serviceWorkerRegistered, details }
}

// Request notification permission
export const requestNotificationPermission = async (): Promise<boolean> => {
  if (!('Notification' in window)) {
    console.warn('⚠️ Acest browser nu suportă notificări')
    return false
  }

  if (Notification.permission === 'granted') {
    permissionGranted.value = true
    return true
  }

  if (Notification.permission !== 'denied') {
    const permission = await Notification.requestPermission()
    permissionGranted.value = permission === 'granted'
    return permissionGranted.value
  }

  return false
}

// Calculate distance between two points (Haversine)
const calculateDistance = (
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number
): number => {
  const R = 6371 // km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) *
    Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) *
    Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}

// Calculate more accurate ETA considering traffic and bus speed
const calculateAccurateETA = (bus: BusLocation, station: Station): number => {
  const distance = calculateDistance(
    bus.latitude,
    bus.longitude,
    station.latitude,
    station.longitude
  )

  // Use actual bus speed if available, otherwise use average urban speed
  let speed = bus.speed && bus.speed > 0 ? bus.speed : 30 // km/h
  
  // Adjust speed based on time of day (traffic patterns)
  const hour = new Date().getHours()
  if ((hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 19)) {
    // Rush hour - reduce speed by 30%
    speed *= 0.7
  }

  const etaMinutes = (distance / speed) * 60

  return Math.max(0, etaMinutes) // Never return negative ETA
}

// Detect if bus is delayed
const isBusDelayed = (bus: BusLocation): boolean => {
  // A bus is considered delayed if speed is very low but not stopped
  if (bus.speed && bus.speed > 0 && bus.speed < 10) {
    return true
  }
  
  // Check timestamp if available
  if (bus.timestamp) {
    const age = Date.now() - bus.timestamp
    if (age > 5 * 60 * 1000) { // Data older than 5 minutes
      return true
    }
  }

  return false
}

// Get crowding level description
const getCrowdingLevel = (occupancy: number): { level: string; icon: string; color: string } => {
  if (occupancy < 30) return { level: 'Liber', icon: '😊', color: '#48bb78' }
  if (occupancy < 60) return { level: 'Moderat', icon: '😐', color: '#ecc94b' }
  if (occupancy < 85) return { level: 'Aglomerat', icon: '😟', color: '#ed8936' }
  return { level: 'Foarte aglomerat', icon: '😰', color: '#f56565' }
}

// Add notification to history
const addToHistory = (notification: Omit<NotificationHistory, 'id' | 'timestamp'>) => {
  const entry: NotificationHistory = {
    ...notification,
    id: `notif-${Date.now()}-${Math.random()}`,
    timestamp: new Date()
  }
  
  notificationHistory.value.unshift(entry)
  
  // Keep only last 50 notifications
  if (notificationHistory.value.length > 50) {
    notificationHistory.value = notificationHistory.value.slice(0, 50)
  }
  
  // Save to localStorage
  try {
    localStorage.setItem('notification_history', JSON.stringify(notificationHistory.value.slice(0, 20)))
  } catch (e) {
    console.error('Failed to save notification history:', e)
  }
}

// Check buses and send notifications
export const checkBusNotifications = (
  buses: BusLocation[],
  station: Station | null
) => {
  if (!settings.value.enabled || !station || !permissionGranted.value) {
    return
  }

  // Double check notification permission
  if (!('Notification' in window) || Notification.permission !== 'granted') {
    console.warn('⚠️ Notificările nu sunt disponibile sau permisiunea a fost revocată')
    return
  }

  buses.forEach((bus) => {
    // Skip if we already notified for this bus
    if (settings.value.notifiedBuses.has(bus.id)) {
      return
    }

    // Skip if route filter is set and doesn't match
    if (settings.value.routeId && bus.routeId !== settings.value.routeId) {
      return
    }

    const etaMinutes = calculateAccurateETA(bus, station)
    const isInFavorites = settings.value.favoriteRoutes.includes(bus.routeId)

    // 1. Check for arrival notifications
    if (etaMinutes <= settings.value.etaThresholdMinutes && etaMinutes > 0) {
      sendArrivalNotification(bus, station, Math.round(etaMinutes), isInFavorites)
      settings.value.notifiedBuses.add(bus.id)

      // Reset notification after bus passes (5 min)
      setTimeout(() => {
        settings.value.notifiedBuses.delete(bus.id)
      }, 5 * 60 * 1000)
    }

    // 2. Check for delay notifications
    if (settings.value.delayAlerts && isBusDelayed(bus) && etaMinutes <= 10) {
      sendDelayNotification(bus, station)
    }

    // 3. Check for crowding alerts
    if (settings.value.crowdingAlerts && bus.occupancy && bus.occupancy >= 85 && etaMinutes <= 5) {
      sendCrowdingNotification(bus, station)
    }
  })
}

// Send arrival notification
const sendArrivalNotification = (
  bus: BusLocation,
  station: Station,
  etaMinutes: number,
  isFavorite: boolean
) => {
  try {
    const prefix = isFavorite ? '⭐ ' : '🚌 '
    const title = `${prefix}Autobuzul Linia ${bus.routeId} se apropie!`
    const body = `Va sosi la ${station.name} în ${etaMinutes} ${etaMinutes === 1 ? 'minut' : 'minute'}`

    const notification = new Notification(title, {
      body,
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: `bus-${bus.id}-${station.id}`,
      requireInteraction: false,
      silent: false,
      vibrate: [200, 100, 200]
    } as NotificationOptions)

    notification.onclick = () => {
      window.focus()
      notification.close()
    }

    // Add to history
    addToHistory({
      type: isFavorite ? 'favorite' : 'arrival',
      busId: bus.id,
      routeId: bus.routeId,
      message: body,
      station: station.name
    })

    // Auto close after 10 seconds
    setTimeout(() => {
      try {
        notification.close()
      } catch (e) {
        // Ignore errors on close
      }
    }, 10000)

    console.log(`🔔 Notificare sosire trimisă pentru autobuzul ${bus.id}`)
  } catch (error) {
    console.error('❌ Eroare la crearea notificării:', error)
  }
}

// Send delay notification
const sendDelayNotification = (
  bus: BusLocation,
  station: Station
) => {
  try {
    const title = '⏰ Întârziere detectată'
    const body = `Autobuzul Linia ${bus.routeId} către ${station.name} poate întârzia`

    const notification = new Notification(title, {
      body,
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: `delay-${bus.id}-${station.id}`,
      requireInteraction: false,
      silent: false
    })

    notification.onclick = () => {
      window.focus()
      notification.close()
    }

    // Add to history
    addToHistory({
      type: 'delay',
      busId: bus.id,
      routeId: bus.routeId,
      message: body,
      station: station.name
    })

    setTimeout(() => {
      try {
        notification.close()
      } catch (e) {
        // Ignore
      }
    }, 10000)

    console.log(`⏰ Notificare întârziere trimisă pentru autobuzul ${bus.id}`)
  } catch (error) {
    console.error('❌ Eroare la crearea notificării de întârziere:', error)
  }
}

// Send crowding notification
const sendCrowdingNotification = (
  bus: BusLocation,
  station: Station
) => {
  if (!bus.occupancy) return

  try {
    const crowding = getCrowdingLevel(bus.occupancy)
    const title = `${crowding.icon} Autobuz ${crowding.level.toLowerCase()}`
    const body = `Linia ${bus.routeId} către ${station.name} - Ocupare: ${bus.occupancy}%`

    const notification = new Notification(title, {
      body,
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: `crowding-${bus.id}-${station.id}`,
      requireInteraction: false,
      silent: true // Silent for crowding alerts
    })

    notification.onclick = () => {
      window.focus()
      notification.close()
    }

    // Add to history
    addToHistory({
      type: 'crowding',
      busId: bus.id,
      routeId: bus.routeId,
      message: body,
      station: station.name
    })

    setTimeout(() => {
      try {
        notification.close()
      } catch (e) {
        // Ignore
      }
    }, 8000)

    console.log(`🚌 Notificare aglomerație trimisă pentru autobuzul ${bus.id}`)
  } catch (error) {
    console.error('❌ Eroare la crearea notificării de aglomerație:', error)
  }
}

// Enable notifications for a station
export const enableNotifications = async (
  stationId: number,
  routeId?: number,
  customSettings?: Partial<NotificationSettings>
): Promise<boolean> => {
  const hasPermission = await requestNotificationPermission()

  if (!hasPermission) {
    console.warn('⚠️ Permisiunea pentru notificări a fost refuzată sau indisponibilă')
    return false
  }

  settings.value.enabled = true
  settings.value.stationId = stationId
  settings.value.routeId = routeId || null
  settings.value.notifiedBuses.clear()

  // Apply custom settings if provided
  if (customSettings) {
    Object.assign(settings.value, customSettings)
  }

  saveSettings()
  console.log(`✅ Notificări activate pentru stația ${stationId}`)
  
  // Trimite notificare de test
  try {
    const testNotification = new Notification('🔔 Notificări activate!', {
      body: 'Vei primi alerte când autobuzele se apropie de stație',
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: 'test-notification',
      requireInteraction: false,
      silent: false
    })

    testNotification.onclick = () => {
      window.focus()
      testNotification.close()
    }

    setTimeout(() => {
      try {
        testNotification.close()
      } catch (e) {
        // Ignore errors on close
      }
    }, 5000)
    console.log('✅ Notificare de test trimisă')
  } catch (error) {
    console.error('❌ Eroare la trimiterea notificării de test:', error)
    // Don't fail the whole operation if test notification fails
  }

  return true
}

// Disable notifications
export const disableNotifications = () => {
  settings.value.enabled = false
  settings.value.stationId = null
  settings.value.routeId = null
  settings.value.notifiedBuses.clear()

  saveSettings()
  console.log('🔕 Notificări dezactivate')
}

// Update notification settings
export const updateNotificationSettings = (newSettings: Partial<NotificationSettings>) => {
  Object.assign(settings.value, newSettings)
  saveSettings()
  console.log('⚙️ Setări notificări actualizate:', newSettings)
}

// Add/remove favorite routes
export const addFavoriteRoute = (routeId: number) => {
  if (!settings.value.favoriteRoutes.includes(routeId)) {
    settings.value.favoriteRoutes.push(routeId)
    saveSettings()
    console.log(`⭐ Traseu ${routeId} adăugat la favorite`)
  }
}

export const removeFavoriteRoute = (routeId: number) => {
  const index = settings.value.favoriteRoutes.indexOf(routeId)
  if (index > -1) {
    settings.value.favoriteRoutes.splice(index, 1)
    saveSettings()
    console.log(`Traseu ${routeId} eliminat din favorite`)
  }
}

// Clear notification history
export const clearNotificationHistory = () => {
  notificationHistory.value = []
  localStorage.removeItem('notification_history')
  console.log('🗑️ Istoric notificări șters')
}

// Get current settings
export const getNotificationSettings = () => settings.value
export const getNotificationHistory = () => notificationHistory.value

export const useNotifications = () => {
  return {
    requestNotificationPermission,
    enableNotifications,
    disableNotifications,
    updateNotificationSettings,
    checkBusNotifications,
    getNotificationSettings,
    getNotificationHistory,
    checkNotificationSupport,
    addFavoriteRoute,
    removeFavoriteRoute,
    clearNotificationHistory,
    permissionGranted,
    settings: readonly(settings),
    notificationHistory: readonly(notificationHistory)
  }
}
